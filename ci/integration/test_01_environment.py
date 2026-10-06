"""Environment: configured ports, AE titles and CARE preflight

Proves the enabler is listening exactly where its cfg/common.cfg says, answers on its configured
AE titles to modalities in its Server List only, and that the CARE instance, facility, patient, tags and imaging activity definitions
the rest of the suite relies on are reachable with the supplied credentials.
"""
import datetime as dt
import re

from pynetdicom.sop_class import ModalityWorklistInformationFind

from dicom_client import SUCCESS, port_open, status_name
from logwatch import LogWatch

DEFAULT_PORTS = {"mwl": 2008, "store": 2007}


def test_services_listen_on_configured_ports(cfg, record):
    """MWL and Store SCP listen on the configured ports
    When non-default ports are configured, the defaults must not be listening - proving the
    services read cfg/common.cfg rather than a hardcoded port."""
    record["mwl_port"] = cfg.mwl_port
    record["store_port"] = cfg.store_port
    assert port_open(cfg.host, cfg.mwl_port), f"MWL SCP is not listening on configured port {cfg.mwl_port}"
    assert port_open(cfg.host, cfg.store_port), f"Store SCP is not listening on configured port {cfg.store_port}"
    for name, configured in (("mwl", cfg.mwl_port), ("store", cfg.store_port)):
        default = DEFAULT_PORTS[name]
        if configured != default:
            listening = port_open(cfg.host, default, timeout=1)
            record[f"default_{name}_port_{default}"] = "listening" if listening else "closed"
            assert not listening, f"Default port {default} is listening although {name} is configured on {configured}"


def test_service_startup_logged_with_configuration(cfg, record):
    """Each service logged a clean start with the configured port and AE title"""
    full = LogWatch(cfg.log_dir)
    full.offsets = {}  # read the whole files, not just this test's slice
    logs = full.slice()

    mwl = re.search(r"Starting MWL DICOM server on port (\d+), AET=(\S+), backend=(\d+)", logs.get("mwl", ""))
    assert mwl, "MWL service did not log its start-up line"
    record["mwl_startup"] = mwl.group(0)
    assert int(mwl.group(1)) == cfg.mwl_port, f"MWL started on {mwl.group(1)}, configured {cfg.mwl_port}"
    assert mwl.group(2) == cfg.mwl_aet, f"MWL started as AET {mwl.group(2)}, configured {cfg.mwl_aet}"
    assert mwl.group(3) == "2", f"MWL backend is {mwl.group(3)}; the CARE server backend (2) is required"
    assert "Starting MWL SCP Service failed" not in logs.get("mwl", ""), "MWL service logged a start-up failure"

    assert "Store SCP Started Successfully" in logs.get("store", ""), "Store SCP did not log a successful start"
    assert "Error Starting Store SCP Service" not in logs.get("store", ""), "Store SCP logged a start-up failure"
    assert "Store SCU Service Started Successfully" in logs.get("scu", ""), "Store SCU (upload) service did not log a successful start"
    record["store_startup"] = "Store SCP Started Successfully"
    record["scu_startup"] = "Store SCU Service Started Successfully"


def test_c_echo_mwl(cfg, dicom, logs, record):
    """C-ECHO to the MWL SCP on its configured AE title succeeds"""
    established, status = dicom.echo(cfg.mwl_port, cfg.mwl_aet)
    record["association"] = f"{cfg.calling_aet} -> {cfg.mwl_aet}@{cfg.mwl_port}"
    record["status"] = status_name(status)
    assert established, "MWL SCP rejected the association"
    assert status == SUCCESS
    logs.wait_for("mwl", rf"\[C-ECHO\] Request from AE={cfg.calling_aet}", timeout=15)


def test_c_echo_store(cfg, dicom, logs, record):
    """C-ECHO to the Store SCP on its configured AE title succeeds"""
    established, status = dicom.echo(cfg.store_port, cfg.store_aet)
    record["association"] = f"{cfg.calling_aet} -> {cfg.store_aet}@{cfg.store_port}"
    record["status"] = status_name(status)
    assert established, "Store SCP rejected the association"
    assert status == SUCCESS
    logs.wait_for("store", rf"Received verification request from AE {cfg.calling_aet}", timeout=15)


def test_mwl_rejects_wrong_called_aet(cfg, dicom, logs, record):
    """MWL SCP rejects an association addressed to the wrong AE title"""
    assert port_open(cfg.host, cfg.mwl_port), "MWL SCP is not running - a rejection would prove nothing"
    wrong = "NOT" + cfg.mwl_aet[:13]
    established, _ = dicom.echo(cfg.mwl_port, wrong)
    record["called_aet"] = wrong
    record["result"] = "accepted" if established else "rejected"
    assert not established, f"MWL SCP accepted an association for called AE {wrong}"
    logs.wait_for("mwl", rf"\[ASSOC\] Rejected: called AE={wrong}", timeout=15)


UNLISTED_AET = "NOTINSERVERLIST"


