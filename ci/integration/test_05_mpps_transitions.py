"""MPPS status transitions: discontinued path and refused transitions

Covers the other ways an MPPS can move, and that the enabler refuses messages that do not fit
the procedure's state instead of forwarding them to CARE:
  IN PROGRESS -> DISCONTINUED             accepted, 'discontinued' webhook
  DISCONTINUED -> COMPLETED               refused (no procedure in progress), no webhook
  N-CREATE for an unknown procedure step  refused
  N-SET for an unknown SOP Instance       refused
  N-SET with a status other than COMPLETED/DISCONTINUED  refused as an invalid attribute value
"""
import re

from pydicom.uid import generate_uid

from checks import wait_for_mpps_radiology_status, wait_for_tag
from conftest import need, sr
from dicom_client import SUCCESS, status_name

PROCESSING_FAILURE = 0x0110
INVALID_ATTRIBUTE_VALUE = 0x0106


def test_in_progress_then_discontinued(cfg, api, dicom, ctx, logs, record):
    """IN PROGRESS -> DISCONTINUED: both accepted and CARE marks the SR cancelled"""
    need(ctx, "worklist_ok")
    entry = sr(ctx, "discontinue")
    record["service_request_id"] = entry["id"]
    record["accession_number"] = entry["accession_number"]

    status, mpps_uid = dicom.mpps_in_progress(entry["worklist_item"])
    record["mpps_sop_instance_uid"] = mpps_uid
    record["n_create_status"] = status_name(status)
    assert status == SUCCESS, f"N-CREATE returned {status_name(status)}"
    logs.wait_for("mwl", rf"\[MPPS\] MPPS webhook sent to CARE: {re.escape(cfg.status_started)} for service_request {entry['id']}", timeout=60)
    wait_for_tag(api, cfg, entry["id"], cfg.status_started, record)

    status = dicom.mpps_set(mpps_uid, "DISCONTINUED")
    record["n_set_status"] = status_name(status)
    assert status == SUCCESS, f"N-SET DISCONTINUED returned {status_name(status)}"
    hook = logs.wait_for("mwl", rf"\[MPPS\] MPPS webhook (sent to CARE|failed|skipped): {re.escape(cfg.status_discontinued)}.*", timeout=60)
    record["status_webhook"] = hook.group(0)
    assert hook.group(1) == "sent to CARE", hook.group(0)
    wait_for_tag(api, cfg, entry["id"], cfg.status_discontinued, record)
    wait_for_mpps_radiology_status(api, cfg, entry["id"], "CANCELLED", record)
    entry["discontinued_mpps_uid"] = mpps_uid


def test_completed_after_discontinued_is_refused(cfg, api, dicom, ctx, logs, record):
    """DISCONTINUED -> COMPLETED is refused and no completed webhook is sent"""
    entry = sr(ctx, "discontinue")
    mpps_uid, = need(entry, "discontinued_mpps_uid")
    status = dicom.mpps_set(mpps_uid, "COMPLETED")
    record["service_request_id"] = entry["id"]
    record["mpps_sop_instance_uid"] = mpps_uid
    record["n_set_status"] = status_name(status)
    assert status == PROCESSING_FAILURE, f"Expected Processing Failure, got {status_name(status)}"
    logs.wait_for("mwl", rf"\[MPPS\] SetCompleted: no procedure in progress for SOPInstanceUID={re.escape(mpps_uid)}", timeout=15)
    logs.assert_absent("mwl", rf"MPPS webhook sent to CARE: {re.escape(cfg.status_completed)} for service_request {entry['id']}")
    assert cfg.status_completed not in {t.get("display") for t in api.get_service_request(entry["id"]).get("tags", [])}, \
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
    logs.assert_absent("mwl", r"MPPS webhook sent to CARE")


def test_n_set_for_unknown_instance_is_refused(dicom, ctx, logs, record):
    """N-SET COMPLETED for an MPPS that was never created is refused"""
    need(ctx, "worklist_ok")
    unknown = generate_uid()
    status = dicom.mpps_set(unknown, "COMPLETED")
    record["mpps_sop_instance_uid"] = unknown
    record["n_set_status"] = status_name(status)
    assert status == PROCESSING_FAILURE, f"Expected Processing Failure, got {status_name(status)}"
    logs.wait_for("mwl", rf"\[MPPS\] SetCompleted: no procedure in progress for SOPInstanceUID={re.escape(unknown)}", timeout=15)
    logs.assert_absent("mwl", r"MPPS webhook sent to CARE")


def test_n_set_with_invalid_status_is_refused(dicom, ctx, logs, record):
    """N-SET with a status other than COMPLETED or DISCONTINUED is refused as invalid"""
    need(ctx, "worklist_ok")
    uid = generate_uid()
    status = dicom.mpps_set(uid, "PAUSED")
    record["status_sent"] = "PAUSED"
    record["n_set_status"] = status_name(status)
    assert status == INVALID_ATTRIBUTE_VALUE, f"Expected Invalid Attribute Value, got {status_name(status)}"
    logs.wait_for("mwl", r"\[MPPS\]\[N-SET\] Rejected status 'PAUSED'", timeout=15)
