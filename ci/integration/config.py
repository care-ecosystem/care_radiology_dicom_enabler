"""Suite configuration, read entirely from environment variables.

Nothing here is specific to one CARE instance: point the same suite at any server, facility,
patient or user by changing the environment (the workflow maps GitHub Environment secrets /
variables and workflow_dispatch inputs onto these names).
"""
import os
from dataclasses import dataclass, field
from pathlib import Path


def _env(name, default=None, required=False):
    value = os.environ.get(name, "")
    value = value.strip() if value else ""
    if not value:
        if required:
            raise RuntimeError(f"Required environment variable {name} is not set")
        return default
    return value


def _int(name, default):
    return int(_env(name, str(default)))


def _bool(name, default):
    return _env(name, "true" if default else "false").lower() in ("1", "true", "yes", "on")


@dataclass
class Config:
    # CARE server
    base_url: str = field(default_factory=lambda: _env("CARE_BASE_URL", required=True).rstrip("/"))
    api_token: str = field(default_factory=lambda: _env("CARE_API_TOKEN", required=True), repr=False)
    username: str = field(default_factory=lambda: _env("CARE_USERNAME", required=True))
    password: str = field(default_factory=lambda: _env("CARE_PASSWORD", required=True), repr=False)
    facility_id: str = field(default_factory=lambda: _env("CARE_FACILITY_ID", required=True))
    patient_id: str = field(default_factory=lambda: _env("CARE_PATIENT_ID", required=True))
    # Optional: reuse a specific encounter; otherwise an active one is found or created.
    encounter_id: str = field(default_factory=lambda: _env("CARE_ENCOUNTER_ID"))
    # Device the enabler serves: Device.registered_name in CARE, and the worklist modality filter.
    modality: str = field(default_factory=lambda: _env("CARE_MODALITY", "CT"))
    # Optional comma-separated activity definition slugs to restrict which imaging ADs are used.
    activity_definitions: str = field(default_factory=lambda: _env("CARE_ACTIVITY_DEFINITIONS", ""))

    # MPPS study_status strings - must match the enabler App.config and the facility tag configs.
    status_started: str = field(default_factory=lambda: _env("MPPS_STATUS_STARTED", "Scan Started"))
    status_completed: str = field(default_factory=lambda: _env("MPPS_STATUS_COMPLETED", "Scan Completed"))
    status_discontinued: str = field(default_factory=lambda: _env("MPPS_STATUS_DISCONTINUED", "Scan Cancelled"))

    # Enabler deployment under test
    enabler_dir: Path = field(default_factory=lambda: Path(_env("ENABLER_DIR", "bin/Release")).resolve())
    host: str = field(default_factory=lambda: _env("ENABLER_HOST", "127.0.0.1"))
    mwl_port: int = field(default_factory=lambda: _int("MWL_PORT", 2008))
    mwl_aet: str = field(default_factory=lambda: _env("MWL_AET", "MODALITYSCP"))
    store_port: int = field(default_factory=lambda: _int("STORE_PORT", 2007))
    store_aet: str = field(default_factory=lambda: _env("STORE_AET", "STORAGESCP"))
    calling_aet: str = field(default_factory=lambda: _env("CALLING_AET", "CARECITEST"))

    # Behaviour
    large_file_mb: int = field(default_factory=lambda: _int("LARGE_FILE_MB", 40))
    timeout_s: int = field(default_factory=lambda: _int("STEP_TIMEOUT_SECONDS", 120))
    cleanup: bool = field(default_factory=lambda: _bool("CLEANUP_SERVICE_REQUESTS", True))
    report_dir: Path = field(default_factory=lambda: Path(_env("REPORT_DIR", "test-results")).resolve())

    @property
    def log_dir(self) -> Path:
        return self.enabler_dir / "logs"

    @property
    def scp_dir(self) -> Path:
        return self.enabler_dir / "SCP"

    @property
    def radiology_status_tracks_mpps(self):
        """care_radiology moves its own record to IN_PROGRESS / CANCELLED only for its constants
        SCAN_STARTED / DISCONTINUED; any other study_status just adds the matching tag."""
        return (self.status_started, self.status_discontinued) == ("SCAN_STARTED", "DISCONTINUED")

    @property
    def secrets(self):
        """Values that must never appear in the report."""
        return [s for s in (self.api_token, self.password) if s]
