"""Device worklist: only the configured device's service requests reach the modality

Queries the worklist the way a modality does - DICOM C-FIND to the enabler's MWL SCP - and checks
the enabler fetched it from CARE scoped to the facility and device, then returned exactly the
service requests that target the configured device.

The MWL SCP answers C-FIND from care_worklist, which it refreshes from the CARE worklist API every
worklist_refresh_interval_seconds (30 by default) and when no row matches a C-FIND.
"""
import datetime as dt
import time

from conftest import TARGETED_ROLES, need, sr
from dicom_client import SUCCESS, status_name

# A periodic refresh that ran while the SRs were being created leaves only some of them in
# care_worklist; C-FIND then answers from those rows until the next refresh. Allow two intervals.
WORKLIST_REFRESH_WAIT_S = 75


def _targeted(ctx):
    return {sr(ctx, role)["accession_number"]: role for role in TARGETED_ROLES}


def test_care_worklist_api_scoped_to_device(cfg, api, ctx, record):
    """CARE's worklist API returns the device's service requests and not the other one
    Checks the server-side data the enabler depends on, independently of the enabler."""
    need(ctx, "service_requests_created")
    targeted = _targeted(ctx)
    start = (dt.datetime.now() - dt.timedelta(hours=2)).strftime("%Y-%m-%d %H:%M:%S")
    end = (dt.datetime.now() + dt.timedelta(minutes=5)).strftime("%Y-%m-%d %H:%M:%S")
    results = api.worklist(start, end, modality=cfg.modality).get("results", [])
    ids = {r["service_request"]["id"] for r in results}
    record["returned_service_requests"] = len(results)
    for role in TARGETED_ROLES:
        entry = sr(ctx, role)
        assert entry["id"] in ids, f"CARE worklist for modality {cfg.modality} is missing the {role} SR {entry['id']}"
    other = ctx["service_requests"].get("other")
    if other:
        assert other["id"] not in ids, f"CARE worklist for {cfg.modality} includes the other-device SR {other['id']}"
        record["other_device_sr"] = f"{other['id']} correctly excluded"
    record["targeted_accessions"] = sorted(targeted)


def test_device_worklist_returns_only_relevant_service_requests(cfg, dicom, ctx, logs, record):
    """C-FIND to the MWL SCP returns exactly the configured device's service requests"""
    need(ctx, "service_requests_created")
    targeted = _targeted(ctx)
    deadline = time.monotonic() + WORKLIST_REFRESH_WAIT_S
    attempts = 0
    while True:
        attempts += 1
        status, items = dicom.find_worklist(modality=cfg.modality, scheduled_aet=cfg.calling_aet)
        returned = {item["accession_number"]: item for item in items}
        if status != SUCCESS or set(targeted) <= set(returned) or time.monotonic() >= deadline:
            break
        time.sleep(5)

    record["c_find_attempts"] = attempts
    record["c_find_status"] = status_name(status)
    record["returned"] = sorted(returned)
    record["expected"] = sorted(targeted)
    assert status == SUCCESS, f"C-FIND ended with {status_name(status)}"

    missing = sorted(set(targeted) - set(returned))
    assert not missing, f"Worklist is missing service requests for the device: {missing}"

    other = ctx["service_requests"].get("other")
    if other:
        assert other["accession_number"] not in returned, (
            f"Worklist includes the other-device SR {other['id']} (accession {other['accession_number']})"
        )
        record["other_device_sr"] = f"{other['accession_number']} correctly excluded"

    unexpected = sorted(set(returned) - set(targeted))
    assert not unexpected, (
        f"Worklist returned service requests this run did not create: {unexpected}. The dedicated "
        "facility should hold no other active SRs for this device since the run started."
    )

    for accession, item in returned.items():
        assert item["patient_id"], f"{accession}: worklist item has no PatientID"
        assert item["modality"] == cfg.modality, f"{accession}: modality {item['modality']} != {cfg.modality}"
        assert item["scheduled_aet"] == cfg.calling_aet, (
            f"{accession}: Scheduled Station AE {item['scheduled_aet']} != configured {cfg.calling_aet}"
        )
        assert item["procedure_step_id"], f"{accession}: worklist item has no Scheduled Procedure Step ID"
        sr(ctx, targeted[accession])["worklist_item"] = item
    record["items"] = [
        f"{a}: PatientID={i['patient_id']} SPS={i['procedure_step_id']} RP={i['requested_procedure_id']} '{i['description']}'"
        for a, i in sorted(returned.items())
    ]
    ctx["worklist_ok"] = True

    logs.wait_for("mwl", rf"\[FACILITY\] AE={cfg.calling_aet} resolved to Facility ID={cfg.facility_id}", timeout=15)
    logs.wait_for("mwl", rf"Fetching Records from care_worklist for Facility ID {cfg.facility_id}", timeout=15)
    logs.wait_for("mwl", rf"Successfully fetched \d+ worklist items from care_worklist, {len(returned)} matching the C-FIND", timeout=15)
    logs.wait_for("mwl", rf"C-FIND completed successfully: returned {len(returned)} worklist items", timeout=15)
    # The CARE call comes from this C-FIND when care_worklist had no match, otherwise from the
    # periodic refresh, so allow one refresh interval for it.
    logs.wait_for("mwl", rf"CARE Worklist URL: .*modality={cfg.modality}&.*&facility={cfg.facility_id}", timeout=45)
    logs.wait_for("mwl", r"care_worklist synced: \d+ new row\(s\) inserted", timeout=45)
    logs.assert_absent("mwl", r"CARE worklist API did not report success|"
                              r"Refreshing care_worklist from the CARE worklist API failed")


def test_worklist_filters_other_modality(cfg, dicom, ctx, record):
    """C-FIND for a different modality returns nothing"""
    need(ctx, "worklist_ok")
    status, items = dicom.find_worklist(modality="ZZ", scheduled_aet=cfg.calling_aet)
    record["c_find_status"] = status_name(status)
    record["returned"] = len(items)
    assert status == SUCCESS
    assert not items, f"C-FIND for modality ZZ returned {len(items)} item(s)"