def test_mwl_rejects_calling_aet_not_in_server_list(cfg, dicom, logs, record):
    """MWL SCP rejects a modality whose calling AE title is not in the Server List
    With checkserver enabled, only the AE/IP pairs in the Server List may query the worklist or
    send MPPS updates to CARE; the check runs when the association is negotiated."""
    assert port_open(cfg.host, cfg.mwl_port), "MWL SCP is not running - a rejection would prove nothing"
    established, _ = dicom.echo(cfg.mwl_port, cfg.mwl_aet, calling_aet=UNLISTED_AET)
    record["calling_aet"] = UNLISTED_AET
    record["result"] = "accepted" if established else "rejected"
    assert not established, f"MWL SCP accepted calling AE {UNLISTED_AET}, which is not in the Server List"
    record["log"] = logs.wait_for("mwl", rf"\[ASSOC\] Rejected: calling AE={UNLISTED_AET} .*", timeout=15).group(0)


def test_store_rejects_calling_aet_not_in_server_list(cfg, dicom, logs, record):
    """Store SCP rejects a modality whose calling AE title is not in the Server List"""
    assert port_open(cfg.host, cfg.store_port), "Store SCP is not running - a rejection would prove nothing"
    established, _ = dicom.echo(cfg.store_port, cfg.store_aet, calling_aet=UNLISTED_AET)
    record["calling_aet"] = UNLISTED_AET
    record["result"] = "accepted" if established else "rejected"
    assert not established, f"Store SCP accepted calling AE {UNLISTED_AET}, which is not in the Server List"
    record["log"] = logs.wait_for("store", rf"Association Rejected: calling AE {UNLISTED_AET} .*", timeout=15).group(0)


def test_store_rejects_unsupported_sop_class(cfg, dicom, record):
    """Store SCP accepts no presentation context for a non-storage SOP class"""
    assert port_open(cfg.host, cfg.store_port), "Store SCP is not running - a rejection would prove nothing"
    assoc = dicom.associate(cfg.store_port, cfg.store_aet, [ModalityWorklistInformationFind])
    accepted = assoc.accepted_contexts if assoc.is_established else []
    if assoc.is_established:
        assoc.release()
    record["proposed"] = "Modality Worklist Information Model - FIND"
    record["accepted_contexts"] = len(accepted)
    assert not accepted, "Store SCP accepted a worklist FIND context"


def test_care_preflight(cfg, api, ctx, record):
    """CARE instance, facility, patient, status tags and imaging activity definitions are usable
    Logs in as the test user, checks the static key against the worklist API, and works out
    which imaging activity definitions target the configured device: those whose locations hold a
    device registered as the configured modality."""
    api.login()
    facility = api.get_facility()
    api.get_patient()  # fails fast if CARE_PATIENT_ID is wrong or not visible to the user
    record["facility"] = f"{facility.get('name')} ({cfg.facility_id})"
    record["patient_id"] = cfg.patient_id

    encounter, created = api.resolve_encounter()
    ctx["encounter_id"] = encounter["id"]
    record["encounter_id"] = f"{encounter['id']} ({'created' if created else 'existing'})"

    missing = []
    for status in (cfg.status_started, cfg.status_completed, cfg.status_discontinued):
        tag = api.facility_tag_config(status)
        record[f"tag_{status}"] = tag["id"] if tag else "MISSING"
        if not tag:
            missing.append(status)
    assert not missing, (
        f"Facility has no service_request tag config with display {missing}. The status webhook "
        "rejects any study_status without one - create the tags or set MPPS_STATUS_* to match."
    )

    now = dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    api.worklist(now, now)  # raises unless the static key is accepted
    record["static_key"] = "accepted by worklist API"

    wanted = {s.strip() for s in cfg.activity_definitions.split(",") if s.strip()}
    targeted, others = [], []
    for ad in api.imaging_activity_definitions():
        if wanted and ad["slug"] not in wanted and ad.get("slug_config", {}).get("slug_value") not in wanted:
            continue
        detail = api.get_activity_definition(ad["slug"])
        devices = []
        for location in detail.get("locations", []):
            for device in api.devices_at_location(location["id"]):
                devices.append((device.get("registered_name") or "", location.get("name") or location["id"]))
        entry = {"slug": ad["slug"], "title": ad.get("title"), "devices": [f"{n} @ {loc}" for n, loc in devices]}
        is_target = any(name.lower() == cfg.modality.lower() for name, _ in devices)
        (targeted if is_target else others).append(entry)

    record["targeted_activity_definitions"] = [f"{a['title']} [{a['slug']}] devices={a['devices']}" for a in targeted]
    record["other_imaging_activity_definitions"] = [f"{a['title']} [{a['slug']}] devices={a['devices']}" for a in others]
    assert targeted, (
        f"No active imaging activity definition has a location holding a device registered as "
        f"'{cfg.modality}'. Associate a '{cfg.modality}' device with an AD location in facility {cfg.facility_id}."
    )
    ctx["targeted_ads"] = targeted
    ctx["other_ads"] = others
    ctx["preflight_ok"] = True
