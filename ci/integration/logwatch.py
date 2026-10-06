"""Per-test capture of the enabler service logs.

The services write Serilog files under <enabler>/logs/yyyy-MM-dd (ModalitySCP*.txt,
StoreSCP*.txt, StoreSCU*.txt). Each file rolls at 10 KB and only the newest 3 stay as .txt; older
ones are zipped into that day's archive folder and deleted. A LogWatch remembers each file's size
when a test starts and reads what is appended as it polls, so slice() returns exactly what was
logged during that test - including files created, rolled or archived mid-test - and wait_for()
polls that slice for an expected line.
"""
import re
import time
import zipfile
from pathlib import Path

# Short service names used in the report and in wait_for(service=...).
SERVICES = {
    "mwl": "ModalitySCP",
    "store": "StoreSCP",
    "scu": "StoreSCU",
}


def _service_of(name: str):
    for key, prefix in SERVICES.items():
        if name.startswith(prefix):
            return key
    return "other"


class LogWatch:
    def __init__(self, log_dir: Path, from_start=False):
        """from_start=True reads everything logged so far, archived files included, instead of
        only what is logged after the watch starts."""
        self.log_dir = log_dir
        self.offsets = {} if from_start else {p: p.stat().st_size for p in self._files()}
        self.seen_archives = set() if from_start else set(self._archives())
        self.captured = {}

    def _files(self):
        if not self.log_dir.exists():
            return []
        return sorted(p for p in self.log_dir.rglob("*.txt") if p.is_file())

    def _archives(self):
        if not self.log_dir.exists():
            return []
        return sorted(p for p in self.log_dir.rglob("archive/*.zip") if p.is_file())

    def _capture(self, name, text):
        service = _service_of(name)
        self.captured[service] = self.captured.get(service, "") + text

    def _poll(self):
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
                    data = fh.read()
            except OSError:
                continue
            # Take whole lines only, so a line being written is read in full on a later poll
            end = data.rfind(b"\n") + 1
            if not end:
                continue
            self.offsets[path] = start + end
            self._capture(path.name, data[:end].decode("utf-8", errors="replace"))

        # A rolled file archived since the last poll: take the part not read before it was deleted
        for archive in self._archives():
            if archive in self.seen_archives:
                continue
            try:
                with zipfile.ZipFile(archive) as zf:
                    entries = [(info.filename, zf.read(info)) for info in zf.infolist()]
            except (OSError, zipfile.BadZipFile):
                continue  # still being written; retry on the next poll
            self.seen_archives.add(archive)
            for name, data in entries:
                original = archive.parent.parent / name
                start = self.offsets.pop(original, 0)
                if len(data) > start:
                    self._capture(name, data[start:].decode("utf-8", errors="replace"))

    def slice(self):
        """{service: text logged since this watch started}."""
        self._poll()
        return dict(self.captured)

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
