"""Invalid input: malformed and non-DICOM files are rejected

Checks each hop refuses bad input without losing the service:
  Store SCP  - a dataset without a Study Instance UID is refused and nothing is saved
  Store SCU  - a non-DICOM file in the outbound folder is not uploaded
  CARE       - upload-dicom-external rejects a non-DICOM payload
"""
import datetime as dt
import shutil
import time

from conftest import need, sr
from dicom_client import SUCCESS, build_ct_image, status_name


def test_store_scp_rejects_dataset_without_study_uid(cfg, dicom, ctx, logs, record):
    """C-STORE of a dataset without a Study Instance UID is refused and nothing is saved"""
    need(ctx, "worklist_ok")
    entry = sr(ctx, "primary")
    ds = build_ct_image(entry["worklist_item"], target_bytes=1024)
    del ds.StudyInstanceUID
    record["sop_instance_uid"] = ds.SOPInstanceUID

    try:
        status, _ = dicom.store(ds)
    except AssertionError as exc:  # association refused outright is also a rejection
        status = None
        record["association"] = str(exc)
    record["c_store_status"] = status_name(status)
    assert status != SUCCESS, "Store SCP reported success for a dataset without a Study Instance UID"

    time.sleep(3)
    saved = list(cfg.scp_dir.rglob(f"{ds.SOPInstanceUID}.dcm")) if cfg.scp_dir.exists() else []
    assert not saved, f"Store SCP saved the invalid dataset: {saved}"
    logs.assert_absent("store", rf"Instance UID: {ds.SOPInstanceUID}")
    established, echo = dicom.echo(cfg.store_port, cfg.store_aet)
    record["store_scp_after"] = "answering" if established and echo == SUCCESS else "NOT answering"
    assert established and echo == SUCCESS, "Store SCP stopped answering after the invalid C-STORE"


def test_store_scu_does_not_upload_non_dicom_file(cfg, logs, record):
    """A non-DICOM file in the outbound folder is rejected by the Store SCU, not uploaded"""
    folder = cfg.scp_dir / f"ci-invalid-{dt.datetime.now():%Y%m%d%H%M%S}"
    folder.mkdir(parents=True, exist_ok=True)
    junk = folder / "not-a-dicom-file.dcm"
    junk.write_bytes(b"This is plain text pretending to be DICOM.\n" * 64)
    record["file"] = str(junk)
    record["file_size_bytes"] = junk.stat().st_size
    try:
        match = logs.wait_for("scu", r"Upload exception for .*not-a-dicom-file\.dcm: (.*)", timeout=60)
        record["scu_rejection"] = match.group(1).strip()
        logs.assert_absent("scu", r"Upload succeeded \(\d+\) for .*not-a-dicom-file\.dcm")
    finally:
        # The SCU leaves failed files in place and retries them; remove it so it does not linger.
        shutil.rmtree(folder, ignore_errors=True)


def test_care_rejects_non_dicom_upload(cfg, api, record):
    """CARE's upload-dicom-external endpoint rejects a non-DICOM payload"""
    response = api.upload_external("not-a-dicom-file.dcm", b"not dicom at all" * 32, cfg.patient_id)
    record["http_status"] = response.status_code
    record["response"] = response.text[:300]
    assert not response.ok, f"CARE accepted a non-DICOM upload with HTTP {response.status_code}"
    assert response.status_code != 401 and response.status_code != 403, "Static key was rejected - not a file validation result"
