"""Service requests: created from the facility's imaging activity definitions

Creates the service requests the rest of the suite works on, through apply_activity_definition
exactly as the CARE UI does, and waits for care_radiology to assign each an accession number.

Roles:
  primary     - targets the configured device; full MPPS + small-file upload flow
  discontinue - targets the configured device; MPPS discontinued and refusal tests
  large       - targets the configured device; large-file upload
  other       - an imaging AD NOT served by the configured device; must stay out of its worklist
"""
import datetime as dt

from care_api import poll
from conftest import TARGETED_ROLES, need


def test_create_service_requests(cfg, api, ctx, record):
    """Create service requests: three for the configured device, one for another imaging device"""
    targeted, encounter_id = need(ctx, "targeted_ads", "encounter_id")
    others = ctx.get("other_ads") or []
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")

    plan = [(role, targeted[i % len(targeted)], True) for i, role in enumerate(TARGETED_ROLES)]
    if others:
        plan.append(("other", others[0], False))
    else:
        record["other"] = "no imaging activity definition outside the configured device - negative worklist check will be skipped"

    for role, ad, is_target in plan:
        created = api.create_service_request(ad["slug"], encounter_id, f"DICOM enabler CI {stamp} [{role}]")
        sr_id = created["id"]
        ctx["created_sr_ids"].append(sr_id)
        ctx["service_requests"][role] = {
            "id": sr_id,
            "activity_definition": ad["slug"],
            "title": ad.get("title"),
            "targets_device": is_target,
        }
        record[role] = f"{sr_id} - {ad.get('title')} [{ad['slug']}]"
    ctx["service_requests_created"] = True


def _accession_for(api, cfg, sr_id):
    """Accession number from the radiology record, falling back to the worklist API meta."""
    rsr = api.radiology_service_request(sr_id)
    if rsr and rsr.get("accession_number"):
        return rsr["accession_number"]
    start = (dt.datetime.now() - dt.timedelta(hours=2)).strftime("%Y-%m-%d %H:%M:%S")
    end = (dt.datetime.now() + dt.timedelta(minutes=5)).strftime("%Y-%m-%d %H:%M:%S")
    for result in api.worklist(start, end).get("results", []):
        if result["service_request"]["id"] == sr_id:
            return (result["service_request"].get("meta") or {}).get("accession_number")
    return None


def test_accession_numbers_assigned(cfg, api, ctx, record):
    """care_radiology assigns an accession number to every new service request
    The number is written by a Celery task after the SR is saved, so this polls for it."""
    need(ctx, "service_requests_created")
    for role, entry in ctx["service_requests"].items():
        accession = poll(lambda: _accession_for(api, cfg, entry["id"]), timeout=cfg.timeout_s,
                         description=f"accession number on {role} service request {entry['id']}")
        entry["accession_number"] = accession
        record[role] = f"SR {entry['id']} -> accession {accession}"
