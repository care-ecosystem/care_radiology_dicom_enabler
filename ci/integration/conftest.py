"""Shared fixtures and result capture for the enabler integration suite.

Each test gets:
  * logs   - a LogWatch started just before the test, so assertions and the report only see
             what the services logged during that test;
  * record - a dict the test fills with the identifiers worth reporting (SR id, accession
             number, UIDs, statuses, timings), for passing tests as much as failing ones.

State that later tests build on (the facility setup, the service requests created) lives in
the session-scoped ctx dict; a test whose prerequisite is missing is skipped, not failed.
"""
import datetime as dt
import json
import time

import pytest

import care_api
from care_api import CareApi
from config import Config
from dicom_client import DicomClient
from logwatch import LogWatch

RESULTS = []
RUN = {}

# Service request roles that target the configured device (see test_02_service_requests).
TARGETED_ROLES = ("primary", "discontinue", "large")


@pytest.fixture(scope="session")
def cfg():
    return Config()


@pytest.fixture(scope="session")
def api(cfg):
    return CareApi(cfg)


@pytest.fixture(scope="session")
def dicom(cfg):
    return DicomClient(cfg)


@pytest.fixture(scope="session")
def ctx(cfg, api):
    state = {"service_requests": {}, "created_sr_ids": []}
    yield state
    if cfg.cleanup and state["created_sr_ids"]:
        for sr_id in state["created_sr_ids"]:
            try:
                api.cancel_service_request(sr_id)
            except Exception as exc:  # noqa: BLE001 - cleanup must not mask test results
                print(f"cleanup: could not revoke service request {sr_id}: {exc}")
        RUN["cleanup"] = f"revoked {len(state['created_sr_ids'])} service request(s)"
    else:
        RUN["cleanup"] = "skipped - service requests left active"


@pytest.fixture
def logs(cfg, request):
    watch = LogWatch(cfg.log_dir)
    request.node._logwatch = watch
    return watch


@pytest.fixture
def record(request):
    data = {}
    request.node._record = data
    return data


@pytest.fixture(autouse=True)
def _per_test_capture(request, logs, record):
    care_api.CALL_LOG = []
    request.node._calls = care_api.CALL_LOG
    request.node._started = time.time()
    yield


def need(ctx, *keys):
    """Skip the current test unless every ctx key was set by an earlier test."""
    missing = [k for k in keys if not ctx.get(k)]
    if missing:
        pytest.skip(f"prerequisite not available: {', '.join(missing)} (an earlier test failed or was skipped)")
    return [ctx[k] for k in keys]


def sr(ctx, role):
    """The service request created for a role ('primary', 'discontinue', 'large', 'other')."""
    entry = ctx["service_requests"].get(role)
    if not entry or not entry.get("accession_number"):
        pytest.skip(f"no '{role}' service request with an accession number (an earlier test failed or was skipped)")
    return entry


def pytest_sessionstart(session):
    RUN["started"] = dt.datetime.now().isoformat(timespec="seconds")


@pytest.hookimpl(hookwrapper=True)
def pytest_runtest_makereport(item, call):
    outcome = yield
    report = outcome.get_result()
    # One entry per test: the call phase, or setup when the test never got to run.
    if report.when == "call" or (report.when == "setup" and report.outcome != "passed"):
        doc = (item.obj.__doc__ or "").strip()
        title, _, description = doc.partition("\n")
        message = ""
        if report.outcome == "failed":
            message = str(call.excinfo.value) if call.excinfo else report.longreprtext
        elif report.outcome == "skipped" and call.excinfo:
            message = str(call.excinfo.value.msg if hasattr(call.excinfo.value, "msg") else call.excinfo.value)
        watch = getattr(item, "_logwatch", None)
        RESULTS.append({
            "nodeid": item.nodeid,
            "group": item.module.__doc__.strip().splitlines()[0] if item.module.__doc__ else item.module.__name__,
            "title": title.strip() or item.name,
            "description": " ".join(line.strip() for line in description.splitlines() if line.strip()),
            "outcome": report.outcome,
            "duration": round(time.time() - getattr(item, "_started", time.time()), 2),
            "message": message,
            "details": getattr(item, "_record", {}),
            "http": list(getattr(item, "_calls", [])),
            "logs": watch.slice() if watch else {},
        })


def pytest_sessionfinish(session, exitstatus):
    cfg = None
    try:
        cfg = Config()
    except RuntimeError:
        pass
    RUN["finished"] = dt.datetime.now().isoformat(timespec="seconds")
    if cfg:
        RUN.update({
            "care_base_url": cfg.base_url,
            "facility_id": cfg.facility_id,
            "patient_id": cfg.patient_id,
            "username": cfg.username,
            "modality": cfg.modality,
            "mwl": f"{cfg.mwl_aet}@{cfg.host}:{cfg.mwl_port}",
            "store": f"{cfg.store_aet}@{cfg.host}:{cfg.store_port}",
            "calling_aet": cfg.calling_aet,
            "mpps_statuses": [cfg.status_started, cfg.status_completed, cfg.status_discontinued],
        })
        report_dir = cfg.report_dir
        secrets = cfg.secrets
    else:
        from pathlib import Path
        report_dir, secrets = Path("test-results").resolve(), []
    report_dir.mkdir(parents=True, exist_ok=True)
    text = json.dumps({"run": RUN, "tests": RESULTS}, indent=2, default=str)
    for secret in secrets:
        text = text.replace(secret, "***")
    (report_dir / "results.json").write_text(text, encoding="utf-8")
