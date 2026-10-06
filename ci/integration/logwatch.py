"""Per-test capture of the enabler service logs.

The services write Serilog files under <enabler>/logs (ModalitySCP*.txt, WorklistItems*.txt,
StoreSCP*.txt, StoreSCU*.txt). A LogWatch remembers each file's size when a test starts, so
slice() returns exactly what was logged during that test - including files created or rolled
mid-test - and wait_for() polls that slice for an expected line.
"""
import re
import time
from pathlib import Path

# Short service names used in the report and in wait_for(service=...).
SERVICES = {
    "mwl": "ModalitySCP",
    "worklist": "WorklistItems",
    "store": "StoreSCP",
    "scu": "StoreSCU",
}


def _service_of(path: Path):
    for key, prefix in SERVICES.items():
        if path.name.startswith(prefix):
            return key
    return "other"


class LogWatch:
    def __init__(self, log_dir: Path):
        self.log_dir = log_dir
        self.offsets = {p: p.stat().st_size for p in self._files()}

    def _files(self):
        if not self.log_dir.exists():
            return []
        return sorted(p for p in self.log_dir.glob("*.txt") if p.is_file())

    def slice(self):
        """{service: text logged since this watch started}."""
        out = {}
        for path in self._files():
            start = self.offsets.get(path, 0)
            try:
                size = path.stat().st_size
                if size < start:  # truncated or replaced
                    start = 0
                if size == start:
                    continue
                with open(path, "rb") as fh:
                    fh.seek(start)
                    text = fh.read().decode("utf-8", errors="replace")
            except OSError:
                continue
            service = _service_of(path)
            out[service] = out.get(service, "") + text
        return out

    def text(self, service):
        return self.slice().get(service, "")

    def wait_for(self, service, pattern, timeout=60, interval=1.0):
        """Wait until pattern (regex) appears in service's log since the watch started.
        Returns the match."""
        regex = re.compile(pattern)
        deadline = time.monotonic() + timeout
        while True:
            match = regex.search(self.text(service))
            if match:
                return match
            if time.monotonic() >= deadline:
                tail = self.text(service)[-1500:] or "(nothing logged)"
                raise AssertionError(
                    f"Expected log line /{pattern}/ in {SERVICES.get(service, service)} log within "
                    f"{timeout}s. Logged during this test:\n{tail}"
                )
            time.sleep(interval)

    def assert_absent(self, service, pattern):
        match = re.search(pattern, self.text(service))
        assert not match, f"Unexpected line in {SERVICES.get(service, service)} log: {match.group(0)}"
