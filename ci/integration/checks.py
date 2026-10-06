"""Read-back checks against CARE, shared by the MPPS and upload tests."""
from care_api import poll


def tag_displays(api, sr_id):
    return {t.get("display") for t in api.get_service_request(sr_id).get("tags", [])}


def wait_for_tag(api, cfg, sr_id, display, record):
    """Poll until the SR carries the tag the status webhook sets for display."""
    tags = poll(lambda: (lambda t: t if display in t else None)(tag_displays(api, sr_id)),
                timeout=cfg.timeout_s, description=f"tag '{display}' on service request {sr_id}")
    record["sr_tags"] = sorted(t for t in tags if t)
    return tags


def wait_for_mpps_radiology_status(api, cfg, sr_id, expected, record):
    """Radiology record status set by an MPPS webhook - only checked when CARE drives it, i.e.
    the plugin's own status constants are in use (see Config.radiology_status_tracks_mpps)."""
    if not cfg.radiology_status_tracks_mpps:
        record["radiology_status"] = "not driven by MPPS for these status strings - SR tag is the status"
        return None
    return wait_for_radiology_status(api, cfg, sr_id, expected, record)


def wait_for_radiology_status(api, cfg, sr_id, expected, record):
    """Poll the care_radiology record status. Records, rather than fails, when the test user
    lacks can_read_radiology_data - the tag check is then the only state read back."""
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
