# Integration & Feature Analysis
## CARE Radiology DICOM Enabler — Device Interaction, Protocol Coverage, and Migration Readiness

> **Read alongside**: `CROSS_PLATFORM_MIGRATION.md` (how to migrate) and `ISSUES.md` (known bugs).  
> This document covers *what* the system does, *what is missing*, and *how the integration pattern extends*.

---

## Table of Contents

1. [Cross-Platform Migration — Gap Assessment](#1-cross-platform-migration--gap-assessment)
2. [How the System Interacts with Lab Devices](#2-how-the-system-interacts-with-lab-devices)
3. [Supported Device Types and Modalities](#3-supported-device-types-and-modalities)
4. [Protocol Coverage — What Works, What Is Missing](#4-protocol-coverage--what-works-what-is-missing)
5. [Core Features Required for Production Readiness](#5-core-features-required-for-production-readiness)
6. [How the CARE Integration Pattern Extends to Other Systems](#6-how-the-care-integration-pattern-extends-to-other-systems)

---

## 1. Cross-Platform Migration — Gap Assessment

The migration guide (`CROSS_PLATFORM_MIGRATION.md`) correctly identifies the technology shift
(.NET Framework → .NET 8, Windows Services → Generic Host). However, several concrete blockers
are not addressed there. **These must be resolved before or during migration, not after.**

### 1.1 Hard Blockers — Migration Will Not Produce a Working System Without Fixing These

| # | File / Location | Issue | Fix Required |
|---|----------------|-------|-------------|
| B1 | `CARE_Auth_Service/PlexusAuthService.cs` | Uses `System.ServiceProcess.ServiceController` to stop sibling services. This is Windows-only and has no cross-platform equivalent. | Replace with a shared `CancellationToken` or an in-process lifecycle coordinator. On Linux, `systemctl` via shell or a signal-based approach is needed if services run as separate processes. |
| B2 | `CARE_DICOM_Enabler.csproj` (WinForms) | WinForms does not run on Linux. The migration guide recommends a web UI but does not define what replaces the service install/start/stop flows. | The management UI must be rebuilt as a REST API + web frontend before the WinForms host can be removed. Until then, the services have no operational interface on Linux. |
| B3 | `Global.cs`, `CStoreSCP.cs`, `WorklistServer.cs` | Multiple places use hardcoded `\` path separators or `C:\`-style roots. | Replace every path construction with `Path.Combine()` and configure `StoragePath` via environment variable (e.g. `DICOM_STORAGE_PATH`). |
| B4 | Several service files | Windows Event Log calls (`EventLog.WriteEntry()`). Silently fail or throw on Linux. | Remove Event Log calls; route all output through Serilog only (already present). |

### 1.2 Stability Bugs — Exist Today, Survive Migration Unchanged

These are documented in `ISSUES.md` but are worth calling out here because they will reproduce on
any platform and block acceptance testing:

| Priority | Bug | Impact |
|----------|-----|--------|
| Critical | C-1: `GetSingleValue<string>(DicomTag.StudyInstanceUID)` throws if tag absent | Any modality that omits the Study UID crashes the C-STORE handler and leaves the association hanging |
| Critical | C-2: `GetString()` without null guard on PatientID, AccessionNumber, Modality | NullReferenceException on incomplete DICOM files — common from older CR/DR equipment |
| Critical | C-4: AE Title directly interpolated into SQL | SQL injection via crafted DICOM AE Title on any platform |
| High | M-1: `async void DicomSCUFile()` | Upload exceptions swallowed silently; failed uploads appear to succeed |
| High | C-3: `ex.InnerException.Message` without null check | Original exception message is lost; support debugging becomes impossible |

### 1.3 Security Gaps — Must Address Before Any Cloud or Internet-Facing Deployment

| Gap | Location | Remediation |
|----|----------|-------------|
| Hardcoded encryption key | `CARE.DAL/EncKey.cs`: `encdeKey = "3DEA271411CD4AA0AC1499ACF35B0A9E"` | Move to environment variable or secret manager (AWS Secrets Manager, Azure Key Vault, K8s Secret). The current key is in source control and ships in every build artifact. |
| Zero IV for AES-CBC | `EnDcryption.cs`: IV is 16 zero bytes | A static IV defeats CBC's purpose. Use a random IV prepended to the ciphertext. |
| Plaintext connection string fallback | `ucls_DAL.cs`: `getConnectionString()` falls back to `App.config` plaintext | Remove plaintext fallback; require encrypted config or environment variable. |

### 1.4 Architectural Gaps — Migration Guide Does Not Address These

| Gap | Why It Matters for Cross-Platform |
|----|----------------------------------|
| Static, non-thread-safe DAL (`objDAL` in `CStoreSCP.cs`) | Works on Windows with one-thread-per-association model. Under .NET 8 Generic Host with DI, multiple threads will race on the single static instance. Must be converted to a scoped/transient DI service. |
| `.Result` blocking in `WorklistItemsProvider.cs` lines 194, 344 | Runs inside a `System.Timers.Timer` callback. On Linux with a smaller default thread-pool, this is a realistic deadlock path. Replace with fully `async` timer pattern (`PeriodicTimer` in .NET 6+). |
| `deployType` global (`Global.cs`) | Controls hub-vs-spoke behaviour but is set at startup and never changes. In a container environment, this should be an environment variable, not a mutable static field. |

---

## 2. How the System Interacts with Lab Devices

The enabler sits on the hospital LAN as a DICOM node. Lab devices (modalities) are configured
once to point at it. After that, all interaction is automatic and protocol-driven.

### 2.1 Complete Exam Lifecycle

```
┌───────────────────────────┐
│  Clinician / Front Desk   │
│  Creates exam order in    │
│  CARE (web browser)       │
└────────────┬──────────────┘
             │ REST API → CARE Django backend stores
             │ scheduled exam (patient, modality, room)
             ▼
┌───────────────────────────────────────────────────────┐
│  CARE DICOM Enabler  (on-premise Windows PC or Linux) │
│                                                       │
│  ┌──────────────────────┐  ┌────────────────────────┐ │
│  │  MWL SCP  port 2008  │  │  StoreSCP  port 2007   │ │
│  └──────────────────────┘  └────────────────────────┘ │
└───────────────────────────────────────────────────────┘
             │                            ▲
             │   1. C-FIND (worklist)     │
             │ ◄──────────────────────────│ Technician
             │                            │ arrives with
             │   2. Worklist response     │ patient at
             │ ───────────────────────────► modality
             │
             │   3. [Exam runs on device]
             │
             │   4. C-STORE (images, one per frame/series)
             │ ◄──────────────────────────
             │
             │ [Images saved to SCP/ folder + MySQL]
             │
             ▼
  StoreSCU timer fires (every 5 s)
             │
             │   5. POST /api/plugin/care_radiology/dicom/upload/
             │ ──────────────────────────────────────────────────►
             │                                   CARE Django backend
             │   6. HTTP 200 OK + study_uid      (marks exam complete,
             │ ◄────────────────────────────      viewer available)
             │
  MySQL instance.upload_status = 'success'
  Local DICOM file deleted
```

### 2.2 Step-by-Step at the DICOM Protocol Level

**Step 1 — Device association request (C-ECHO)**
The modality first sends a C-ECHO to verify connectivity. The enabler's SCP accepts the
Verification SOP class (1.2.840.10008.1.1) and responds with Success. This is how a technician
tests the connection from the device console.

**Step 2 — Worklist query (C-FIND)**
When the technician opens "Worklist" on the device:
- Device sends a `C-FIND-RQ` to port 2008 with the Modality Worklist SOP class (1.2.840.10008.5.1.4.31)
- Query typically includes: today's date, modality code (e.g. `CT`), optionally patient name/ID
- The MWL SCP calls `GET /api/plugin/care_radiology/dicom/worklist/` on CARE
- CARE returns scheduled exams as JSON; the enabler maps each to a DICOM dataset
- One `C-FIND-RSP` (status `FF00h` = Pending) is sent per matching exam
- A final `C-FIND-RSP` (status `0000h` = Success) closes the query
- The device displays the list; the technician selects the patient — no manual typing needed

**Step 3 — Image acquisition**
The exam runs. The device collects raw image data internally. The enabler is not involved here.

**Step 4 — Image send (C-STORE)**
After acquisition (or during, for some devices):
- Device opens a new DICOM association to port 2007
- Proposes presentation contexts (SOP class + transfer syntax combinations)
- For each image, sends a `C-STORE-RQ` containing the full DICOM file
- The enabler's StoreSCP saves to `SCP/{StudyUID}/{SeriesUID}/{InstanceUID}.dcm` and writes metadata to MySQL
- Responds with `C-STORE-RSP` status `0000h` (Success) per image
- Device closes association when all images are sent

**Step 5 — Upload to CARE (C-STORE SCU / HTTP POST)**
The `StoreSCU` service wakes every 5 seconds:
- Queries MySQL for instances with `upload_status = 'pending'`
- For each: reads the DICOM file, posts it as `multipart/form-data` to CARE
- On HTTP 200: sets `upload_status = 'success'`, deletes local file
- On failure: sets `upload_status = 'failed'` (no retry currently — see §5.4)

### 2.3 One-Time Device Configuration

A biomedical engineer configures each modality once via the device's DICOM settings panel:

| Setting | Value (example) |
|---------|----------------|
| MWL Server AE Title | `MODALITYSCP` |
| MWL Server Host | `192.168.1.50` (IP of the PC running the enabler) |
| MWL Server Port | `2008` |
| Store Server AE Title | `STORAGESCP` |
| Store Server Host | `192.168.1.50` |
| Store Server Port | `2007` |
| Local AE Title | `CT_ROOM1` (device's own identity) |

The enabler's AE titles and ports are configured once in `cfg/common.cfg` (or App.config for
services). No further configuration is needed on either side unless the IP or AE title changes.

---

## 3. Supported Device Types and Modalities

### 3.1 Modality Coverage

The StoreSCP accepts **all DICOM Storage SOP classes**, meaning any image-producing device that
speaks DICOM can send to it without pre-registration of a specific SOP class.

| Modality | DICOM Code | Common Vendors | Notes |
|----------|-----------|---------------|-------|
| Computed Tomography | CT | Siemens, GE, Philips, Canon | Fully supported. Typical study: 200–800 instances |
| Magnetic Resonance | MR | Siemens, GE, Philips, Hitachi | Fully supported. Variable series count |
| Digital Radiography | DR/DX | Carestream, Fujifilm, Agfa | Fully supported |
| Computed Radiography | CR | Konica Minolta, Agfa | Fully supported (older equipment) |
| Ultrasound | US | GE, Siemens, Mindray, Samsung | Single and multi-frame both accepted |
| Mammography | MG | Hologic, GE, Siemens | Digital mammography SOP class accepted |
| PET / PET-CT | PT | GE, Siemens | Accepted; no special dose-report parsing |
| Nuclear Medicine | NM | GE, Siemens, Philips | Accepted |
| Fluoroscopy / Angiography | XA/RF | Siemens, Philips, GE | Accepted |
| Optical Coherence Tomography | OCT | Topcon, Zeiss | Accepted (OphthalmologyOCT SOP class) |
| Endoscopy / Visible Light | ES/VL | Olympus, Karl Storz | Accepted (multi-frame SC) |
| Structured Reports | SR | (generated by any modality) | Stored as file; no structured parsing |
| DICOM-ECG Waveform | ECG | GE MUSE, Philips PageWriter | Accepted; no waveform analysis |
| Dose Reports (RDSR) | SR | All CT/fluoroscopy vendors | Accepted as file; not forwarded to CARE separately |

### 3.2 Transfer Syntax Coverage

The StoreSCP advertises the following transfer syntaxes, covering virtually all modern and legacy
modalities:

| Transfer Syntax | UID | Coverage |
|----------------|-----|---------|
| Implicit VR Little Endian | 1.2.840.10008.1.2 | Legacy default; all old equipment |
| Explicit VR Little Endian | 1.2.840.10008.1.2.1 | Standard for modern equipment |
| Explicit VR Big Endian | 1.2.840.10008.1.2.2 | Older Philips/Siemens equipment |
| JPEG Lossless Process 14 SV1 | 1.2.840.10008.1.2.4.70 | Common for CR/DX lossless |
| JPEG 2000 Lossless | 1.2.840.10008.1.2.4.90 | Modern CT/MR lossless |
| JPEG 2000 Lossy | 1.2.840.10008.1.2.4.91 | Accepted for review purposes |
| RLE Lossless | 1.2.840.10008.1.2.5 | Nuclear medicine, older equipment |
| JPEG LS Near Lossless | 1.2.840.10008.1.2.4.81 | Some ultrasound vendors |
| JPEG Process 1 (lossy) | 1.2.840.10008.1.2.4.51 | Legacy fluoroscopy |

**Gap**: JPEG XL and High-Throughput JPEG 2000 (HTJ2K, `1.2.840.10008.1.2.4.202`) are emerging
in new scanner firmware. These are not listed in the current presentation context list but can be
added in `CStoreSCP.cs` as fo-dicom gains codec support.

### 3.3 Vendor Interoperability Notes

Most IHE-conformant modalities will work out of the box. Edge cases:

- **Older GE equipment**: May send JPEG Process 2/4 (extended lossy) — accepted by current SCP
- **Philips IntelliSpace**: Uses Deflate transfer syntax (1.2.840.10008.1.2.1.99) — not currently listed; add if needed
- **DICOM SR from modalities**: Dose reports and measurement SRs are accepted but not parsed; CARE does not receive structured data from them separately
- **MPPS N-CREATE/N-SET from all modern modalities**: The current enabler has no MPPS SCP — see §5.1

---

## 4. Protocol Coverage — What Works, What Is Missing

### 4.1 Current State

| DICOM Service | Role | Port | Status | Notes |
|--------------|------|------|--------|-------|
| C-ECHO | SCP | 2007, 2008 | Working | Both MWL and Store SCP respond |
| C-FIND (MWL) | SCP | 2008 | Working (bugs) | Name filter uses OR instead of AND (ISSUES M-L2) |
| C-STORE | SCP | 2007 | Working (bugs) | Crashes on missing DICOM tags (ISSUES C-1, C-2) |
| C-STORE | SCU | — | Working (bugs) | `async void` swallows failures (ISSUES M-1) |
| C-MOVE | — | — | Not implemented | — |
| C-GET | — | — | Not implemented | — |
| N-CREATE / N-SET (MPPS) | — | — | Interface stub only | `IMppsSource.cs` exists, not wired up |
| DICOM TLS | — | — | Not implemented | All traffic is plaintext TCP |

### 4.2 Missing Protocol: MPPS (Modality Performed Procedure Step)

**What it is**: MPPS is the mechanism by which modalities report exam status back to the RIS/MWL
system. After a technician starts an exam, the device sends:
- `N-CREATE` (MPPS In Progress) — "exam started, here are the planned steps"
- `N-SET` (MPPS Completed or Discontinued) — "exam done/cancelled, here is what was actually done"

**Why it matters**:
- Without MPPS, the CARE backend never knows an exam actually ran until images arrive
- Ordering, scheduling, and billing workflows in CARE cannot close the exam lifecycle loop
- Modern modalities (Siemens, GE, Philips) send MPPS by default; they will log errors if the SCP
  is missing. Some devices refuse to send images until MPPS is acknowledged.
- IHE Scheduled Workflow (SWF) profile requires MPPS

**Implementation path**:
- SOP classes: `1.2.840.10008.3.1.2.3.3` (MPPS SOP Class), `1.2.840.10008.3.1.2.3.4` (MPPS Pull)
- The `IMppsSource.cs` interface stub already exists in `CARE_MWL_Service/Model/`
- fo-dicom supports N-CREATE/N-SET via `IDicomNCreateProvider` and `IDicomNSetProvider`
- On receipt, the enabler must call CARE backend to update exam status:
  `PATCH /api/plugin/care_radiology/order/{service_request_id}/` with `status=in_progress` or `status=completed`

### 4.3 Missing Protocol: DICOM TLS

All DICOM associations currently run over unencrypted TCP. Patient data (names, IDs, images)
is visible to anyone on the hospital LAN.

**Regulatory context**: HIPAA Security Rule (45 CFR §164.312(e)) requires encryption of PHI
in transit over open networks. Many hospital compliance teams now require TLS even on internal VLANs.

**Implementation**: fo-dicom supports DICOM TLS natively. Configuration requires:
1. An X.509 certificate on the enabler
2. Setting `DicomServerFactory.Create<T>(port, tlsAcceptor: new DicomTlsAcceptor(cert))`
3. Modalities configured with the server certificate's thumbprint (or a CA-signed cert they trust)

### 4.4 Missing Protocol: C-MOVE / C-GET

C-MOVE and C-GET allow a PACS or viewer to pull images from an archive by sending a retrieve
request. The current system only supports push (C-STORE from modality → enabler → CARE).

**When this matters**: If CARE needs to fetch a prior study for comparison, or if a radiologist
uses a DICOM viewer (e.g. OHIF, 3D Slicer) that queries the enabler directly via C-FIND and
then pulls images via C-MOVE, the pull will fail.

For the current deployment model (all images pushed to CARE and viewed via CARE's viewer), C-MOVE
and C-GET are not required. They become necessary if the enabler is also used as a local archive.

### 4.5 Missing Protocol: HL7 v2

CARE currently uses its own REST API to serve the worklist. Many hospital HIS/RIS systems produce
orders as HL7 v2 ORM (Order Message) or ORU (Result Message) instead of or in addition to REST.

Without HL7 support, the enabler cannot receive worklist items from a standalone HIS that only
speaks HL7. This is a gap for integrations beyond the CARE ecosystem.

### 4.6 Missing: DICOMweb (WADO-RS / STOW-RS / QIDO-RS)

DICOMweb is the modern HTTP-based alternative to traditional DICOM networking. Cloud PACS
(Google Cloud Healthcare API, AWS HealthImaging, Azure DICOM Service) expose DICOMweb endpoints.
The current enabler does not speak DICOMweb; it uses traditional DICOM associations.

This is not a blocker for the current hospital deployment model but limits cloud integration options.

---

## 5. Core Features Required for Production Readiness

### 5.1 MPPS SCP (Priority: High for any IHE-conformant deployment)

**What to build**:
```
CARE MWL Service adds two new handlers:
  IDicomNCreateProvider → handle MPPS In Progress
  IDicomNSetProvider    → handle MPPS Completed / Discontinued

On N-CREATE:
  - Store MPPS dataset to MySQL (mpps table: study_uid, status, start_time, modality)
  - Call CARE: PATCH /api/plugin/care_radiology/order/{accession_no}/ {status: "in_progress"}

On N-SET (Completed):
  - Update MySQL mpps record
  - Call CARE: PATCH /api/plugin/care_radiology/order/{accession_no}/ {status: "completed"}

On N-SET (Discontinued):
  - Call CARE: PATCH /api/plugin/care_radiology/order/{accession_no}/ {status: "cancelled"}
```

### 5.2 DICOM TLS (Priority: High for any non-isolated network)

Enable per-port. MWL (2008) and StoreSCP (2007) both need TLS. Certificate management:
- Development: self-signed, loaded from file path configured in `appsettings.json`
- Production: CA-signed certificate, renewed via ACME/Let's Encrypt or hospital PKI
- Kubernetes: mounted as a Secret volume

### 5.3 Worklist Date-Range Query (Priority: Medium)

The current implementation returns today's scheduled items. Devices regularly query for a
date range (e.g. yesterday through tomorrow) to handle timezone edge cases and late starters.

**Fix in `WorklistHandler.cs`**: The `AddDateCondition()` method exists but uses a single-day
comparison. Change it to support `ScheduledProcedureStepStartDate` as a range:
- `20260611-20260613` → from June 11 to June 13 (DICOM range notation)
- `20260611-` → June 11 onwards (open-ended)

This also affects the CARE API query: pass `date_from` and `date_to` query parameters.

### 5.4 Upload Retry with Backoff (Priority: High for reliability)

Current behaviour: one attempt, mark `failed` on error, no retry.

**Required behaviour**:
```
Instance upload_status values:
  pending    → not yet attempted
  retrying   → failed at least once, scheduled for retry
  failed     → exhausted retries (manual intervention needed)
  success    → uploaded, file deleted

Retry schedule (exponential backoff):
  Attempt 1: immediate
  Attempt 2: 30 seconds later
  Attempt 3: 5 minutes later
  Attempt 4: 1 hour later
  → after 4 failures: status = 'failed', alert logged

Required schema addition:
  ALTER TABLE instance ADD COLUMN retry_count INT DEFAULT 0;
  ALTER TABLE instance ADD COLUMN next_retry_at DATETIME;
```

### 5.5 Health and Readiness Endpoints (Priority: High for container/cloud deployment)

The enabler has no HTTP surface today. Adding a minimal ASP.NET Core Minimal API alongside the
DICOM services provides:

```
GET /healthz/live   → 200 OK if process is running (liveness)
GET /healthz/ready  → 200 OK if:
                        MySQL connection: OK
                        MWL DICOM server: listening on 2008
                        StoreSCP: listening on 2007
                        CARE backend reachable: yes/no (degraded, not fatal)
GET /metrics        → Prometheus text format:
                        dicom_cfind_requests_total
                        dicom_cstore_requests_total
                        dicom_cstore_errors_total
                        upload_pending_count
                        upload_failed_count
```

Kubernetes liveness/readiness probes point to `/healthz/live` and `/healthz/ready`. Alerts fire
when `upload_failed_count > 0` or readiness fails.

### 5.6 Structured Audit Log (Priority: Medium for HIPAA compliance)

Every DICOM association and every image received/uploaded must be logged with:
- Timestamp, Calling AE Title, Called AE Title, Patient ID (hashed if logging to file), Study UID,
  operation (C-FIND / C-STORE), result (success / failure / rejection)

Store in a separate `audit_log` table (not the same Serilog file) so it can be queried and cannot
be disabled by changing a log level.

### 5.7 Multi-Facility Routing (Priority: Medium for hub-and-spoke deployments)

The `deployType` field (1 = Client/Hospital, 2 = Server/Central) implies a topology where multiple
hospital sites send images to a central archive. The routing logic does not currently exist.

**Required**: When `deployType = 2`, incoming C-STORE images must be tagged with the originating
facility (derived from Calling AE Title → facility lookup in `servers` table) and routed to the
correct CARE tenant or folder partition.

---

## 6. How the CARE Integration Pattern Extends to Other Systems

The enabler's integration with CARE follows a simple contract:
1. **Worklist**: `GET {base}/api/plugin/care_radiology/dicom/worklist/` with a static API key → returns JSON exam list
2. **Upload**: `POST {base}/api/plugin/care_radiology/dicom/upload/` with multipart DICOM file → returns `{study_uid}`
3. **Auth**: `POST {base}/api/token/` with device credentials → returns JWT

This same contract can be replicated or adapted for any other backend system by adding a new
backend type to `WorklistItemsProvider.cs` (currently: 0=hardcoded, 1=MySQL, 2=CARE HTTP, 3=mock).

### 6.1 Laboratory Information System (LIS) Integration

A digital pathology scanner or automated analyser behaves like a modality — it queries a worklist
and pushes result images. The enabler can serve as the DICOM gateway between the LIS and the
scanner.

```
LIS (REST or HL7)
       │
       │  GET /api/lis/worklist/   (new backend type 4)
       ▼
WorklistItemsProvider (backend=4: LisProvider)
       │
       │  Maps LIS order fields → DICOM worklist dataset
       ▼
C-FIND response to pathology scanner

Scanner acquires slide images
       │
       │  C-STORE → StoreSCP
       ▼
POST /api/lis/result/upload/    (or existing CARE upload endpoint)
```

LIS-specific fields to map:
| LIS Field | DICOM Tag |
|-----------|-----------|
| Order ID | AccessionNumber (0008,0050) |
| Patient ID | PatientID (0010,0020) |
| Specimen type | ScheduledProcedureStepDescription (0040,0007) |
| Requested procedure | RequestedProcedureDescription (0032,1060) |

### 6.2 Third-Party PACS / VNA

The `servers` table already stores AE Title, IP, and port for DICOM targets. The SCU service can
forward images to any PACS alongside (or instead of) the CARE HTTP upload:

```
StoreSCU modes (configurable per deployment):
  Mode A: HTTP POST to CARE only           ← current production mode
  Mode B: DICOM C-STORE to PACS only       ← for hospitals with existing PACS
  Mode C: Both (CARE + PACS simultaneously) ← for hybrid deployments
  Mode D: DICOM C-STORE to PACS, then      ← PACS as primary; CARE gets link
           CARE receives WADO-URI to image
```

Implementation: add an `upload_mode` field to the `servers` table and fan out the upload task
in the SCU service to all enabled destinations.

### 6.3 Cloud DICOM Storage

Cloud providers expose DICOMweb STOW-RS endpoints that accept multipart DICOM over HTTPS:

| Provider | Endpoint type | Auth |
|---------|--------------|------|
| Google Cloud Healthcare API | STOW-RS | OAuth2 service account |
| Azure Health Data Services | STOW-RS | Azure AD token |
| AWS HealthImaging | S3 (DICOM-native) | IAM credentials |

The SCU service's HTTP upload path can be adapted to each by:
1. Changing the `Content-Type` from `multipart/form-data` to `multipart/related; type=application/dicom`
2. Replacing the CARE static API key with the cloud provider's token
3. Adding a new backend type (e.g. `upload_target = "gcp_healthcare"`)

The CARE Django backend can then retrieve images from the cloud store via WADO-RS rather than
storing them itself — reducing CARE's storage overhead.

### 6.4 RIS via HL7 v2 ADT/ORM

Many hospitals run a standalone Radiology Information System (RIS) that produces orders as HL7 v2
ORM^O01 (Order Message). Adding an HL7 listener as a fifth backend type lets the enabler serve
worklist items from a RIS without modifying the RIS:

```
RIS sends HL7 ORM^O01 (TCP port 2575, MLLP framing)
       │
       ▼
HL7Listener (new background service)
  - Parses PID (patient), ORC (order control), OBR (observation request)
  - Inserts into worklist_queue table in MySQL
       │
       ▼
WorklistItemsProvider (backend=5: HL7Provider)
  - Reads from worklist_queue
  - Returns items to C-FIND as normal
```

Field mapping:
| HL7 Segment.Field | DICOM Tag |
|-------------------|-----------|
| PID-3 | PatientID (0010,0020) |
| PID-5 | PatientName (0010,0010) |
| PID-7 | PatientBirthDate (0010,0030) |
| OBR-4 | RequestedProcedureDescription (0032,1060) |
| OBR-18 | AccessionNumber (0008,0050) |
| OBR-24 | Modality in ScheduledProcedureStep (0040,0100 > 0008,0060) |

### 6.5 Summary: Integration Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│              Worklist Sources (backend type switch)              │
│                                                                  │
│  [0] Hardcoded  [1] MySQL  [2] CARE REST  [3] Mock              │
│  [4] LIS REST   [5] HL7 ORM                                      │
└─────────────────────────────┬────────────────────────────────────┘
                              │ WorklistItemsProvider
                              ▼
                    MWL SCP (C-FIND, port 2008)
                              │
                              ▼
                         Modalities
                              │  C-STORE
                              ▼
                    StoreSCP (port 2007)
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│              Upload Destinations (fan-out)                       │
│                                                                  │
│  [A] CARE HTTP POST (current)                                    │
│  [B] DICOM C-STORE → existing hospital PACS                      │
│  [C] STOW-RS → Google Cloud / Azure / AWS HealthImaging          │
│  [D] Both A + B simultaneously                                   │
└──────────────────────────────────────────────────────────────────┘
```

---

## Appendix — Quick Reference: What Needs to Happen Before Go-Live

### Must-Fix Before Any Deployment

- [ ] Fix C-1: null crash on missing StudyInstanceUID in `CStoreSCP.cs:195`
- [ ] Fix C-2: null crash on missing PatientID/Modality tags in `CStoreSCP.cs:243–253`
- [ ] Fix C-4: SQL injection in AE Title validation in `ucls_DAL.cs:395`
- [ ] Fix M-1: change `async void DicomSCUFile()` to `async Task`
- [ ] Move encryption key out of `EncKey.cs` into environment variable

### Must-Fix Before Cross-Platform Migration

- [ ] Replace `ServiceController` in Auth Service with platform-agnostic lifecycle management
- [ ] Replace all `\` path literals with `Path.Combine()`
- [ ] Remove Windows Event Log calls; use Serilog only
- [ ] Convert static `objDAL` to DI-scoped service
- [ ] Replace `.Result` blocking calls with `await` + `PeriodicTimer`
- [ ] Define replacement for WinForms UI (REST API + web frontend)

### Required for IHE-Conformant Production Use

- [ ] Implement MPPS SCP (N-CREATE / N-SET handlers)
- [ ] Enable DICOM TLS on both ports
- [ ] Fix worklist date-range query (`WorklistHandler.AddDateCondition`)
- [ ] Add upload retry with exponential backoff
- [ ] Add `/healthz` and `/metrics` endpoints
- [ ] Add structured audit log table

---

**Document Version**: 1.0  
**Date**: 2026-06-16  
**Based on**: Codebase analysis of `care_radiology_dicom_enabler` (branch: debug, commit 5611bb7)  
**Maintained by**: CARE Development Team
