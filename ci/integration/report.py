"""Render results.json into report.html (self-contained) and summary.md (GitHub step summary).

Usage: python report.py <report_dir>
"""
import html
import json
import sys
from pathlib import Path

ICON = {"passed": "✅", "failed": "❌", "skipped": "⏭️"}
SERVICE_TITLES = {
    "mwl": "MWL SCP and CARE worklist fetch (ModalitySCP)",
    "store": "Store SCP (StoreSCP)",
    "scu": "Store SCU / upload (StoreSCU)",
    "other": "Other",
}


def _value(v):
    if isinstance(v, (list, tuple)):
        return "<br>".join(html.escape(str(i)) for i in v) or "—"
    return html.escape(str(v))


def render_html(data):
    run, tests = data["run"], data["tests"]
    counts = {k: sum(1 for t in tests if t["outcome"] == k) for k in ("passed", "failed", "skipped")}
    overall = "failed" if counts["failed"] else "passed"

    out = [f"""<!doctype html><html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>DICOM Enabler Integration Report</title>
<style>
:root {{ --bg:#fff; --fg:#1f2328; --muted:#59636e; --line:#d1d9e0; --card:#f6f8fa;
         --pass:#1a7f37; --fail:#cf222e; --skip:#9a6700; }}
@media (prefers-color-scheme: dark) {{ :root {{ --bg:#0d1117; --fg:#e6edf3; --muted:#9198a1;
         --line:#3d444d; --card:#151b23; --pass:#3fb950; --fail:#f85149; --skip:#d29922; }} }}
body {{ background:var(--bg); color:var(--fg); font:14px/1.5 system-ui,sans-serif; margin:0 auto;
        max-width:1100px; padding:16px; }}
h1 {{ font-size:22px; margin:0 0 4px; }} h2 {{ font-size:17px; margin:28px 0 8px; }}
.muted {{ color:var(--muted); }} table {{ border-collapse:collapse; width:100%; }}
td,th {{ border-bottom:1px solid var(--line); padding:4px 8px; text-align:left; vertical-align:top;
         overflow-wrap:anywhere; }}
th {{ color:var(--muted); font-weight:600; width:28%; }}
.badge {{ border-radius:10px; color:#fff; font-size:12px; font-weight:600; padding:1px 8px; }}
.passed {{ background:var(--pass); }} .failed {{ background:var(--fail); }} .skipped {{ background:var(--skip); }}
.test {{ background:var(--card); border:1px solid var(--line); border-radius:6px; margin:10px 0; padding:10px 12px; }}
.test.failed {{ border-left:4px solid var(--fail); }} .msg {{ color:var(--fail); white-space:pre-wrap; }}
pre {{ background:var(--bg); border:1px solid var(--line); border-radius:4px; font-size:12px;
       max-height:420px; overflow:auto; padding:8px; white-space:pre-wrap; }}
summary {{ cursor:pointer; }}
</style></head><body>
<h1>DICOM Enabler Integration Report <span class="badge {overall}">{overall.upper()}</span></h1>
<div class="muted">{counts['passed']} passed · {counts['failed']} failed · {counts['skipped']} skipped ·
{html.escape(str(run.get('started', '')))} → {html.escape(str(run.get('finished', '')))}</div>
<h2>Run configuration</h2><table>"""]
    for key, value in run.items():
        out.append(f"<tr><th>{html.escape(key)}</th><td>{_value(value)}</td></tr>")
    out.append("</table>")

    group = None
    for t in tests:
        if t["group"] != group:
            group = t["group"]
            out.append(f"<h2>{html.escape(group)}</h2>")
        out.append(f'<div class="test {t["outcome"]}"><div><span class="badge {t["outcome"]}">{t["outcome"]}</span> '
                   f'<b>{html.escape(t["title"])}</b> <span class="muted">{t["duration"]}s</span></div>')
        if t["description"]:
            out.append(f'<div class="muted">{html.escape(t["description"])}</div>')
        if t["message"]:
            out.append(f'<pre class="msg">{html.escape(t["message"])}</pre>')
        if t["details"]:
            out.append("<table>")
            for key, value in t["details"].items():
                out.append(f"<tr><th>{html.escape(key)}</th><td>{_value(value)}</td></tr>")
            out.append("</table>")
        if t["http"]:
            rows = "".join(f"<tr><td>{c['method']}</td><td>{html.escape(c['path'])}</td><td>{c['status']}</td><td>{c['ms']} ms</td></tr>"
                           for c in t["http"])
            out.append(f"<details><summary>CARE API calls ({len(t['http'])})</summary><table>{rows}</table></details>")
        for service, text in t["logs"].items():
            lines = text.count("\n")
            out.append(f"<details><summary>{html.escape(SERVICE_TITLES.get(service, service))} log ({lines} lines)</summary>"
                       f"<pre>{html.escape(text)}</pre></details>")
        out.append("</div>")
    out.append("</body></html>")
    return "\n".join(out)


def _md(text):
    return str(text).replace("|", "\\|").replace("\n", " ")


def _reason(message):
    # First line only: assertion messages carry pytest's rewritten expression on the following lines.
    line = next((l.strip() for l in str(message).splitlines() if l.strip()), "")
    return line if len(line) <= 200 else line[:197] + "..."


def render_markdown(data):
    tests = data["tests"]
    counts = {k: sum(1 for t in tests if t["outcome"] == k) for k in ("passed", "failed", "skipped")}
    with_reason = counts["failed"] or counts["skipped"]
    out = [
        f"## DICOM Enabler integration — {'❌ FAILED' if counts['failed'] else '✅ PASSED'}",
        f"{counts['passed']} passed · {counts['failed']} failed · {counts['skipped']} skipped",
        "",
        "| | Test | Reason |" if with_reason else "| | Test |",
        "|---|---|---|" if with_reason else "|---|---|",
    ]
    for t in tests:
        row = f"| {ICON.get(t['outcome'], '')} | {_md(t['title'])} |"
        if with_reason:
            row += f" {_md(_reason(t['message'])) if t['outcome'] != 'passed' else ''} |"
        out.append(row)
    out.append("")
    out.append("Per-test details and service logs: `report.html` in the run artifacts.")
    return "\n".join(out)


def main():
    report_dir = Path(sys.argv[1] if len(sys.argv) > 1 else "test-results")
    data = json.loads((report_dir / "results.json").read_text(encoding="utf-8"))
    (report_dir / "report.html").write_text(render_html(data), encoding="utf-8")
    (report_dir / "summary.md").write_text(render_markdown(data), encoding="utf-8")
    print(f"Wrote {report_dir / 'report.html'} and {report_dir / 'summary.md'}")


if __name__ == "__main__":
    main()
