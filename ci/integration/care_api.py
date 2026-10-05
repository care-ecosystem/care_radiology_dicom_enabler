"""Thin client for the CARE core and care_radiology endpoints the suite needs.

Two credentials are in play, as in production:
  * a user JWT (POST /api/v1/auth/login/) for core EMR calls - creating service requests and
    reading them back;
  * the plugin's static key (CARE_RADIOLOGY_WEBHOOK_SECRET) for the endpoints the enabler itself
    calls - worklist, webhooks and upload-dicom-external.
"""
import time

import requests

# Every HTTP call is appended here so the report can show what the suite did against CARE.
# conftest swaps in a fresh list per test.
CALL_LOG = []


class CareApiError(AssertionError):
    pass


class CareApi:
    def __init__(self, cfg):
        self.cfg = cfg
        self.session = requests.Session()
        self.session.headers["Accept"] = "application/json"
        self._jwt = None

    # -- plumbing ----------------------------------------------------------------------------
    def _request(self, method, path, *, auth="jwt", expected=None, timeout=60, **kwargs):
        url = path if path.startswith("http") else f"{self.cfg.base_url}{path}"
        headers = kwargs.pop("headers", {})
        if auth == "jwt":
            headers["Authorization"] = f"Bearer {self.jwt}"
        elif auth == "key":
            headers["Authorization"] = self.cfg.api_token
        started = time.monotonic()
        response = self.session.request(method, url, headers=headers, timeout=timeout, **kwargs)
        elapsed_ms = int((time.monotonic() - started) * 1000)
        CALL_LOG.append({
            "method": method,
            "path": url.replace(self.cfg.base_url, ""),
            "status": response.status_code,
            "ms": elapsed_ms,
        })
        if expected is not None:
            codes = (expected,) if isinstance(expected, int) else tuple(expected)
            if response.status_code not in codes:
                raise CareApiError(
                    f"{method} {url} returned {response.status_code}, expected {codes}: {response.text[:800]}"
                )
        return response

    def _json(self, method, path, **kwargs):
        kwargs.setdefault("expected", (200, 201))
        return self._request(method, path, **kwargs).json()

    @property
    def jwt(self):
        if self._jwt is None:
            self.login()
        return self._jwt

    def login(self):
        r = self._request(
            "POST", "/api/v1/auth/login/", auth=None, expected=200,
            json={"username": self.cfg.username, "password": self.cfg.password},
        )
        body = r.json()
        if "access" not in body:
            raise CareApiError("Login did not return an access token (MFA-enabled users are not supported)")
        self._jwt = body["access"]

    def _paginate(self, path, params=None, limit=100):
        params = dict(params or {})
        params["limit"] = limit
        offset = 0
        while True:
            params["offset"] = offset
            page = self._json("GET", path, params=params)
            results = page.get("results", page if isinstance(page, list) else [])
            yield from results
            offset += len(results)
            if not results or not page.get("next"):
                return

    # -- core EMR ----------------------------------------------------------------------------
    @property
    def _facility_path(self):
        return f"/api/v1/facility/{self.cfg.facility_id}"

    def get_facility(self):
        return self._json("GET", f"{self._facility_path}/")

    def get_patient(self):
        return self._json("GET", f"/api/v1/patient/{self.cfg.patient_id}/")

    def resolve_encounter(self):
        """Use CARE_ENCOUNTER_ID if given, else an in-progress encounter of the patient at the
        facility, else create one. Returns (encounter, created)."""
        if self.cfg.encounter_id:
            return self._json("GET", f"/api/v1/encounter/{self.cfg.encounter_id}/"), False
        for enc in self._paginate("/api/v1/encounter/", {
            "facility": self.cfg.facility_id,
            "patient_filter": self.cfg.patient_id,
            "status": "in_progress",
        }):
            return enc, False
        enc = self._json("POST", "/api/v1/encounter/", json={
            "patient": self.cfg.patient_id,
            "facility": self.cfg.facility_id,
            "status": "in_progress",
            "encounter_class": "amb",
            "priority": "routine",
            "organizations": [],
            "period": {},
        })
        return enc, True

    def imaging_activity_definitions(self):
        return list(self._paginate(f"{self._facility_path}/activity_definition/", {
            "classification": "imaging", "status": "active",
        }))

    def get_activity_definition(self, slug):
        return self._json("GET", f"{self._facility_path}/activity_definition/{slug}/")

    def devices_at_location(self, location_id):
        return list(self._paginate(f"{self._facility_path}/device/", {"current_location": location_id}))

    def facility_tag_config(self, display):
        """The facility-level tag whose display equals display exactly - the lookup the status
        webhook performs (display filter is icontains, so the exact match is done here)."""
        for tag in self._paginate("/api/v1/tag_config/", {
            "facility": self.cfg.facility_id, "facility_only": "true", "display": display,
            "resource": "service_request",
        }):
            if tag.get("display") == display:
                return tag
        return None

    def create_service_request(self, ad_slug, encounter_id, note):
        return self._json("POST", f"{self._facility_path}/service_request/apply_activity_definition/", json={
            "activity_definition": ad_slug,
            "encounter": encounter_id,
            "service_request": {
                "status": "active",
                "category": "imaging",
                "intent": "order",
                "priority": "routine",
                "note": note,
            },
        })

    def get_service_request(self, sr_id):
        return self._json("GET", f"{self._facility_path}/service_request/{sr_id}/")

    def cancel_service_request(self, sr_id):
        return self._request(
            "POST", f"{self._facility_path}/service_request/{sr_id}/cancel/",
            json={"status": "revoked"}, expected=(200, 204, 400),
        )

    # -- care_radiology ----------------------------------------------------------------------
    def radiology_service_request(self, sr_id):
        r = self._request("GET", f"/api/care_radiology/radiology_service_request/{sr_id}/")
        return r.json() if r.status_code == 200 else None

    def studies_for_service_request(self, sr_id):
        return self._json("GET", "/api/care_radiology/dicom/studies/", params={"serviceRequestId": sr_id})

    def worklist(self, from_dt, to_dt, modality=None):
        params = {"facility": self.cfg.facility_id, "from": from_dt, "to": to_dt}
        if modality:
            params["modality"] = modality
        return self._json("GET", "/api/care_radiology/dicom/worklist/", auth="key", params=params)

    def upload_external(self, filename, content, patient_id):
        return self._request(
            "POST", "/api/care_radiology/dicom/upload-dicom-external/", auth="key", timeout=600,
            files={"file": (filename, content, "application/dicom")},
            data={"patient_id": patient_id},
        )


def poll(fn, *, timeout, interval=3, description="condition"):
    """Call fn until it returns a truthy value or timeout seconds pass. Returns the value."""
    deadline = time.monotonic() + timeout
    last_error = None
    while True:
        try:
            value = fn()
            if value:
                return value
        except Exception as exc:  # noqa: BLE001 - surfaced below if the deadline passes
            last_error = exc
        if time.monotonic() >= deadline:
            detail = f" (last error: {last_error})" if last_error else ""
            raise AssertionError(f"Timed out after {timeout}s waiting for {description}{detail}")
        time.sleep(interval)
