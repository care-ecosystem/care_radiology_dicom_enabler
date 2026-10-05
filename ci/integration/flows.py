"""Multi-step flows shared by more than one test module."""
import io
import re
import time

from pydicom import dcmwrite

from checks import wait_for_linked_study
from dicom_client import SUCCESS, build_ct_image, status_name


def encoded_size(ds):
    buffer = io.BytesIO()
    dcmwrite(buffer, ds, enforce_file_format=True)
    return buffer.tell()


def store_and_verify_upload(cfg, api, dicom, logs, record, entry, target_bytes, upload_timeout):
    """C-STORE an image for entry's worklist item, then follow it through the Store SCP, the Store
    SCU upload to CARE and the study link. Shared by the small and large file tests."""
    item = entry["worklist_item"]
    ds = build_ct_image(item, target_bytes=target_bytes)
    size = encoded_size(ds)
    record["service_request_id"] = entry["id"]
    record["accession_number"] = entry["accession_number"]
    record["patient_id_in_file"] = ds.PatientID
    record["study_instance_uid"] = ds.StudyInstanceUID
    record["sop_instance_uid"] = ds.SOPInstanceUID
    record["file_size_bytes"] = size
    record["file_size"] = f"{size / 1024 / 1024:.2f} MB" if size > 1024 * 1024 else f"{size / 1024:.1f} KB"

    status, seconds = dicom.store(ds)
    stored_at = time.monotonic()
    record["c_store_status"] = status_name(status)
    record["c_store_seconds"] = seconds
    assert status == SUCCESS, f"C-STORE returned {status_name(status)}"

    sop = re.escape(ds.SOPInstanceUID)
    logs.wait_for("store", rf"Instance UID: {sop}", timeout=60)
    store_log = logs.text("store")
    assert f"Accession Number: {entry['accession_number']}" in store_log, "Store SCP did not log the file's accession number"
    assert "Database Update Successful" in store_log, "Store SCP did not record the study in its database"
    saved = logs.wait_for("store", rf"File Path: (.*{sop}\.dcm)", timeout=15).group(1)
    record["saved_to"] = saved

    uploaded = logs.wait_for("scu", rf"Upload succeeded \((\d+)\) for .*{sop}\.dcm", timeout=upload_timeout)
    record["care_upload_http_status"] = int(uploaded.group(1))
    record["upload_seconds_after_store"] = round(time.monotonic() - stored_at, 1)
    webhook = logs.wait_for("scu", r"Webhook (succeeded|failed) \((\d+)\)", timeout=60)
    record["study_webhook"] = f"{webhook.group(1)} ({webhook.group(2)})"
    assert webhook.group(1) == "succeeded", f"Study webhook failed: {logs.text('scu')[-800:]}"
    logs.wait_for("scu", rf"Deleted local file: .*{sop}\.dcm", timeout=30)

    wait_for_linked_study(api, cfg, entry["id"], ds.StudyInstanceUID, record, timeout=max(cfg.timeout_s, 180))
    return ds
