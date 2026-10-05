"""DICOM SCU side of the suite, acting as the modality.

Uses pynetdicom so MPPS is sent as real N-CREATE / N-SET messages (DCMTK's storescu can only
send C-STORE, which is why the earlier workflow could never exercise MPPS).
"""
import datetime as dt
import math
import socket
import time

from pydicom.dataset import Dataset, FileMetaDataset
from pydicom.sequence import Sequence
from pydicom.uid import ExplicitVRLittleEndian, PYDICOM_IMPLEMENTATION_UID, generate_uid
from pynetdicom import AE
from pynetdicom.sop_class import (
    CTImageStorage,
    ModalityPerformedProcedureStep,
    ModalityWorklistInformationFind,
    Verification,
)

SUCCESS = 0x0000
PENDING = (0xFF00, 0xFF01)
# Status names for the report.
STATUS_NAMES = {
    0x0000: "Success",
    0x0106: "Invalid Attribute Value",
    0x0110: "Processing Failure",
    0x0112: "No Such SOP Instance",
    0x0122: "SOP Class Not Supported",
    0xA700: "Out of Resources",
    0xA900: "Dataset Does Not Match SOP Class",
    0xC000: "Cannot Understand",
    0xC001: "Processing Failure (Cannot Understand)",
    0xC211: "Unable to Process",
}


def status_name(code):
    if code is None:
        return "no response"
    return f"0x{code:04X} {STATUS_NAMES.get(code, '')}".strip()


def port_open(host, port, timeout=3):
    try:
        with socket.create_connection((host, port), timeout=timeout):
            return True
    except OSError:
        return False


