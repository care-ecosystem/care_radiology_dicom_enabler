"""File handling: large DICOM file end to end (large SR)

The small-file path is covered by the procedure flow; this pushes a file of LARGE_FILE_MB through
the same chain - C-STORE, Store SCP, Store SCU upload to CARE, study link - and records the
timings so regressions in transfer or upload time show up in the report.
"""
from checks import wait_for_radiology_status
from conftest import need, sr
from flows import store_and_verify_upload


def test_large_file_stored_uploaded_and_linked(cfg, api, dicom, ctx, logs, record):
    """Large DICOM file is stored, pushed to CARE and linked to the service request"""
    need(ctx, "worklist_ok")
    entry = sr(ctx, "large")
    need(entry, "worklist_item")
    upload_timeout = max(cfg.timeout_s, 60 + cfg.large_file_mb * 10)
    record["requested_size_mb"] = cfg.large_file_mb
    store_and_verify_upload(cfg, api, dicom, logs, record, entry,
                            target_bytes=cfg.large_file_mb * 1024 * 1024, upload_timeout=upload_timeout)
    size = record["file_size_bytes"]
    assert size >= cfg.large_file_mb * 1024 * 1024 * 0.95, f"Generated file is only {size} bytes"
    wait_for_radiology_status(api, cfg, entry["id"], "COMPLETED", record)
