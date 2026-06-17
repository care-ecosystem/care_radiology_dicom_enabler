"""Minimal mock of the CARE backend for CI integration testing.

Serves the two endpoints the DICOM Enabler services call:
  GET  /api/plugin/care_radiology/dicom/worklist/  -> worklist JSON
  POST /api/plugin/care_radiology/dicom/upload/    -> upload ack
  POST /api/token/  (and /login-api)               -> fake JWT
"""
import json
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer

WORKLIST_RESPONSE = json.dumps({
    "status": "success",
    "results": [{
        "service_request": {
            "id": "ci-sr-id",
            "name": "CT Chest CI Test",
            "date": "2026-06-10T10:00:00Z"
        },
        "facility": {
            "id": "ci-fac-id",
            "name": "CI Test Hospital"
        },
        "patient": {
            "name": "CI^TestPatient",
            "address": "CI Address",
            "phone_number": "+0000000000",
            "gender": "M",
            "age": 30
        }
    }]
})


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
                "message": "DICOM uploaded successfully"
            })
            self._send(201, body)
        else:
            body = json.dumps({"access": "mock-ci-jwt-token", "refresh": "mock-ci-refresh"})
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