class DicomClient:
    def __init__(self, cfg):
        self.cfg = cfg

    def _ae(self, contexts, calling_aet=None):
        ae = AE(ae_title=calling_aet or self.cfg.calling_aet)
        # Generous timeouts: the large-file C-STORE and the CARE round trip behind C-FIND both
        # take real time on a CI runner.
        ae.acse_timeout = 30
        ae.dimse_timeout = 300
        ae.network_timeout = 300
        for ctx in contexts:
            ae.add_requested_context(ctx, ExplicitVRLittleEndian)
        return ae

    def associate(self, port, called_aet, contexts, calling_aet=None):
        ae = self._ae(contexts, calling_aet)
        return ae.associate(self.cfg.host, port, ae_title=called_aet)

    # -- verification --------------------------------------------------------------------
    def echo(self, port, called_aet, calling_aet=None):
        """Returns (association_established, status_code)."""
        assoc = self.associate(port, called_aet, [Verification], calling_aet)
        if not assoc.is_established:
            return False, None
        try:
            status = assoc.send_c_echo()
            return True, getattr(status, "Status", None)
        finally:
            assoc.release()

    # -- worklist ------------------------------------------------------------------------
    def find_worklist(self, modality=None, scheduled_aet=None):
        """C-FIND against the MWL SCP. Returns (final_status, [item dicts])."""
        query = Dataset()
        for keyword in ("PatientName", "PatientID", "PatientBirthDate", "PatientSex", "AccessionNumber",
                        "RequestedProcedureID", "RequestedProcedureDescription", "ReferringPhysicianName",
                        "InstitutionName"):
            setattr(query, keyword, "")
        sps = Dataset()
        sps.ScheduledStationAETitle = scheduled_aet or ""
        sps.Modality = modality or ""
        sps.ScheduledProcedureStepStartDate = ""
        sps.ScheduledProcedureStepStartTime = ""
        sps.ScheduledProcedureStepID = ""
        sps.ScheduledProcedureStepDescription = ""
        query.ScheduledProcedureStepSequence = Sequence([sps])

        assoc = self.associate(self.cfg.mwl_port, self.cfg.mwl_aet, [ModalityWorklistInformationFind])
        assert assoc.is_established, "MWL SCP refused the C-FIND association"
        items, final = [], None
        try:
            for status, identifier in assoc.send_c_find(query, ModalityWorklistInformationFind):
                code = getattr(status, "Status", None)
                if code in PENDING and identifier is not None:
                    items.append(_worklist_item(identifier))
                else:
                    final = code
        finally:
            assoc.release()
        return final, items

    # -- MPPS ----------------------------------------------------------------------------
    def mpps_in_progress(self, item, sop_instance_uid=None, procedure_step_id=None):
        """N-CREATE an MPPS IN PROGRESS for a worklist item. Returns (status_code, sop_instance_uid)."""
        sop_instance_uid = sop_instance_uid or generate_uid()
        now = dt.datetime.now()
        ds = Dataset()
        step = Dataset()
        step.AccessionNumber = item["accession_number"]
        step.ScheduledProcedureStepID = procedure_step_id or item["procedure_step_id"]
        step.RequestedProcedureID = item.get("requested_procedure_id", "")
        step.StudyInstanceUID = item.get("study_uid") or generate_uid()
        step.ScheduledProcedureStepDescription = item.get("description", "")
        ds.ScheduledStepAttributesSequence = Sequence([step])
        ds.PatientName = item.get("patient_name", "")
        ds.PatientID = item.get("patient_id", "")
        ds.PerformedProcedureStepID = now.strftime("%H%M%S%f")[:16]
        ds.PerformedStationAETitle = self.cfg.calling_aet
        ds.PerformedStationName = "CARE CI"
        ds.PerformedLocation = "CI"
        ds.PerformedProcedureStepStartDate = now.strftime("%Y%m%d")
        ds.PerformedProcedureStepStartTime = now.strftime("%H%M%S")
        ds.PerformedProcedureStepStatus = "IN PROGRESS"
        ds.PerformedProcedureStepDescription = item.get("description", "")
        ds.Modality = self.cfg.modality
        ds.PerformedSeriesSequence = Sequence([])

        status = self._n_call("create", ds, sop_instance_uid)
        return status, sop_instance_uid

    def mpps_set(self, sop_instance_uid, status_value, series_uid=None, instance_uids=()):
        """N-SET the MPPS to COMPLETED / DISCONTINUED (or any value, for negative tests)."""
        now = dt.datetime.now()
        ds = Dataset()
        ds.PerformedProcedureStepStatus = status_value
        ds.PerformedProcedureStepEndDate = now.strftime("%Y%m%d")
        ds.PerformedProcedureStepEndTime = now.strftime("%H%M%S")
        if status_value == "COMPLETED":
            series = Dataset()
            series.SeriesInstanceUID = series_uid or generate_uid()
            series.PerformingPhysicianName = ""
            series.ProtocolName = "CARE CI"
            series.OperatorsName = ""
            series.SeriesDescription = "CARE CI series"
            refs = []
            for uid in instance_uids:
                ref = Dataset()
                ref.ReferencedSOPClassUID = CTImageStorage
                ref.ReferencedSOPInstanceUID = uid
                refs.append(ref)
            series.ReferencedImageSequence = Sequence(refs)
            series.ReferencedNonImageCompositeSOPInstanceSequence = Sequence([])
            ds.PerformedSeriesSequence = Sequence([series])
        return self._n_call("set", ds, sop_instance_uid)

    def _n_call(self, kind, ds, sop_instance_uid):
        assoc = self.associate(self.cfg.mwl_port, self.cfg.mwl_aet, [ModalityPerformedProcedureStep])
        assert assoc.is_established, "MWL SCP refused the MPPS association"
        try:
            if kind == "create":
                status, _ = assoc.send_n_create(ds, ModalityPerformedProcedureStep, sop_instance_uid)
            else:
                status, _ = assoc.send_n_set(ds, ModalityPerformedProcedureStep, sop_instance_uid)
            return getattr(status, "Status", None) if status else None
        finally:
            if assoc.is_established:
                assoc.release()

    # -- storage -------------------------------------------------------------------------
    def store(self, ds, calling_aet=None):
        """C-STORE a dataset to the Store SCP. Returns (status_code, seconds)."""
        assoc = self.associate(self.cfg.store_port, self.cfg.store_aet, [CTImageStorage], calling_aet)
        assert assoc.is_established, "Store SCP refused the C-STORE association"
        started = time.monotonic()
        try:
            status = assoc.send_c_store(ds)
            return (getattr(status, "Status", None) if status else None), round(time.monotonic() - started, 2)
        finally:
            if assoc.is_established:
                assoc.release()


