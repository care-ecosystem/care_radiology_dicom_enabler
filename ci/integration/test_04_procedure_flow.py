"""Procedure flow: MPPS started -> small DICOM upload -> MPPS completed (primary SR)

The happy path a modality drives for one service request from the device worklist:
  1. MPPS N-CREATE IN PROGRESS  -> enabler sends the 'started' status webhook to CARE
  2. C-STORE a small image      -> Store SCP saves it into SCP/
  3. Store SCU picks it up       -> uploads to CARE and links the study to the SR by accession
  4. MPPS N-SET COMPLETED        -> enabler sends the 'completed' status webhook to CARE
"""
import re

from checks import wait_for_mpps_radiology_status, wait_for_radiology_status, wait_for_tag
from conftest import need, sr
from dicom_client import SUCCESS, status_name
from flows import store_and_verify_upload


def test_mpps_in_progress(cfg, api, dicom, ctx, logs, record):
    """MPPS N-CREATE (IN PROGRESS) is accepted and CARE marks the SR as scan started"""
    need(ctx, "worklist_ok")
    entry = sr(ctx, "primary")
    item = entry["worklist_item"]
    status, mpps_uid = dicom.mpps_in_progress(item)
    record["service_request_id"] = entry["id"]
    record["accession_number"] = entry["accession_number"]
    record["mpps_sop_instance_uid"] = mpps_uid
    record["n_create_status"] = status_name(status)
    assert status == SUCCESS, f"N-CREATE returned {status_name(status)}"
    entry["mpps_uid"] = mpps_uid

    logs.wait_for("mwl", rf"\[MPPS\] SetInProgress: matched ServiceRequestId={entry['id']}", timeout=15)
    hook = logs.wait_for("mwl", rf"\[MPPS\] MPPS webhook (sent to CARE|failed|skipped): {re.escape(cfg.status_started)}.*", timeout=60)
    record["status_webhook"] = hook.group(0)
    assert hook.group(1) == "sent to CARE", hook.group(0)
    wait_for_tag(api, cfg, entry["id"], cfg.status_started, record)
    wait_for_mpps_radiology_status(api, cfg, entry["id"], "IN_PROGRESS", record)


def test_store_small_file_and_upload_to_care(cfg, api, dicom, ctx, logs, record):
    """Small DICOM file is stored, pushed to CARE and linked to the service request"""
    entry = sr(ctx, "primary")
    need(entry, "mpps_uid")
    ds = store_and_verify_upload(cfg, api, dicom, logs, record, entry, target_bytes=8 * 1024,
                                 upload_timeout=cfg.timeout_s)
    entry["study_uid"] = ds.StudyInstanceUID
    entry["series_uid"] = ds.SeriesInstanceUID
    entry["sop_uid"] = ds.SOPInstanceUID
    wait_for_radiology_status(api, cfg, entry["id"], "COMPLETED", record)


def test_mpps_completed(cfg, api, dicom, ctx, logs, record):
    """MPPS N-SET (COMPLETED) is accepted and CARE marks the SR as scan completed"""
    entry = sr(ctx, "primary")
    need(entry, "mpps_uid", "sop_uid")
    status = dicom.mpps_set(entry["mpps_uid"], "COMPLETED", entry["series_uid"], [entry["sop_uid"]])
    record["service_request_id"] = entry["id"]
    record["accession_number"] = entry["accession_number"]
    record["mpps_sop_instance_uid"] = entry["mpps_uid"]
    record["n_set_status"] = status_name(status)
    assert status == SUCCESS, f"N-SET COMPLETED returned {status_name(status)}"

    logs.wait_for("mwl", rf"\[MPPS\]\[N-SET\] COMPLETED result=True for SOPInstanceUID={re.escape(entry['mpps_uid'])} \(1 referenced", timeout=15)
    hook = logs.wait_for("mwl", rf"\[MPPS\] MPPS webhook (sent to CARE|failed|skipped): {re.escape(cfg.status_completed)}.*", timeout=60)
    record["status_webhook"] = hook.group(0)
    assert hook.group(1) == "sent to CARE", hook.group(0)
    tags = wait_for_tag(api, cfg, entry["id"], cfg.status_completed, record)
    assert cfg.status_started in tags, "The started tag was lost when the completed tag was added"
    record["outcome"] = "SR went started -> images linked -> completed"
