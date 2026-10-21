"""MPPS status transitions: discontinued path and refused transitions

Covers the other ways an MPPS can move, and that the enabler refuses messages that do not fit
the procedure's state. MPPS IN PROGRESS makes the MWL service send Scan Started; MPPS COMPLETED
sends nothing - the Store SCU sends Scan Completed once the study's images are uploaded:
  IN PROGRESS -> DISCONTINUED             accepted, Scan Started sent, no Scan Completed
  DISCONTINUED -> COMPLETED               refused (no procedure in progress), no status sent
  N-CREATE for an unknown procedure step  refused
  N-SET for an unknown SOP Instance       refused
  N-SET with a status other than COMPLETED/DISCONTINUED  refused as an invalid attribute value
"""
import re

from pydicom.uid import generate_uid

from checks import tag_displays, wait_for_tag
from conftest import need, sr
from dicom_client import SUCCESS, status_name
from flows import wait_for_scan_status

PROCESSING_FAILURE = 0x0110
INVALID_ATTRIBUTE_VALUE = 0x0106


def test_in_progress_then_discontinued(cfg, api, dicom, ctx, logs, record):
    """IN PROGRESS -> DISCONTINUED: both accepted, with Scan Started sent and no Scan Completed"""
    need(ctx, "worklist_ok")
    entry = sr(ctx, "discontinue")
    record["service_request_id"] = entry["id"]
    record["accession_number"] = entry["accession_number"]

    status, mpps_uid = dicom.mpps_in_progress(entry["worklist_item"])
    record["mpps_sop_instance_uid"] = mpps_uid
    record["n_create_status"] = status_name(status)
    assert status == SUCCESS, f"N-CREATE returned {status_name(status)}"
    logs.wait_for("mwl", rf"\[MPPS\] SetInProgress: matched ServiceRequestId={entry['id']}", timeout=15)
    record["scan_started"] = wait_for_scan_status(cfg, logs, entry, cfg.status_started, timeout=60, service="mwl")

    status = dicom.mpps_set(mpps_uid, "DISCONTINUED")
    record["n_set_status"] = status_name(status)
    assert status == SUCCESS, f"N-SET DISCONTINUED returned {status_name(status)}"
    logs.wait_for("mwl", rf"\[MPPS\] SetDiscontinued: ServiceRequestId={entry['id']}", timeout=15)
    tags = wait_for_tag(api, cfg, entry["id"], cfg.status_started, record)
    assert cfg.status_completed not in tags, \
        "CARE shows the SR completed although the procedure was discontinued"
    entry["discontinued_mpps_uid"] = mpps_uid


def test_completed_after_discontinued_is_refused(cfg, api, dicom, ctx, logs, record):
    """DISCONTINUED -> COMPLETED is refused and no status is sent to CARE"""
    entry = sr(ctx, "discontinue")
    mpps_uid, = need(entry, "discontinued_mpps_uid")
    status = dicom.mpps_set(mpps_uid, "COMPLETED")
    record["service_request_id"] = entry["id"]
    record["mpps_sop_instance_uid"] = mpps_uid
    record["n_set_status"] = status_name(status)
    assert status == PROCESSING_FAILURE, f"Expected Processing Failure, got {status_name(status)}"
    logs.wait_for("mwl", rf"\[MPPS\] SetCompleted: no procedure in progress for SOPInstanceUID={re.escape(mpps_uid)}", timeout=15)
    logs.assert_absent("mwl", r"Scan status sent to CARE")
    assert cfg.status_completed not in tag_displays(api, entry["id"]), \
        "CARE shows the SR completed although the procedure was discontinued"


def test_n_create_for_unknown_procedure_step_is_refused(dicom, ctx, logs, record):
    """N-CREATE for a procedure step that is not on the worklist is refused"""
    entry = sr(ctx, "discontinue")
    need(entry, "worklist_item")
    status, mpps_uid = dicom.mpps_in_progress(entry["worklist_item"], procedure_step_id="NOSUCHSTEP")
    record["procedure_step_id"] = "NOSUCHSTEP"
    record["n_create_status"] = status_name(status)
    assert status == PROCESSING_FAILURE, f"Expected Processing Failure, got {status_name(status)}"
    logs.wait_for("mwl", r"\[MPPS\] SetInProgress: no worklist item matched ProcedureStepID=NOSUCHSTEP", timeout=15)
    logs.assert_absent("mwl", r"Scan status sent to CARE")


def test_n_set_for_unknown_instance_is_refused(dicom, ctx, logs, record):
    """N-SET COMPLETED for an MPPS that was never created is refused"""
    need(ctx, "worklist_ok")
    unknown = generate_uid()
    status = dicom.mpps_set(unknown, "COMPLETED")
    record["mpps_sop_instance_uid"] = unknown
    record["n_set_status"] = status_name(status)
    assert status == PROCESSING_FAILURE, f"Expected Processing Failure, got {status_name(status)}"
    logs.wait_for("mwl", rf"\[MPPS\] SetCompleted: no procedure in progress for SOPInstanceUID={re.escape(unknown)}", timeout=15)
    logs.assert_absent("mwl", r"Scan status sent to CARE")


def test_n_set_with_invalid_status_is_refused(dicom, ctx, logs, record):
    """N-SET with a status other than COMPLETED or DISCONTINUED is refused as invalid"""
    need(ctx, "worklist_ok")
    uid = generate_uid()
    status = dicom.mpps_set(uid, "PAUSED")
    record["status_sent"] = "PAUSED"
    record["n_set_status"] = status_name(status)
    assert status == INVALID_ATTRIBUTE_VALUE, f"Expected Invalid Attribute Value, got {status_name(status)}"
    logs.wait_for("mwl", r"\[MPPS\]\[N-SET\] Rejected status 'PAUSED'", timeout=15)