def _worklist_item(identifier):
    sps = identifier.ScheduledProcedureStepSequence[0] if "ScheduledProcedureStepSequence" in identifier else Dataset()
    return {
        "accession_number": str(identifier.get("AccessionNumber", "") or ""),
        "patient_id": str(identifier.get("PatientID", "") or ""),
        "patient_name": str(identifier.get("PatientName", "") or ""),
        "requested_procedure_id": str(identifier.get("RequestedProcedureID", "") or ""),
        "description": str(identifier.get("RequestedProcedureDescription", "") or ""),
        "procedure_step_id": str(sps.get("ScheduledProcedureStepID", "") or ""),
        "modality": str(sps.get("Modality", "") or ""),
        "scheduled_aet": str(sps.get("ScheduledStationAETitle", "") or ""),
        "institution": str(identifier.get("InstitutionName", "") or ""),
    }


def build_ct_image(item, target_bytes=8192, study_uid=None, series_uid=None):
    """A CT image for a worklist item, carrying its accession number and patient ID exactly as a
    modality would copy them from the MWL response. Pixel data is sized to ~target_bytes."""
    side = max(8, int(math.sqrt(max(target_bytes, 128) / 2)))
    now = dt.datetime.now()
    study_uid = study_uid or generate_uid()
    series_uid = series_uid or generate_uid()
    sop_uid = generate_uid()

    ds = Dataset()
    ds.file_meta = FileMetaDataset()
    ds.file_meta.MediaStorageSOPClassUID = CTImageStorage
    ds.file_meta.MediaStorageSOPInstanceUID = sop_uid
    ds.file_meta.TransferSyntaxUID = ExplicitVRLittleEndian
    ds.file_meta.ImplementationClassUID = PYDICOM_IMPLEMENTATION_UID

    ds.SOPClassUID = CTImageStorage
    ds.SOPInstanceUID = sop_uid
    ds.StudyInstanceUID = study_uid
    ds.SeriesInstanceUID = series_uid
    ds.FrameOfReferenceUID = generate_uid()
    ds.StudyDate = ds.SeriesDate = ds.ContentDate = now.strftime("%Y%m%d")
    ds.StudyTime = ds.SeriesTime = ds.ContentTime = now.strftime("%H%M%S")
    ds.AccessionNumber = item["accession_number"]
    ds.PatientID = item["patient_id"]
    ds.PatientName = item.get("patient_name", "")
    ds.PatientBirthDate = ""
    ds.PatientSex = ""
    ds.Modality = "CT"
    ds.StudyID = "1"
    ds.SeriesNumber = 1
    ds.InstanceNumber = 1
    ds.StudyDescription = item.get("description", "") or "CARE CI study"
    ds.SeriesDescription = "CARE CI series"
    ds.InstitutionName = item.get("institution", "")
    ds.ImageType = ["ORIGINAL", "PRIMARY", "AXIAL"]
    ds.ImagePositionPatient = [0, 0, 0]
    ds.ImageOrientationPatient = [1, 0, 0, 0, 1, 0]
    ds.PixelSpacing = [0.5, 0.5]
    ds.SliceThickness = 1.0
    ds.KVP = 120
    ds.SamplesPerPixel = 1
    ds.PhotometricInterpretation = "MONOCHROME2"
    ds.Rows = side
    ds.Columns = side
    ds.BitsAllocated = 16
    ds.BitsStored = 12
    ds.HighBit = 11
    ds.PixelRepresentation = 0
    ds.RescaleIntercept = -1024
    ds.RescaleSlope = 1
    pattern = bytes(range(256)) * 16
    n = side * side * 2
    ds.add_new(0x7FE00010, "OW", (pattern * (n // len(pattern) + 1))[:n])
    return ds
