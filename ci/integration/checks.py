"""Read-back checks against CARE, shared by the MPPS and upload tests."""
from care_api import poll


def wait_for_radiology_status(api, cfg, sr_id, expected, record):
    """Poll the care_radiology record status. Records, rather than fails, when the test user
    lacks can_read_radiology_data - the scan status log lines are then the only check."""
    if api.radiology_service_request(sr_id) is None:
        record["radiology_status"] = "not readable by the test user (needs can_read_radiology_data)"
        return None
    status = poll(lambda: (lambda r: r["status"] if r and r.get("status") == expected else None)(
        api.radiology_service_request(sr_id)), timeout=cfg.timeout_s,
        description=f"radiology status {expected} on service request {sr_id}")
    record["radiology_status"] = status
    return status


def wait_for_linked_study(api, cfg, sr_id, study_uid, record, timeout=None):
    """Poll /dicom/studies/?serviceRequestId= until the study is linked to the SR."""
    def linked():
        for study in api.studies_for_service_request(sr_id):
            if study.get("study_uid") == study_uid:
                return study
        return None

    study = poll(linked, timeout=timeout or cfg.timeout_s, interval=5,
                 description=f"study {study_uid} linked to service request {sr_id}")
    record["care_study_external_id"] = str(study.get("external_id"))
    record["care_study_series"] = len(study.get("study_series") or [])
    return study
