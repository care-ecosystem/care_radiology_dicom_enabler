# Software Requirements Specification
## CARE Radiology DICOM Enabler

**Version:** 2.0  
**Date:** 2026-06-24  
**Status:** Draft  
**Based on:** Codebase analysis (branch: debug, commit 5611bb7) + FHIR R4 / CARE platform compatibility target

> **Related documents**  
> `CROSS_PLATFORM_MIGRATION.md` — migration execution plan  
> `INTEGRATION_AND_FEATURE_ANALYSIS.md` — device interaction and protocol coverage  
> `ISSUES.md` — known defects in the current implementation

---

## Table of Contents

1. [Purpose and Scope](#1-purpose-and-scope)
2. [System Context](#2-system-context)
3. [Stakeholders and User Roles](#3-stakeholders-and-user-roles)
4. [Functional Requirements — DICOM Services](#4-functional-requirements--dicom-services)
5. [Functional Requirements — CARE Platform Integration](#5-functional-requirements--care-platform-integration)
6. [Functional Requirements — FHIR R4 Compatibility](#6-functional-requirements--fhir-r4-compatibility)
7. [Functional Requirements — Management and Operations](#7-functional-requirements--management-and-operations)
8. [Data Requirements](#8-data-requirements)
9. [Non-Functional Requirements](#9-non-functional-requirements)
10. [Constraints and Assumptions](#10-constraints-and-assumptions)
11. [Requirement Traceability Matrix](#11-requirement-traceability-matrix)

---

## 1. Purpose and Scope

### 1.1 Purpose

This document specifies the complete functional and non-functional requirements for the CARE Radiology DICOM Enabler — a software gateway that bridges hospital imaging devices (CT, MRI, X-ray, ultrasound, etc.) with the CARE Health Information Management System.

It covers:
- Current behaviour that must be preserved
- Defects in the current implementation that constitute unmet requirements
- New requirements for FHIR R4 compatibility
- New requirements for full CARE platform integration
- Cross-platform and operational requirements

### 1.2 Scope

**In scope:**
- Modality Worklist (MWL) SCP service
- DICOM Image Storage SCP service
- DICOM Image Upload SCU service
- MPPS (Modality Performed Procedure Step) SCP service
- FHIR R4 resource mapping layer
- CARE backend REST API integration
- Device-facing DICOM network services
- Management API and configuration

**Out of scope:**
- CARE backend (Django) implementation — this document defines the contract the CARE backend must fulfill
- DICOM Viewer / OHIF integration — a separate concern
- HL7 v2 ADT/ORM ingestion — identified as a future extension, not a v2.0 requirement
- Radiologist reporting workflow — handled by CARE

### 1.3 Current System Summary

The current system is implemented in C# (.NET Framework 4.7.2) as four Windows Services plus a WinForms management GUI. It uses fo-dicom 5.0.2 for DICOM networking and MySQL for local metadata storage.

**What works today (verified by CI):**
- C-ECHO on both MWL (port 2008) and StoreSCP (port 2007)
- C-FIND Modality Worklist SCP with CARE backend as data source
- C-STORE SCP for receiving images from modalities
- C-STORE SCU for HTTP POST upload to CARE backend

**What is broken or missing today** (see `ISSUES.md` and `INTEGRATION_AND_FEATURE_ANALYSIS.md`):
- MPPS not implemented (interface stub only)
- DICOM TLS not implemented
- Six critical crash/security bugs
- No FHIR integration
- No upload retry logic
- No health/metrics endpoints
- Windows-only (cannot run on Linux or in containers)

---

## 2. System Context

### 2.1 Context Diagram

```
┌───────────────────────────────────────────────────────────────────────────┐
│                        Hospital Imaging Environment                       │
│                                                                           │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │
│  │  CT      │  │  MRI     │  │  DR/CR   │  │ Ultrasnd │  │ Mammo    │  │
│  │ Scanner  │  │ Scanner  │  │  X-ray   │  │          │  │          │  │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘  │
│       │              │              │              │              │        │
│       └──────────────┴──────────────┴──────────────┴─────────────┘        │
│                              DICOM (TCP)                                  │
│                    C-ECHO / C-FIND / C-STORE / N-CREATE / N-SET           │
└──────────────────────────────────┬────────────────────────────────────────┘
                                   │
                     ┌─────────────▼──────────────┐
                     │  CARE DICOM Enabler         │
                     │  (this system)              │
                     │                             │
                     │  ┌─────────────────────┐   │
                     │  │ MWL SCP  port 2008   │   │
                     │  │ StoreSCP port 2007   │   │
                     │  │ MPPS SCP port 2009   │   │
                     │  │ Mgmt API port 5000   │   │
                     │  └─────────────────────┘   │
                     └──────────────┬──────────────┘
                                    │
               ┌────────────────────┼───────────────────────┐
               │                    │                       │
  ┌────────────▼──────┐  ┌──────────▼──────────┐  ┌────────▼──────────┐
  │  CARE Platform    │  │  FHIR R4 Server      │  │  PACS / VNA       │
  │  (Django)         │  │  (CARE or standalone)│  │  (optional)       │
  │  REST API         │  │  REST API            │  │  DICOM C-STORE    │
  └───────────────────┘  └─────────────────────┘  └───────────────────┘
```

### 2.2 Interfaces Summary

| Interface | Direction | Protocol | Current State |
|-----------|-----------|----------|---------------|
| Modality → MWL SCP | Inbound | DICOM C-FIND | Working |
| Modality → StoreSCP | Inbound | DICOM C-STORE | Working (bugs) |
| Modality → MPPS SCP | Inbound | DICOM N-CREATE / N-SET | Not implemented |
| DICOM TLS | Inbound | DICOM over TLS | Not implemented |
| Enabler → CARE worklist | Outbound | HTTPS REST GET | Working |
| Enabler → CARE upload | Outbound | HTTPS REST POST | Working (no retry) |
| Enabler → CARE auth | Outbound | HTTPS REST POST | Working |
| Enabler → FHIR server | Outbound | FHIR R4 REST | Not implemented |
| FHIR server → Enabler | Inbound | FHIR R4 REST (webhook/subscription) | Not implemented |
| Enabler → PACS (SCU) | Outbound | DICOM C-STORE | Working (bugs) |
| Management UI → Enabler | Inbound | HTTP REST (new) | Not implemented |

---

## 3. Stakeholders and User Roles

| Role | Description | Primary Interactions |
|------|-------------|---------------------|
| **Radiologic Technologist** | Operates imaging devices; relies on worklist pre-fill | Modality console (indirect) |
| **Radiologist** | Reviews images in CARE viewer; writes reports | CARE platform (indirect) |
| **Biomedical Engineer** | Configures AE titles, IPs, ports on devices and enabler | Management UI / config file |
| **Hospital IT Administrator** | Installs, monitors, and maintains the enabler | Management UI, logs, health API |
| **CARE Platform Developer** | Maintains the Django backend; consumes enabler APIs | CARE REST / FHIR endpoints |
| **System Integrator** | Connects the enabler to third-party PACS, RIS, or LIS | DICOM SCU, FHIR, config |

---

## 4. Functional Requirements — DICOM Services

### 4.1 MWL SCP — Modality Worklist

#### FR-MWL-01: Association Acceptance
The system **shall** accept DICOM associations from any AE Title when `checkserver = false`.  
When `checkserver = true`, the system **shall** accept associations only from AE Titles present in the configured server list.

#### FR-MWL-02: C-ECHO Response
The system **shall** respond to C-ECHO requests on the MWL port with DICOM Status `0000h` (Success).

#### FR-MWL-03: C-FIND — SOP Class
The system **shall** accept C-FIND requests for the Modality Worklist Information Model SOP class (UID `1.2.840.10008.5.1.4.31`) only. All other SOP classes **shall** be rejected with a `Refused: No Acceptable Presentation Contexts` response.

#### FR-MWL-04: C-FIND — Query Attributes
The system **shall** honour the following query keys from the C-FIND request:

| DICOM Tag | Name | Match Type |
|-----------|------|-----------|
| 0010,0020 | Patient ID | Single Value / Wildcard |
| 0010,0010 | Patient Name | Wildcard (Surname^Forename) |
| 0008,0050 | Accession Number | Single Value |
| 0040,0100 > 0008,0060 | Scheduled Modality | Single Value |
| 0040,0100 > 0040,0001 | Scheduled Station AE Title | Single Value |
| 0040,0100 > 0040,0002 | Scheduled Procedure Step Start Date | Single Value / Range |
| 0040,0100 > 0040,0003 | Scheduled Procedure Step Start Time | Single Value / Range |

Unrecognised query keys **shall** be silently ignored (universal match).

#### FR-MWL-05: C-FIND — Response Attributes
Each C-FIND response dataset **shall** include at minimum:

| DICOM Tag | Name | Source |
|-----------|------|--------|
| 0010,0020 | Patient ID | CARE patient.id or generated |
| 0010,0010 | Patient Name | CARE patient.name (Surname^Forename) |
| 0010,0030 | Patient Birth Date | Derived from patient.age (see FR-MWL-08) |
| 0010,0040 | Patient Sex | CARE patient.gender mapped to M/F/O |
| 0008,0050 | Accession Number | Derived from service_request.external_id |
| 0020,000D | Study Instance UID | CARE-assigned or generated (see FR-MWL-09) |
| 0040,0100 | Scheduled Procedure Step Sequence | See sub-tags below |
| > 0008,0060 | Modality | From CARE service_request or config default |
| > 0040,0001 | Scheduled Station AE Title | From config `careScheduledAET` |
| > 0040,0002 | Scheduled Procedure Step Start Date | From service_request.date |
| > 0040,0003 | Scheduled Procedure Step Start Time | From service_request.date |
| > 0040,0007 | Scheduled Procedure Step Description | service_request.name |
| > 0040,0009 | Scheduled Procedure Step ID | service_request.id |
| 0032,1060 | Requested Procedure Description | service_request.name |
| 0008,0080 | Institution Name | facility.name |
| 0008,1030 | Study Description | service_request.name |

#### FR-MWL-06: C-FIND — Name Filter Logic
When the C-FIND request contains a Patient Name filter, the system **shall** apply the filter as an AND condition (Surname AND Forename must both match when both are specified). The current implementation uses OR and **shall** be corrected.

#### FR-MWL-07: C-FIND — Date Range
The system **shall** support DICOM date range notation for Scheduled Procedure Step Start Date:
- `YYYYMMDD` — exact date
- `YYYYMMDD-YYYYMMDD` — inclusive range
- `YYYYMMDD-` — from date onwards
- `-YYYYMMDD` — up to and including date

#### FR-MWL-08: Birth Date Derivation
When CARE provides only `patient.age` (integer years), the system **shall** derive a birth date as `today − age years`. The system **shall** log a warning that precision is limited to the year. When CARE provides a full ISO 8601 birth date (FHIR `Patient.birthDate`), the system **shall** use that value directly.

#### FR-MWL-09: Study Instance UID
The system **shall** use a CARE-assigned Study Instance UID when CARE provides one via the worklist API response. When no UID is provided, the system **shall** generate a locally unique UID using the DICOM UID root configured in `cfg/common.cfg` and cache it for the duration of the worklist refresh interval. Hardcoded UIDs (currently `1.2.34.567890.1234567890.1`) **shall** not be used in production.

#### FR-MWL-10: Worklist Refresh
The system **shall** refresh the local worklist cache by querying the backend at a configurable interval (default: 30 seconds). Stale cache **shall** still be served if the backend is temporarily unreachable; the system **shall** log a warning.

#### FR-MWL-11: Backend Data Sources
The system **shall** support the following worklist data sources, selectable via configuration:

| Backend ID | Source | Notes |
|-----------|--------|-------|
| 0 | Hardcoded test items | Development / diagnostics only |
| 1 | MySQL local database | Off-network deployments |
| 2 | CARE REST API | Primary production mode |
| 3 | Mock server (port 9000) | CI testing only |

#### FR-MWL-12: Modality Filter Passthrough
When the C-FIND request specifies a Scheduled Modality, the system **shall** pass the modality value as a query parameter to the CARE backend (`?modality=CT`) so the backend can pre-filter results.

---

### 4.2 StoreSCP — Image Receiver

#### FR-SCP-01: Association Acceptance
The system **shall** accept DICOM associations for C-STORE on the configured store port (default: 2007).

#### FR-SCP-02: C-ECHO Response
The system **shall** respond to C-ECHO on the store port with Status `0000h`.

#### FR-SCP-03: SOP Class Coverage
The system **shall** accept all Storage SOP classes in the current DICOM standard. The system **shall** accept at minimum the following transfer syntaxes:

| UID | Name |
|-----|------|
| 1.2.840.10008.1.2 | Implicit VR Little Endian |
| 1.2.840.10008.1.2.1 | Explicit VR Little Endian |
| 1.2.840.10008.1.2.2 | Explicit VR Big Endian |
| 1.2.840.10008.1.2.4.70 | JPEG Lossless P14 SV1 |
| 1.2.840.10008.1.2.4.90 | JPEG 2000 Lossless |
| 1.2.840.10008.1.2.4.91 | JPEG 2000 Lossy |
| 1.2.840.10008.1.2.5 | RLE Lossless |
| 1.2.840.10008.1.2.4.80 | JPEG LS Lossless |
| 1.2.840.10008.1.2.4.81 | JPEG LS Near Lossless |

#### FR-SCP-04: Missing Tag Handling
The system **shall** handle missing or empty DICOM tags gracefully. Specifically:
- If `StudyInstanceUID` (0020,000D) is absent, the system **shall** generate a local UID and log a warning.
- If `PatientID` (0010,0020) is absent, the system **shall** store the image with an empty patient ID and log a warning.
- If `Modality` (0008,0060) is absent, the system **shall** store the image with modality `OT` (Other) and log a warning.
- The system **shall never** throw an unhandled exception due to a missing DICOM tag.

#### FR-SCP-05: Storage Path
The system **shall** store received DICOM files in the path:
```
{StorageBasePath}/{StudyInstanceUID}/{SeriesInstanceUID}/{SOPInstanceUID}.dcm
```
`StorageBasePath` **shall** be configurable via environment variable `DICOM_STORAGE_PATH` or config file. Path construction **shall** use platform-independent path APIs.

#### FR-SCP-06: Directory Auto-Creation
The system **shall** create all required directories in the storage path before writing the first file in a study/series.

#### FR-SCP-07: Metadata Extraction
On receipt of each DICOM file, the system **shall** extract and persist the following metadata to the local database:

| DICOM Tag | Field | Table |
|-----------|-------|-------|
| 0020,000D | StudyInstanceUID | study |
| 0020,000E | SeriesInstanceUID | series |
| 0008,0018 | SOPInstanceUID | instance |
| 0010,0020 | PatientID | study |
| 0008,0050 | AccessionNumber | study |
| 0008,0060 | Modality | series |
| 0008,0020 | StudyDate | study |
| 0008,0030 | StudyTime | study |
| 0008,1030 | StudyDescription | study |
| 0008,103E | SeriesDescription | series |
| 0008,0080 | InstitutionName | study |
| 0020,0013 | InstanceNumber | instance |

#### FR-SCP-08: AE Title Validation
When `checkserver = true`, the system **shall** validate the calling AE Title against the configured server list. The validation **shall** use parameterised SQL queries. Non-whitelisted AE Titles **shall** result in association rejection with status `Refused: Called AE Title Not Recognised`.

#### FR-SCP-09: Duplicate Instance Handling
The system **shall** detect duplicate SOPInstanceUIDs and overwrite the existing file and database record, logging a warning. It **shall not** return an error to the modality for a duplicate.

#### FR-SCP-10: Concurrent Associations
The system **shall** handle at least 10 simultaneous C-STORE associations from different modalities without data corruption or deadlock.

---

### 4.3 MPPS SCP — Modality Performed Procedure Step

#### FR-MPPS-01: MPPS Port
The system **shall** listen for MPPS operations on a configurable port (default: 2009). The port **shall** be independent of the MWL and Store SCP ports.

#### FR-MPPS-02: N-CREATE (MPPS In Progress)
The system **shall** accept DICOM N-CREATE requests for the Modality Performed Procedure Step SOP class (UID `1.2.840.10008.3.1.2.3.3`).

On receipt of N-CREATE, the system **shall**:
1. Store the MPPS dataset in the local database (see FR-DATA-05)
2. Notify the CARE backend with status `in_progress` (see FR-CARE-05)
3. Respond with N-CREATE-RSP, Status `0000h`

Minimum attributes to capture:

| DICOM Tag | Name |
|-----------|------|
| 0040,A372 | Performed Procedure Code Sequence |
| 0008,0060 | Modality |
| 0040,0253 | Performed Procedure Step ID |
| 0040,0244 | Performed Procedure Step Start Date |
| 0040,0245 | Performed Procedure Step Start Time |
| 0008,1030 | Study Description |
| 0020,000D | Study Instance UID (from Referenced Study) |
| 0008,0050 | Accession Number |

#### FR-MPPS-03: N-SET (MPPS Completed)
The system **shall** accept N-SET requests to update MPPS status to `COMPLETED` or `DISCONTINUED`.

On receipt of N-SET with status `COMPLETED`, the system **shall**:
1. Update the MPPS record in the local database
2. Notify the CARE backend with status `completed` (see FR-CARE-06)
3. Respond with N-SET-RSP, Status `0000h`

On receipt of N-SET with status `DISCONTINUED`, the system **shall**:
1. Update the MPPS record
2. Notify the CARE backend with status `cancelled`
3. Respond with N-SET-RSP, Status `0000h`

#### FR-MPPS-04: Orphan MPPS Handling
The system **shall** accept and store MPPS messages even when no matching worklist item exists in the local cache. These **shall** be logged as warnings and forwarded to CARE.

---

### 4.4 StoreSCU — Image Upload

#### FR-SCU-01: Trigger
The system **shall** poll the local database for instances with `upload_status = 'pending'` at a configurable interval (default: 5 seconds).

#### FR-SCU-02: Upload Modes
The system **shall** support the following upload destinations, configurable per deployment:
- **Mode A (HTTP POST)**: Multipart/form-data POST to CARE `upload` endpoint
- **Mode B (DICOM C-STORE)**: DICOM C-STORE to a configured PACS AE Title
- **Mode C (Both)**: Upload to CARE HTTP and DICOM PACS simultaneously

#### FR-SCU-03: Retry Policy
The system **shall** implement an exponential-backoff retry policy for failed uploads:

| Attempt | Delay Before Retry |
|---------|-------------------|
| 1st (initial) | Immediate |
| 2nd | 30 seconds |
| 3rd | 5 minutes |
| 4th | 1 hour |
| After 4th failure | Mark `upload_status = 'failed'`; emit alert log |

#### FR-SCU-04: Success Handling
On a successful upload response, the system **shall**:
1. Update `instance.upload_status = 'success'`
2. Delete the local DICOM file if `deleteAfterUpload = true` (default: true)
3. Update `instance.uploaded_at` timestamp

#### FR-SCU-05: Async Upload
Upload operations **shall** be implemented using `async Task` (not `async void`). All exceptions **shall** be caught, logged, and reflected in the database status. Silent exception swallowing **shall not** occur.

#### FR-SCU-06: Batch Size
The system **shall** process pending uploads in configurable batches (default: 10 per polling cycle) to prevent unbounded memory use with large queues.

---

### 4.5 DICOM TLS

#### FR-TLS-01: TLS Support
The system **shall** support DICOM over TLS (DICOM TLS profile, RFC 5246 / TLS 1.2 minimum, TLS 1.3 preferred) on all inbound DICOM ports (MWL, StoreSCP, MPPS).

#### FR-TLS-02: Certificate Configuration
TLS certificates **shall** be configurable via:
- File path + password (for on-premises deployments)
- Kubernetes Secret mount (for container deployments)

#### FR-TLS-03: Non-TLS Fallback
Non-TLS operation **shall** remain available via configuration flag for environments where all devices are on an isolated LAN.

---

## 5. Functional Requirements — CARE Platform Integration

### 5.1 Worklist API

#### FR-CARE-01: Worklist Request
The system **shall** fetch scheduled exams from CARE using:

```
GET {careBaseUrl}/api/plugin/care_radiology/dicom/worklist/
Headers:
  Authorization: Token {careToken}
Query parameters:
  modality={modality_code}     (optional; from C-FIND filter)
  date_from={YYYYMMDD}         (optional; from C-FIND date range)
  date_to={YYYYMMDD}           (optional; from C-FIND date range)
  facility={facility_id}       (optional; multi-facility deployments)
```

#### FR-CARE-02: Worklist Response Mapping
The system **shall** map the CARE API response to DICOM Modality Worklist items using the following field mapping. Fields marked **Required** **shall** cause a warning log if absent; the item is still added with empty values.

| CARE JSON Field | DICOM Tag | Notes |
|----------------|-----------|-------|
| `service_request.external_id` | AccessionNumber (0008,0050) | Extract last two segments split by `-` |
| `service_request.id` | Scheduled Procedure Step ID (0040,0009) | |
| `service_request.name` | Requested Procedure Description (0032,1060) | Also Study Description (0008,1030) |
| `service_request.date` | Scheduled Start Date/Time (0040,0002/3) | ISO 8601 → DICOM DA/TM |
| `service_request.modality` | Modality (0008,0060) | Fall back to `careModality` config if absent |
| `patient.id` (FHIR) / generated | Patient ID (0010,0020) | See FR-FHIR-04 |
| `patient.name` | Patient Name (0010,0010) | Split on space: Surname^Forename |
| `patient.gender` | Patient Sex (0010,0040) | `male`→`M`, `female`→`F`, other→`O` |
| `patient.birth_date` (FHIR) | Patient Birth Date (0010,0030) | Prefer over `age` |
| `patient.age` | Patient Birth Date (0010,0030) | Approximate: today minus N years |
| `patient.phone_number` | Patient Telephone Numbers (0010,2154) | |
| `facility.name` | Institution Name (0008,0080) | |
| `study_instance_uid` (FHIR) | Study Instance UID (0020,000D) | If CARE provides it |

#### FR-CARE-03: Image Upload Request
The system **shall** upload received DICOM files to CARE using:

```
POST {careBaseUrl}/api/plugin/care_radiology/dicom/upload/
Headers:
  Authorization: Token {careToken}
  Content-Type: multipart/form-data
Body:
  file: {DICOM binary}
  study_uid: {StudyInstanceUID}
  series_uid: {SeriesInstanceUID}     (new: for server-side organisation)
  accession_number: {AccessionNumber} (new: for linking to service_request)
```

#### FR-CARE-04: Upload Response Handling
On HTTP 201 response from CARE upload, the system **shall** extract `study_uid` from the response body and store it in the local `study` table as `care_study_uid`.

#### FR-CARE-05: MPPS In-Progress Notification
On MPPS N-CREATE receipt, the system **shall** call:

```
PATCH {careBaseUrl}/api/plugin/care_radiology/service_request/{accession_number}/status/
Headers:
  Authorization: Token {careToken}
Body (JSON):
  {
    "status": "in_progress",
    "mpps_uid": "{MPPSInstanceUID}",
    "started_at": "{ISO8601 datetime}"
  }
```

#### FR-CARE-06: MPPS Completion Notification
On MPPS N-SET (COMPLETED), the system **shall** call:

```
PATCH {careBaseUrl}/api/plugin/care_radiology/service_request/{accession_number}/status/
Body:
  {
    "status": "completed",
    "ended_at": "{ISO8601 datetime}",
    "num_instances": {count}
  }
```

On MPPS N-SET (DISCONTINUED):
```
Body: { "status": "cancelled", "reason": "{DiscontinuationReason}" }
```

#### FR-CARE-07: Authentication
The system **shall** obtain a bearer token from CARE using:
```
POST {careBaseUrl}/api/token/
Body: { "device": "{deviceName}", "username": "{userName}", "password": "{password}" }
Response: { "access": "{jwt}", "refresh": "{refreshJwt}" }
```

The system **shall** cache the access token and refresh it before expiry using the refresh token. The system **shall not** re-authenticate from scratch on every API call.

#### FR-CARE-08: Device Registration
On first startup, the system **shall** attempt to register the device with CARE:
```
POST {careBaseUrl}/api/plugin/care_radiology/device/register/
Body: { "device_name": "...", "deploy_type": 1|2, "mwl_ae_title": "...", "store_ae_title": "..." }
```
If registration fails, the system **shall** log a warning and continue operating (registration failure is non-fatal).

#### FR-CARE-09: Connection Resilience
The system **shall** continue serving modality worklist queries from its local cache when the CARE backend is temporarily unreachable. The system **shall** queue image uploads and retry them when the backend becomes reachable again.

---

## 6. Functional Requirements — FHIR R4 Compatibility

### 6.1 Overview

FHIR R4 compatibility means the enabler can:
1. **Consume** FHIR resources from a FHIR server (e.g. CARE's FHIR endpoint) as an alternative to the CARE proprietary REST API
2. **Produce** FHIR resources representing the imaging workflow (ImagingStudy, DiagnosticReport) and write them to a FHIR server
3. **Expose** FHIR-compliant resource representations via its own management API

The CARE platform **shall** expose a FHIR R4 endpoint. The enabler supports both the current CARE REST contract and the FHIR contract, switchable via configuration (`backend = 4` for FHIR mode).

### 6.2 FHIR Resources in Scope

| FHIR Resource | Usage in this system |
|--------------|---------------------|
| `Patient` (R4) | Source of patient demographics for worklist |
| `ServiceRequest` (R4) | Scheduled imaging order — primary worklist source |
| `ImagingStudy` (R4) | Created/updated by enabler when images are received |
| `DiagnosticReport` (R4) | Created by enabler when study is complete (stub); filled by radiologist in CARE |
| `Practitioner` (R4) | Referring and performing physician identity |
| `Organization` (R4) | Facility / institution |
| `Endpoint` (R4) | DICOM connection parameters (AE Title, IP, port) |
| `Task` (R4) | Tracks upload and MPPS state in FHIR workflow |

### 6.3 FHIR Worklist Source (Consuming FHIR)

#### FR-FHIR-01: ServiceRequest Query
In FHIR mode, the system **shall** query the FHIR server for scheduled imaging orders:

```
GET {fhirBaseUrl}/ServiceRequest
  ?status=active
  &category=imaging
  &_include=ServiceRequest:subject          (Patient)
  &_include=ServiceRequest:requester        (Practitioner)
  &_include=ServiceRequest:performer        (Organization)
  &occurrence={date_from},{date_to}         (date range from C-FIND)
  &code={modality_snomed_code}              (optional modality filter)
  &_format=json
Headers:
  Authorization: Bearer {fhirToken}
```

#### FR-FHIR-02: ServiceRequest → DICOM Worklist Mapping
The system **shall** map FHIR `ServiceRequest` + included resources to DICOM worklist items:

| FHIR Path | DICOM Tag | Notes |
|-----------|-----------|-------|
| `ServiceRequest.identifier[system=accession].value` | AccessionNumber (0008,0050) | |
| `ServiceRequest.id` | Scheduled Procedure Step ID (0040,0009) | |
| `ServiceRequest.code.coding[system=SNOMED].display` | Requested Procedure Description (0032,1060) | |
| `ServiceRequest.code.coding[system=DICOM].code` | Modality (0008,0060) | e.g. `CT`, `MR` |
| `ServiceRequest.occurrenceDateTime` | Scheduled Start Date/Time (0040,0002/3) | |
| `ServiceRequest.subject` (Patient reference) | → Patient tags | |
| `Patient.identifier[system=MRN].value` | Patient ID (0010,0020) | |
| `Patient.name[use=official].family` | Patient Name Surname (0010,0010) | |
| `Patient.name[use=official].given[0]` | Patient Name Forename (0010,0010) | |
| `Patient.birthDate` | Patient Birth Date (0010,0030) | YYYY-MM-DD → YYYYMMDD |
| `Patient.gender` | Patient Sex (0010,0040) | `male`→`M`, `female`→`F` |
| `Patient.telecom[system=phone].value` | Patient Telephone (0010,2154) | |
| `ServiceRequest.requester` (Practitioner) | Referring Physician (0008,0090) | |
| `ServiceRequest.performer` (Organization) | Institution Name (0008,0080) | |
| `ServiceRequest.identifier[system=study_uid].value` | Study Instance UID (0020,000D) | If provided |

#### FR-FHIR-03: Patient Search Fallback
If the ServiceRequest bundle does not include the Patient resource inline, the system **shall** perform a secondary FHIR lookup:
```
GET {fhirBaseUrl}/Patient/{patient_id}
```

#### FR-FHIR-04: Patient ID Stability
The system **shall** use the FHIR `Patient.id` (server-assigned logical ID) as the DICOM Patient ID (0010,0020) when querying CARE via FHIR, to ensure stable cross-system patient identity.

### 6.4 ImagingStudy — Creating FHIR Resources (Producing FHIR)

#### FR-FHIR-05: ImagingStudy Creation
When the first C-STORE image of a new study is received, the system **shall** create a FHIR `ImagingStudy` resource:

```json
{
  "resourceType": "ImagingStudy",
  "status": "registered",
  "subject": { "reference": "Patient/{fhir_patient_id}" },
  "basedOn": [{ "reference": "ServiceRequest/{fhir_sr_id}" }],
  "identifier": [
    { "system": "urn:dicom:uid", "value": "urn:oid:{StudyInstanceUID}" },
    { "use": "usual", "value": "{AccessionNumber}" }
  ],
  "started": "{study_date_time_iso}",
  "numberOfSeries": 0,
  "numberOfInstances": 0,
  "endpoint": [{ "reference": "Endpoint/{wado_endpoint_id}" }],
  "series": []
}
```

The system **shall** POST this resource to:
```
POST {fhirBaseUrl}/ImagingStudy
```

#### FR-FHIR-06: ImagingStudy Update
As each series and instance is received, the system **shall** update the `ImagingStudy`:
- Increment `numberOfSeries` and `numberOfInstances`
- Add series entries with `uid`, `modality`, `numberOfInstances`, and `instance` array
- Update `status` to `available` when MPPS COMPLETED is received

```
PUT {fhirBaseUrl}/ImagingStudy/{imagingStudy_id}
```

#### FR-FHIR-07: ImagingStudy Series / Instance Population
Each series in `ImagingStudy.series` **shall** contain:

| FHIR Field | Source |
|-----------|--------|
| `uid` | SeriesInstanceUID |
| `number` | SeriesNumber |
| `modality.code` | Modality DICOM code (e.g. `CT`) |
| `modality.system` | `http://dicom.nema.org/resources/ontology/DCM` |
| `description` | SeriesDescription |
| `numberOfInstances` | Count of received instances |
| `instance[].uid` | SOPInstanceUID |
| `instance[].sopClass.code` | SOP Class UID |
| `instance[].number` | InstanceNumber |
| `instance[].title` | ImageComments or empty |

#### FR-FHIR-08: ImagingStudy Endpoint Reference
The `ImagingStudy.endpoint` **shall** reference a FHIR `Endpoint` resource representing the WADO-RS or DICOM retrieve address, if available. The enabler **shall** create or reuse this `Endpoint` resource at startup.

### 6.5 DiagnosticReport — Stub Creation

#### FR-FHIR-09: DiagnosticReport Stub
When MPPS COMPLETED is received, the system **shall** create a stub FHIR `DiagnosticReport`:

```json
{
  "resourceType": "DiagnosticReport",
  "status": "partial",
  "category": [{ "coding": [{ "system": "http://loinc.org", "code": "18748-4", "display": "Diagnostic imaging study" }] }],
  "code": { "text": "{ServiceRequest.code.text}" },
  "subject": { "reference": "Patient/{fhir_patient_id}" },
  "basedOn": [{ "reference": "ServiceRequest/{fhir_sr_id}" }],
  "imagingStudy": [{ "reference": "ImagingStudy/{imagingStudy_id}" }],
  "effectiveDateTime": "{mpps_completed_at}"
}
```

Status **shall** remain `partial` until a radiologist updates it in CARE.

### 6.6 FHIR Task — Workflow Tracking

#### FR-FHIR-10: Task for Upload State
The system **shall** create a FHIR `Task` resource when image upload begins and update it on completion:

| Upload State | Task.status |
|-------------|-------------|
| pending | `requested` |
| in progress | `in-progress` |
| success | `completed` |
| failed | `failed` |

### 6.7 FHIR Authentication

#### FR-FHIR-11: SMART on FHIR
The system **shall** support SMART on FHIR `client_credentials` flow for accessing the FHIR server:
```
POST {fhirTokenUrl}
Body: grant_type=client_credentials&client_id=...&client_secret=...
Response: { "access_token": "...", "expires_in": 3600 }
```

The token **shall** be refreshed automatically before expiry.

#### FR-FHIR-12: FHIR API Key Fallback
The system **shall** also support a static bearer token for FHIR servers that do not implement SMART on FHIR.

### 6.8 DICOM–FHIR Mapping Reference

#### Modality Code Mapping (DICOM ↔ SNOMED CT / FHIR)

| DICOM Code | SNOMED CT Display | FHIR code.coding.code |
|-----------|-------------------|-----------------------|
| CT | Computed tomography | 77477000 |
| MR | Magnetic resonance imaging | 113091000 |
| CR | Computed radiography | 168537006 |
| DX | Digital radiography | 363680008 |
| US | Ultrasonography | 16310003 |
| MG | Mammography | 71651007 |
| PT | Positron emission tomography | 82918005 |
| NM | Nuclear medicine | 363680008 |
| XA | X-ray angiography | 77343006 |
| RF | Fluoroscopy | 44491008 |

#### Patient Sex Mapping

| DICOM Value | FHIR Value |
|------------|-----------|
| M | `male` |
| F | `female` |
| O | `other` |
| (empty) | `unknown` |

---

## 7. Functional Requirements — Management and Operations

### 7.1 Management REST API

#### FR-MGT-01: Service Status
The system **shall** expose `GET /api/services` returning the running status of each DICOM service (MWL SCP, StoreSCP, MPPS SCP, StoreSCU).

#### FR-MGT-02: Health Check
The system **shall** expose:
- `GET /healthz/live` — returns HTTP 200 if the process is running
- `GET /healthz/ready` — returns HTTP 200 when all DICOM ports are bound and the database is reachable; HTTP 503 otherwise with a JSON body describing which checks failed

#### FR-MGT-03: Metrics
The system **shall** expose `GET /metrics` in Prometheus text format including at minimum:

| Metric | Type | Description |
|--------|------|-------------|
| `dicom_cfind_requests_total` | Counter | C-FIND requests received |
| `dicom_cstore_requests_total` | Counter | C-STORE requests received |
| `dicom_cstore_errors_total` | Counter | C-STORE requests that resulted in error |
| `dicom_mpps_ncreate_total` | Counter | MPPS N-CREATE requests |
| `dicom_mpps_nset_total` | Counter | MPPS N-SET requests |
| `upload_pending_total` | Gauge | Instances with upload_status=pending |
| `upload_failed_total` | Gauge | Instances with upload_status=failed |
| `upload_success_total` | Counter | Instances successfully uploaded |
| `worklist_items_cached` | Gauge | Number of items in current worklist cache |
| `backend_api_latency_seconds` | Histogram | CARE/FHIR API call duration |

#### FR-MGT-04: Server CRUD
The system **shall** expose CRUD endpoints for managing the DICOM server list (the `servers` database table) via `GET/POST/PUT/DELETE /api/servers`.

#### FR-MGT-05: Configuration
Key configuration values (AE titles, ports, backend URL, modality filter, upload mode, TLS enabled) **shall** be readable and writable via `GET/PUT /api/config`.

#### FR-MGT-06: Log Access
The system **shall** expose `GET /api/logs?service={name}&level={level}&from={iso8601}&to={iso8601}` returning structured log entries as JSON.

### 7.2 Configuration

#### FR-CFG-01: Environment Variables
All sensitive configuration (backend URL, API key, database password, encryption key) **shall** be configurable via environment variables. Environment variables **shall** take precedence over config file values.

| Environment Variable | Description |
|---------------------|-------------|
| `DICOM_STORAGE_PATH` | Base path for received DICOM files |
| `CARE_BASE_URL` | CARE backend base URL |
| `CARE_API_TOKEN` | Static API token for CARE |
| `FHIR_BASE_URL` | FHIR server base URL (optional) |
| `FHIR_CLIENT_ID` | SMART on FHIR client ID |
| `FHIR_CLIENT_SECRET` | SMART on FHIR client secret |
| `DB_CONNECTION_STRING` | MySQL/PostgreSQL connection string |
| `ENCRYPTION_KEY` | 32-byte AES key (replaces hardcoded EncKey.cs) |
| `MWL_AE_TITLE` | MWL SCP AE Title |
| `MWL_PORT` | MWL SCP port |
| `STORE_AE_TITLE` | StoreSCP AE Title |
| `STORE_PORT` | StoreSCP port |
| `MPPS_PORT` | MPPS SCP port |
| `TLS_CERT_PATH` | Path to TLS certificate file |
| `TLS_CERT_PASSWORD` | TLS certificate password |
| `BACKEND_MODE` | `care_rest` (default) or `fhir` |

### 7.3 Audit Logging

#### FR-AUDIT-01: Audit Events
The system **shall** write a structured audit log entry for each of the following events:

| Event | Required Fields |
|-------|----------------|
| DICOM association opened | timestamp, calling AE, called AE, source IP |
| DICOM association closed | timestamp, calling AE, reason |
| C-FIND request received | timestamp, AE, query attributes (no PHI in log) |
| C-STORE image received | timestamp, AE, SOPInstanceUID, StudyInstanceUID |
| C-STORE image rejected | timestamp, AE, reason |
| MPPS N-CREATE received | timestamp, AE, MPPSInstanceUID, AccessionNumber |
| MPPS N-SET received | timestamp, AE, MPPSInstanceUID, new status |
| Image uploaded to CARE | timestamp, instance_id, response_status |
| Image upload failed | timestamp, instance_id, error, retry_count |
| Auth token obtained | timestamp (no credential values logged) |
| Config changed via API | timestamp, user, field changed (no secret values) |

#### FR-AUDIT-02: Audit Log Storage
Audit logs **shall** be written to a separate `audit_log` database table in addition to the Serilog file log. The database table **shall** be queryable via the management API.

---

## 8. Data Requirements

### 8.1 Local Database Schema

#### Table: `study`
```sql
CREATE TABLE study (
  id                  INT AUTO_INCREMENT PRIMARY KEY,
  study_uid           VARCHAR(500) NOT NULL UNIQUE,
  care_study_uid      VARCHAR(500),          -- CARE-assigned UID from upload response
  fhir_imaging_study_id VARCHAR(200),        -- FHIR ImagingStudy resource ID
  fhir_sr_id          VARCHAR(200),          -- FHIR ServiceRequest resource ID
  patient_id          VARCHAR(200),
  fhir_patient_id     VARCHAR(200),          -- FHIR Patient resource ID
  accession_number    VARCHAR(100),
  study_date          DATE,
  study_time          TIME,
  study_description   VARCHAR(500),
  modality_codes      VARCHAR(100),
  institution_name    VARCHAR(255),
  num_series          INT DEFAULT 0,
  num_instances       INT DEFAULT 0,
  upload_status       ENUM('pending','retrying','success','failed') DEFAULT 'pending',
  care_upload_status  ENUM('pending','retrying','success','failed') DEFAULT 'pending',
  fhir_sync_status    ENUM('pending','synced','failed') DEFAULT 'pending',
  received_at         TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_study_uid (study_uid),
  INDEX idx_accession (accession_number),
  INDEX idx_patient_id (patient_id)
);
```

#### Table: `series`
```sql
CREATE TABLE series (
  id               INT AUTO_INCREMENT PRIMARY KEY,
  series_uid       VARCHAR(500) NOT NULL UNIQUE,
  study_id         INT REFERENCES study(id) ON DELETE CASCADE,
  modality         VARCHAR(20),
  series_number    INT,
  series_description VARCHAR(500),
  num_instances    INT DEFAULT 0,
  INDEX idx_series_uid (series_uid)
);
```

#### Table: `instance`
```sql
CREATE TABLE instance (
  id               INT AUTO_INCREMENT PRIMARY KEY,
  sop_instance_uid VARCHAR(500) NOT NULL UNIQUE,
  series_id        INT REFERENCES series(id) ON DELETE CASCADE,
  instance_number  INT,
  sop_class_uid    VARCHAR(200),
  transfer_syntax  VARCHAR(200),
  file_path        VARCHAR(1000),
  file_size        BIGINT,
  upload_status    ENUM('pending','retrying','success','failed') DEFAULT 'pending',
  retry_count      INT DEFAULT 0,
  next_retry_at    DATETIME,
  created_at       TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  uploaded_at      DATETIME,
  INDEX idx_sop_instance_uid (sop_instance_uid),
  INDEX idx_upload_status (upload_status),
  INDEX idx_next_retry (next_retry_at)
);
```

#### Table: `mpps`
```sql
CREATE TABLE mpps (
  id                    INT AUTO_INCREMENT PRIMARY KEY,
  mpps_instance_uid     VARCHAR(500) NOT NULL UNIQUE,
  study_uid             VARCHAR(500),
  accession_number      VARCHAR(100),
  modality              VARCHAR(20),
  status                ENUM('in_progress','completed','discontinued') NOT NULL,
  started_at            DATETIME,
  ended_at              DATETIME,
  num_instances         INT,
  discontinuation_reason VARCHAR(500),
  care_notified         BOOLEAN DEFAULT FALSE,
  fhir_task_id          VARCHAR(200),
  created_at            TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_mpps_uid (mpps_instance_uid),
  INDEX idx_accession (accession_number)
);
```

#### Table: `servers`
```sql
CREATE TABLE servers (
  id            INT AUTO_INCREMENT PRIMARY KEY,
  ae_title      VARCHAR(100) NOT NULL,
  host          VARCHAR(255) NOT NULL,
  port          INT NOT NULL,
  description   VARCHAR(500),
  server_type   ENUM('store_scu_target','mwl_source','mpps_target') DEFAULT 'store_scu_target',
  tls_enabled   BOOLEAN DEFAULT FALSE,
  is_active     BOOLEAN DEFAULT TRUE,
  created_at    TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  UNIQUE (ae_title, host, port)
);
```

#### Table: `audit_log`
```sql
CREATE TABLE audit_log (
  id            BIGINT AUTO_INCREMENT PRIMARY KEY,
  event_time    DATETIME NOT NULL,
  event_type    VARCHAR(100) NOT NULL,
  calling_ae    VARCHAR(100),
  called_ae     VARCHAR(100),
  source_ip     VARCHAR(45),
  resource_type VARCHAR(100),
  resource_id   VARCHAR(500),
  result        ENUM('success','failure','warning') NOT NULL,
  detail        TEXT,
  INDEX idx_event_time (event_time),
  INDEX idx_event_type (event_type)
);
```

### 8.2 CARE API Contract (Expected from CARE Backend)

The CARE backend **shall** provide the following endpoints. This is the contract this enabler depends on.

#### Worklist Response Schema
```json
{
  "status": "success",
  "results": [
    {
      "service_request": {
        "id": "string (CARE internal ID)",
        "external_id": "string (e.g. SR-FAC-2026-001)",
        "name": "string (procedure name)",
        "date": "ISO 8601 datetime",
        "modality": "string (DICOM modality code, e.g. CT)",
        "study_instance_uid": "string (optional; DICOM UID)"
      },
      "facility": {
        "id": "string",
        "name": "string"
      },
      "patient": {
        "id": "string (CARE patient ID)",
        "fhir_id": "string (FHIR Patient logical ID, optional)",
        "name": "string",
        "birth_date": "YYYY-MM-DD (preferred over age)",
        "age": "integer (fallback if birth_date absent)",
        "gender": "male | female | other | unknown",
        "phone_number": "string",
        "address": "string"
      },
      "referring_physician": {
        "name": "string",
        "fhir_id": "string (optional)"
      }
    }
  ]
}
```

#### Upload Response Schema
```json
{
  "status": "success",
  "study_uid": "string (DICOM Study Instance UID)",
  "fhir_imaging_study_id": "string (FHIR ImagingStudy ID, optional)",
  "message": "string"
}
```

---

## 9. Non-Functional Requirements

### 9.1 Performance

| Requirement | Target |
|-------------|--------|
| C-FIND response time (end-to-end with cached worklist) | < 200 ms |
| C-FIND response time (with live backend query) | < 2 s |
| C-STORE throughput | ≥ 10 simultaneous associations without queuing |
| C-STORE latency (disk write + DB record) | < 1 s per instance |
| Upload throughput (CARE HTTP POST) | ≥ 5 instances/second aggregate |
| MPPS response time | < 500 ms |
| Management API response time | < 200 ms (95th percentile) |
| Memory footprint (all services combined) | < 512 MB at steady state |

### 9.2 Reliability

| Requirement | Target |
|-------------|--------|
| Service availability | 99.5% (measured over 30-day rolling window) |
| Worklist served from cache when CARE is down | Yes, indefinitely with stale-cache warning |
| Images accepted when CARE upload is down | Yes — queued locally, uploaded on reconnect |
| MPPS accepted when CARE is down | Yes — queued and retried |
| Data loss on service restart | Zero — no in-flight data held only in memory |
| Graceful shutdown | In-progress C-STORE operations complete before shutdown |

### 9.3 Security

| Requirement | Detail |
|-------------|--------|
| No hardcoded secrets | Encryption key, API token, DB password must come from environment or secret manager |
| AES key management | 32-byte random key, per-deployment; random IV per encryption operation |
| SQL injection prevention | All database queries **shall** use parameterised statements |
| DICOM TLS | Available for all inbound ports (see FR-TLS-01) |
| HTTPS for all outbound HTTP | `careBaseUrl` and `fhirBaseUrl` **shall** require HTTPS in production mode |
| Minimum TLS version | TLS 1.2 (TLS 1.3 preferred) |
| API authentication | Management API endpoints (write operations) **shall** require a valid JWT |
| PHI in logs | Patient names, IDs, and dates **shall not** appear in Serilog operational logs; only in the audit log table |
| Dependency scanning | CI pipeline **shall** include dependency vulnerability scanning |

### 9.4 Cross-Platform

| Requirement | Detail |
|-------------|--------|
| Target platforms | Windows 10/11, Windows Server 2019+, Ubuntu 20.04+, Debian 11+, Docker (Linux container) |
| Runtime | .NET 8 LTS (target framework `net8.0`) |
| Path handling | All file paths constructed with `Path.Combine()` and `Path.DirectorySeparatorChar` |
| No Windows-only APIs | `ServiceBase`, `ServiceController`, `EventLog`, `Registry`, WinForms — all removed |
| Service lifecycle | Managed by .NET Generic Host with `UseSystemd()` (Linux) and `UseWindowsService()` (Windows) |
| Docker image | Official `mcr.microsoft.com/dotnet/runtime:8.0` base; no Windows base image |

### 9.5 Observability

| Requirement | Detail |
|-------------|--------|
| Structured logging | All log output in structured JSON format (Serilog with JSON sink) |
| Log levels | `TRACE`, `DEBUG`, `INFO`, `WARN`, `ERROR` — configurable per service |
| Correlation IDs | Each DICOM association assigned a UUID; propagated to all log entries and API calls |
| Health endpoint | `/healthz/live` and `/healthz/ready` (see FR-MGT-02) |
| Metrics | Prometheus `/metrics` endpoint (see FR-MGT-03) |
| Distributed tracing | OpenTelemetry trace context propagated to outbound CARE/FHIR HTTP calls |

### 9.6 Maintainability

| Requirement | Detail |
|-------------|--------|
| Dependency injection | All services use constructor injection via Microsoft DI container |
| No static mutable state | No `static` fields holding database connections or shared mutable objects |
| Async/await throughout | No `.Result` or `.Wait()` blocking calls on async operations |
| Unit test coverage | Core business logic (DICOM–FHIR mapping, worklist filtering, retry logic) ≥ 80% line coverage |
| Integration tests | CI pipeline includes DICOM end-to-end test (C-ECHO, C-FIND, C-STORE) as currently implemented |

---

## 10. Constraints and Assumptions

### 10.1 Constraints

- The system must remain compatible with DICOM-conformant modalities; changes to SOP class lists or transfer syntax acceptance must maintain backward compatibility with equipment already deployed.
- The CARE backend must provide the worklist API response with a `modality` field per item. Without this, all worklist items default to the configured `careModality` value.
- FHIR mode requires CARE to expose a FHIR R4 endpoint. If CARE does not expose FHIR, `backend_mode = care_rest` must be used.
- MPPS notification to CARE requires CARE to implement the `PATCH .../status/` endpoint defined in FR-CARE-05/06. Until that endpoint exists, MPPS is stored locally but not forwarded.

### 10.2 Assumptions

- Hospital network permits TCP connections on ports 2007, 2008, and 2009 from modalities to the machine running the enabler.
- The machine running the enabler has outbound HTTPS access to the CARE backend.
- DICOM modalities at the facility support at minimum C-ECHO and C-STORE; C-FIND support on the device side is required for worklist use.
- For FHIR mode: the CARE FHIR server is reachable from the enabler and implements FHIR R4 search for `ServiceRequest` and CRUD for `ImagingStudy`.

---

## 11. Requirement Traceability Matrix

| Requirement ID | Category | Priority | Current State | Blocks Migration |
|---------------|----------|----------|--------------|-----------------|
| FR-MWL-01–12 | DICOM MWL | Must-Have | Partial (FR-MWL-06, 07, 09 broken) | No |
| FR-SCP-01–10 | DICOM StoreSCP | Must-Have | Partial (FR-SCP-04, 08 broken) | No |
| FR-MPPS-01–04 | DICOM MPPS | Must-Have | Not implemented | No |
| FR-SCU-01–06 | DICOM SCU | Must-Have | Partial (FR-SCU-03, 05 broken) | No |
| FR-TLS-01–03 | DICOM TLS | Should-Have | Not implemented | No |
| FR-CARE-01–09 | CARE Integration | Must-Have | Partial (FR-CARE-05, 06 missing) | No |
| FR-FHIR-01–12 | FHIR R4 | Should-Have | Not implemented | No |
| FR-MGT-01–06 | Management API | Must-Have | Not implemented | **Yes** (WinForms replacement) |
| FR-CFG-01 | Configuration | Must-Have | Partial (env vars missing) | **Yes** |
| FR-AUDIT-01–02 | Audit Log | Should-Have | Partial (file only) | No |
| NFR — Cross-Platform | Platform | Must-Have | Fails (Windows-only) | **Yes** |
| NFR — Security | Security | Must-Have | Fails (hardcoded key, SQL injection) | **Yes** |
| NFR — Performance | Performance | Should-Have | Untested | No |
| NFR — Reliability | Reliability | Must-Have | Partial (no retry, no cache fallback on outage) | No |

### Priority Definitions
- **Must-Have**: System cannot be used in production without this
- **Should-Have**: Required for compliance or full workflow; can be deferred one sprint
- **Nice-to-Have**: Improves quality or future extensibility; can be deferred

### Items Blocking Cross-Platform Migration
The following must be completed before the system can run on Linux or in Docker:

1. **FR-CFG-01** — environment variable configuration (replace `App.config` and `EncKey.cs`)
2. **FR-MGT-01 to FR-MGT-06** — management REST API (replaces WinForms GUI)
3. **NFR — Cross-Platform** — path handling, removal of `ServiceController`, removal of `EventLog`
4. **NFR — Security** — move encryption key out of source; fix SQL injection (FR-SCP-08)
5. **FR-SCU-05** — `async Task` (fix `async void` before migration amplifies concurrency)

---

**Document Version**: 2.0  
**Prepared by**: CARE Development Team  
**Review status**: Draft — requires sign-off from CARE platform team and biomedical engineering lead
