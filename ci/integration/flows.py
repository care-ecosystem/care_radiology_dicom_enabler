"""Multi-step flows shared by more than one test module."""
import io
import re
import time

from pydicom import dcmwrite

from checks import wait_for_linked_study, wait_for_tag
from dicom_client import SUCCESS, build_ct_image, status_name


def encoded_size(ds):
    buffer = io.BytesIO()
    dcmwrite(buffer, ds, enforce_file_format=True)
    return buffer.tell()


def wait_for_scan_status(cfg, logs, entry, status, timeout, service="scu"):
    """Wait for the Store SCU (or, for Scan Started on MPPS IN PROGRESS, the MWL service) to send
    study_status for entry's service request and assert CARE accepted it."""
    sent = logs.wait_for(
        service, rf"Scan status (sent to CARE|failed|could not connect to CARE): {re.escape(status)}\b.*?service_request {re.escape(entry['id'])}",
        timeout=timeout)
    assert sent.group(1) == "sent to CARE", sent.group(0)
    return sent.group(0)


def store_and_verify_upload(cfg, api, dicom, logs, record, entry, target_bytes, upload_timeout, started_by_mpps=False):
    """C-STORE an image for entry's worklist item, then follow it through the Store SCP, the Store
    SCU's Scan Started, upload to CARE, study link and Scan Completed. Shared by the small and large
    file tests, which each use their own service request. started_by_mpps: Scan Started was already
    sent by the MWL service on MPPS IN PROGRESS, so the Store SCU must not send it again."""
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

    if not started_by_mpps:
        record["scan_started"] = wait_for_scan_status(cfg, logs, entry, cfg.status_started, timeout=upload_timeout)
    wait_for_tag(api, cfg, entry["id"], cfg.status_started, record)
    uploaded = logs.wait_for("scu", rf"Upload succeeded \((\d+)\) for .*{sop}\.dcm", timeout=upload_timeout)
    record["care_upload_http_status"] = int(uploaded.group(1))
    record["upload_seconds_after_store"] = round(time.monotonic() - stored_at, 1)
    acc = re.escape(entry["accession_number"])
    linked = logs.wait_for(
        "scu", rf"Uploaded (study linked to service_request (\S+) \(AccessionNumber={acc}\)|.*{sop}\.dcm unlinked)", timeout=60)
    record["linked_service_request"] = linked.group(2)
    assert linked.group(2) == entry["id"], \
        f"Upload was not linked to service_request {entry['id']}: {linked.group(0)}"
    logs.wait_for("scu", rf"Deleted local file: .*{sop}\.dcm", timeout=30)

    wait_for_linked_study(api, cfg, entry["id"], ds.StudyInstanceUID, record, timeout=max(cfg.timeout_s, 180))

    # Sent once the study's last file was uploaded and none arrived for scan_completed_wait_s
    record["scan_completed"] = wait_for_scan_status(cfg, logs, entry, cfg.status_completed,
                                                    timeout=cfg.scan_completed_wait_s + 60)
    tags = wait_for_tag(api, cfg, entry["id"], cfg.status_completed, record)
    assert cfg.status_started in tags, "The started tag was lost when the completed tag was added"
    if started_by_mpps:
        logs.assert_absent("scu", rf"Scan status sent to CARE: {re.escape(cfg.status_started)}\b.*?service_request {re.escape(entry['id'])}")
    return ds
