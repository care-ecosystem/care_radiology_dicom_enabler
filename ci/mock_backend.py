"""Minimal mock of the CARE backend for CI integration testing.

Serves the two endpoints the DICOM Enabler services call:
  GET  /api/care_radiology/facility/<id>/external/worklist/  -> worklist JSON
  POST /api/care_radiology/facility/<id>/external/imaging_study/upload/  -> upload ack
  PATCH /api/care_radiology/facility/<id>/external/imaging_study/status/  -> scan status ack
  POST /api/token/  (and /login-api)               -> fake JWT
"""
import json
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer

WORKLIST_RESPONSE = json.dumps({
    "count": 1,
    "status": "success",
    "results": [{
        "service_request": {
            "id": "ci-sr-id",
            "name": "CT Chest CI Test",
            "description": "CT of chest",
            "body_site": "Chest",
            "procedure_id": "169069000",
            "priority": "routine",
            "technician_instruction": None,
            "patient_instruction": None,
            "date": "2026-06-10T10:00:00Z",
            "accession_number": "CI0000000001"
        },
        "locations": [{
            "id": "ci-location-id",
            "name": "CI Radiology",
            "devices": [{"id": "ci-device-id", "name": "CT"}]
        }],
        "facility": {
            "id": "ci-fac-id",
            "name": "CI Test Hospital"
        },
        "patient": {
            "id": "ci-patient-id",
            "name": "CI^TestPatient",
            "gender": "M",
            "age": 30,
            "patient_uhid": None
        }
    }]
})


STUDY_STATUSES = ("IN_PROGRESS", "COMPLETED", "CANCELLED")


class MockHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        if "worklist" in self.path:
            body = WORKLIST_RESPONSE
        else:
            body = '{"status":"ok"}'
        self._send(200, body)

    def do_POST(self):
        length = int(self.headers.get("Content-Length", 0))
        self.rfile.read(length)
        if "upload" in self.path:
            body = json.dumps({
                "status": "success",
                "study_uid": "1.2.999.9.9.9",
                "message": "DICOM files uploaded to DCM4CHE successfully",
                "service_request_id": "ci-sr-id"
            })
            self._send(201, body)
        else:
            body = json.dumps({"access": "mock-ci-jwt-token", "refresh": "mock-ci-refresh"})
            self._send(200, body)

    def do_PATCH(self):
        length = int(self.headers.get("Content-Length", 0))
        payload = json.loads(self.rfile.read(length) or b"{}")
        # The scan status API only accepts these study_status values
        if payload.get("study_status") not in STUDY_STATUSES:
            self._send(400, json.dumps({"study_status": [f"\"{payload.get('study_status')}\" is not a valid choice."]}))
            return
        body = json.dumps({
            "detail": "MPPS status processed successfully",
            "service_request_id": payload.get("service_request_id"),
            "study_status": payload.get("study_status"),
        })
        self._send(200, body)

    def _send(self, code, body):
        encoded = body.encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(encoded)))
        self.end_headers()
        self.wfile.write(encoded)

    def log_message(self, fmt, *args):
        print(f"[mock] {fmt % args}", flush=True)


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 9000
    print(f"Mock CARE backend starting on http://localhost:{port}", flush=True)
    HTTPServer(("localhost", port), MockHandler).serve_forever()
