# CARE Radiology DICOM Enabler

## Project Overview

The CARE Radiology DICOM Enabler (formerly Plexus DICOM Enabler) is a comprehensive Windows-based healthcare imaging integration platform built on .NET Framework 4.7.2. It provides a complete DICOM (Digital Imaging and Communications in Medicine) networking solution that bridges medical imaging modalities (CT, MRI, X-Ray machines) with healthcare information systems.

**Built with:** Visual Studio 2019

## Purpose

This enterprise-grade solution enables:
- **DICOM Communication** between medical imaging devices and healthcare IT systems
- **Modality Worklist Services (MWL SCP)** for delivering scheduled patient information to imaging equipment
- **Image Reception (Store SCP)** for receiving DICOM images from modalities
- **Image Upload (Store SCU)** for transmitting images to remote DICOM servers
- **Multi-Server Management** with database-driven configuration
- **User Authentication** with periodic credential validation

## Integration with CARE Healthcare Service

The CARE Radiology DICOM Enabler is a critical bridge component in the CARE Healthcare ecosystem, connecting Windows-based medical imaging devices to the modern Django-based CARE EMR (Electronic Medical Records) system.

### Overall Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│ Hospital Network (Windows Environment)                                          │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                   │
│  Medical Imaging Devices (CT, MRI, X-Ray, Ultrasound)                           │
│         │                                 │                                       │
│         │ C-FIND (Worklist Query)        │ C-STORE (Send Images)                │
│         │ Port 2008                       │ Port 2007                            │
│         ↓                                 ↓                                       │
│  ┌──────────────────────────┐   ┌──────────────────────────┐                   │
│  │ CARE_MWL_Service         │   │ CARE_StoreSCP_Service    │                   │
│  │ Modality Worklist SCP    │   │ DICOM Image Receiver     │                   │
│  │ AE: MODALITYSCP          │   │ AE: STORAGESCP           │                   │
│  │ Port: 2008               │   │ Port: 2007               │                   │
│  └──────────────────────────┘   └──────────────────────────┘                   │
│         │                                 │                                       │
│         │ HTTP GET (Static API Key)      │ Saves to ./SCP folder                │
│         │ /dicom/worklist/                │                                       │
│         ↓                                 ↓                                       │
│  ┌────────────────────────────────────────────────────────────┐                │
│  │ CARE_SCU_Service (Store SCU - Uploader)                    │                │
│  │ • Monitors ./SCP folder (every 5 seconds)                  │                │
│  │ • Uploads DICOM files to Django backend                    │                │
│  └────────────────────────────────────────────────────────────┘                │
│         │                                                                         │
│         │ HTTP POST (JWT Token)                                                  │
│         │ /dicom/upload/ (multipart/form-data)                                  │
│         ↓                                                                         │
└─────────┼─────────────────────────────────────────────────────────────────────┘
          │ HTTPS
          ↓
┌─────────────────────────────────────────────────────────────────────────────────┐
│ Cloud/On-Premise CARE Backend (Django/Python)                                   │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                   │
│  ┌───────────────────────────────────────────────────────────────────────────┐ │
│  │ CARE Radiology Plugin (care_radiology)                                    │ │
│  │ Plugin-based Django REST API                                              │ │
│  ├───────────────────────────────────────────────────────────────────────────┤ │
│  │ API Endpoints:                                                            │ │
│  │ • GET  /api/plugin/care_radiology/dicom/worklist/     ← MWL Service     │ │
│  │ • POST /api/plugin/care_radiology/dicom/upload/       ← SCU Service     │ │
│  │ • GET  /api/plugin/care_radiology/dicom/studies/      ← Web UI          │ │
│  │ • POST /api/plugin/care_radiology/webhooks/study/     ← DCM4CHEE        │ │
│  │ • POST /api/plugin/care_radiology/study_report/       ← Radiologist UI  │ │
│  │ • GET  /api/plugin/care_radiology/study-report-audits/ ← Compliance     │ │
│  └───────────────────────────────────────────────────────────────────────────┘ │
│         │                          │                        │                     │
│         │                          │                        │                     │
│         ↓                          ↓                        ↓                     │
│  ┌───────────┐            ┌───────────┐          ┌─────────────────────────┐   │
│  │PostgreSQL │            │   Redis   │          │ DCM4CHEE PACS           │   │
│  │ Metadata  │            │   Cache   │          │ (Archive 5.34.1)        │   │
│  │ - Patient │            │ - Studies │          ├─────────────────────────┤   │
│  │ - Service │            │ - Auth    │          │ • STOW-RS (Upload)      │   │
│  │   Request │            └───────────┘          │ • QIDO-RS (Query)       │   │
│  │ - Reports │                                   │ • WADO-RS (Retrieve)    │   │
│  └───────────┘                                   │ • OHIF Viewer (Web)     │   │
│                                                   │ • MinIO S3 Storage      │   │
│                                                   └─────────────────────────┘   │
│                                                                                   │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Technology Stack Comparison

| Component | Windows DICOM Enabler | CARE Backend | PACS |
|-----------|----------------------|--------------|------|
| **Language** | C# (.NET 4.7.2) | Python 3.13 | Java (WildFly) |
| **Framework** | Windows Forms | Django 6.0 + DRF | DCM4CHEE 5.34.1 |
| **DICOM Library** | fo-dicom 5.0.2 | pydicom 3.0.2 | dcm4che |
| **Database** | MySQL 8.0 | PostgreSQL | PostgreSQL + LDAP |
| **Storage** | Local filesystem | S3 (MinIO) | MinIO S3 |
| **Authentication** | Static API Key + JWT | JWT + RBAC | Nginx proxy auth |
| **UI** | MaterialSkin 2.3.1 | React | OHIF Viewer v3.9.2 |

### Key Integration Points

#### 1. Worklist API Integration
**Windows Service:** `CARE_MWL_Service` (Port 2008)
**Django Endpoint:** `GET /api/plugin/care_radiology/dicom/worklist/`
**File:** `Sample_ModalitySCP/Model/WorklistItemsProvider.cs:230+`

**How it works:**
1. Medical device (CT/MRI) sends DICOM C-FIND request to port 2008
2. MWL Service receives request with modality filter (e.g., modality="CT")
3. Service makes HTTP GET to Django backend with Static API Key authentication
4. Django queries `ServiceRequest` model for active orders matching criteria
5. Returns JSON with patient demographics, exam details, scheduled date/time
6. MWL Service converts JSON to DICOM worklist format
7. Returns DICOM C-FIND response to imaging device

**Request:**
```http
GET /api/plugin/care_radiology/dicom/worklist/?modality=CT&from=2026-05-14&to=2026-05-15
Authorization: <STATIC_API_KEY>
```

**Response:**
```json
{
  "status": "success",
  "results": [
    {
      "service_request": {
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "name": "CT Chest with Contrast",
        "date": "2026-05-14T10:30:00Z"
      },
      "facility": {
        "id": "660e8400-e29b-41d4-a716-446655440001",
        "name": "Central Hospital"
      },
      "patient": {
        "name": "John Doe",
        "address": "123 Main St",
        "phone_number": "+1234567890",
        "gender": "M",
        "age": 45
      }
    }
  ]
}
```

**Code Reference:** `src/care_radiology/api/dicom.py:85-107`

#### 2. DICOM Image Upload Integration
**Windows Service:** `CARE_SCU_Service` (Uploader)
**Django Endpoint:** `POST /api/plugin/care_radiology/dicom/upload/`
**File:** `CARE_SCU_Service/Plexus_SCU_Service.cs`

**How it works:**
1. Store SCP Service receives images and saves to `./SCP/{StudyUID}/{SeriesUID}/{InstanceUID}.dcm`
2. SCU Service monitors `./SCP` folder every 5 seconds
3. For each DICOM file, SCU creates multipart/form-data HTTP POST
4. Django receives DICOM file, extracts metadata using pydicom
5. Django re-encodes as multipart/related and forwards to DCM4CHEE via STOW-RS
6. DCM4CHEE stores in MinIO S3 and returns StudyInstanceUID
7. Django creates/updates `DicomStudy` record linking patient to study
8. Django returns success response to Windows service
9. Windows service deletes local file after successful upload

**Request:**
```http
POST /api/plugin/care_radiology/dicom/upload/
Authorization: Bearer <JWT_TOKEN>
Content-Type: multipart/form-data

Form Data:
  - patient_id: 550e8400-e29b-41d4-a716-446655440000
  - file: [DICOM binary data]
```

**Response (201 Created):**
```json
{
  "status": "success",
  "message": "DICOM uploaded successfully",
  "study_uid": "1.2.840.113619.2.55.3.2609.2.1.1",
  "dicom_response": {
    "00081199": {
      "Value": [{
        "00081155": {"Value": ["1.2.840.113619..."]}
      }]
    }
  }
}
```

**Code Reference:** `src/care_radiology/api/dicom.py:110-223`

#### 3. DCM4CHEE PACS Integration
**Communication:** Django Backend ↔ DCM4CHEE
**Protocol:** DICOMweb (REST API over HTTP)
**Base URL:** `http://arc:8080/dcm4chee-arc/rs/`

**STOW-RS (Store):**
```http
POST /rs/studies HTTP/1.1
Content-Type: multipart/related; type="application/dicom"; boundary=DICOMBOUNDARY-{uuid}
Accept: application/dicom+json

--DICOMBOUNDARY-{uuid}
Content-Type: application/dicom
Content-Length: 123456

[DICOM binary data]
--DICOMBOUNDARY-{uuid}--
```

**QIDO-RS (Query):**
```http
GET /rs/studies?StudyInstanceUID=1.2.840.113619.2.55.3.2609.2.1.1
Accept: application/dicom+json
```

**WADO-RS (Retrieve):**
```http
GET /rs/studies/{StudyUID}/series/{SeriesUID}/instances/{InstanceUID}/frames/1
Accept: image/jpeg
```

**Code Reference:** `src/care_radiology/api/dicom.py:396-479`

#### 4. Study Completion Webhook
**Source:** DCM4CHEE PACS
**Django Endpoint:** `POST /api/plugin/care_radiology/webhooks/study/`
**Authentication:** Static API Key

**How it works:**
1. DCM4CHEE configured with webhook URL pointing to Django
2. When study finalized (all series uploaded), DCM4CHEE triggers webhook
3. Django receives StudyInstanceUID + ServiceRequest ID
4. Creates `RadiologyServiceRequest` junction record linking service request to DICOM study
5. Logs webhook in `RadiologyWebhookLogs` for audit
6. Updates `DicomStudy` with metadata from DCM4CHEE
7. Busts Redis cache for study

**Request:**
```http
POST /api/plugin/care_radiology/webhooks/study/
Authorization: <STATIC_API_KEY>
Content-Type: application/json

{
  "service_request_id": "550e8400-e29b-41d4-a716-446655440000",
  "study_id": "1.2.840.113619.2.55.3.2609.2.1.1"
}
```

**Response (200 OK):**
```json
{
  "detail": "Webhook received and saved successfully",
  "record": {
    "external_id": "770e8400-e29b-41d4-a716-446655440002",
    "data": { ... }
  }
}
```

**Code Reference:** `src/care_radiology/api/webhooks.py:31-116`

### Complete Radiology Workflow

#### End-to-End Process Flow

```
1. ORDER PLACEMENT (Django Backend)
   Doctor creates ServiceRequest via CARE EMR UI
   └─ POST /api/facility/{facility_id}/service_request/
   └─ Fields: title, code (SNOMED CT), body_site, patient, encounter, healthcare_service
   └─ Status: "draft" → "active"

2. WORKLIST QUERY (Windows MWL Service)
   Imaging device queries for patient worklist
   └─ C-FIND request to port 2008
   └─ MWL Service → GET /dicom/worklist/?modality=CT
   └─ Returns patient demographics + exam details
   └─ Device displays worklist to technician

3. IMAGE ACQUISITION (Medical Device)
   Technician selects patient from worklist
   └─ Performs CT/MRI/X-Ray scan
   └─ Device generates DICOM images
   └─ Embeds patient ID, Study UID, Service Request ID

4. IMAGE TRANSMISSION (Windows Store SCP)
   Device sends images to CARE_StoreSCP_Service
   └─ C-STORE request to port 2007
   └─ Service validates AE Title
   └─ Saves to ./SCP/{StudyUID}/{SeriesUID}/{InstanceUID}.dcm
   └─ Updates MySQL database with metadata

5. IMAGE UPLOAD (Windows Store SCU)
   CARE_SCU_Service monitors ./SCP folder
   └─ Detects new files every 5 seconds
   └─ POST /dicom/upload/ (multipart/form-data)
   └─ Django receives and validates JWT token
   └─ Django → DCM4CHEE STOW-RS upload
   └─ Success → deletes local file

6. PACS STORAGE (DCM4CHEE)
   DCM4CHEE stores DICOM in MinIO S3
   └─ Indexes metadata in PostgreSQL
   └─ Triggers webhook → POST /webhooks/study/
   └─ Django creates DicomStudy + RadiologyServiceRequest records

7. IMAGE VIEWING (Web UI)
   Radiologist opens OHIF viewer in browser
   └─ GET /api/plugin/care_radiology/dicom/studies/?patient_id={id}
   └─ Returns list of studies with has_report annotation
   └─ OHIF loads images via WADO-RS from DCM4CHEE

8. REPORT CREATION (Django Backend)
   Radiologist dictates/types report
   └─ POST /api/plugin/care_radiology/study_report/
   └─ Fields: study, modality, body_part, technique, findings, impression
   └─ Creates StudyReport record
   └─ Automatically creates StudyReportAudit for HIPAA compliance

9. REPORT APPROVAL (Django Backend)
   Senior radiologist reviews and approves
   └─ PATCH /api/plugin/care_radiology/study_report/{id}/
   └─ Updates status to "final"
   └─ Audit trail tracks all changes (old_value → new_value)

10. ORDER COMPLETION (Django Backend)
    System updates ServiceRequest status
    └─ PATCH /api/facility/{id}/service_request/{id}/
    └─ Status: "active" → "completed"
    └─ Referring physician notified
```

### Database Models and Relationships

#### CARE Backend (Django/PostgreSQL)

**Core EMR Models:**
```python
Patient
├── external_id: UUID (primary key)
├── name, gender, date_of_birth
├── phone_number, address
└── instance_identifiers: JSONField (MRN, etc.)

Encounter
├── external_id: UUID
├── patient: FK(Patient)
├── facility: FK(Facility)
├── status: draft|active|completed
└── encounter_class: ambulatory|emergency|inpatient

ServiceRequest (Radiology Order)
├── external_id: UUID
├── patient: FK(Patient)
├── encounter: FK(Encounter)
├── title: "CT Chest with Contrast"
├── code: JSONField (SNOMED CT coding)
├── body_site: JSONField (anatomical location)
├── status: draft|active|completed|cancelled
├── healthcare_service: FK(HealthcareService)
├── activity_definition: FK(ActivityDefinition)
└── requester: FK(User)
```

**Radiology Plugin Models:**
```python
DicomStudy (Links Patient to DICOM)
├── external_id: UUID
├── patient: FK(Patient)
├── dicom_study_uid: CharField (DICOM StudyInstanceUID)
├── UNIQUE (patient, dicom_study_uid)
└── Annotated: has_report (Exists subquery)

RadiologyServiceRequest (Junction Table)
├── external_id: UUID
├── service_request: FK(ServiceRequest)
├── dicom_study: FK(DicomStudy)
└── raw_data: JSONField (webhook payload)

StudyReport
├── external_id: UUID
├── study: FK(DicomStudy)
├── modality: FK(ModalityType)
├── body_part: FK(BodyPart)
├── scan_protocol: FK(ScanProtocol)
├── technique: TextField (imaging parameters)
├── findings: TextField (clinical observations)
├── impression: TextField (radiologist conclusion)
├── created_datetime: DateTimeField
└── last_modified_datetime: DateTimeField(auto_now)

StudyReportAudit (HIPAA Compliance)
├── external_id: UUID
├── study_report: FK(StudyReport)
├── action: "Created"|"Updated"
├── field_name: CharField
├── old_value: JSONField
├── new_value: JSONField
└── created_datetime: DateTimeField

ModalityType (CT, MRI, X-Ray, Ultrasound)
├── external_id: UUID
├── display_name: CharField
└── coding: JSONField (HL7/SNOMED codes)

BodyPart (Chest, Abdomen, Head, etc.)
├── external_id: UUID
├── display_name: CharField
├── modality_types: M2M(ModalityType)
└── coding: JSONField (SNOMED CT codes)

ScanProtocol (Specific protocols per modality/body part)
├── external_id: UUID
├── modality_type: FK(ModalityType)
├── body_part: FK(BodyPart)
├── display_name: CharField
└── default_parameters: JSONField

RadiologyWebhookLogs (Audit Trail)
├── external_id: UUID
├── webhook_type: CharField (e.g., "SR-STUDY-INSERT")
├── payload: JSONField
└── created_datetime: DateTimeField
```

#### Windows Enabler (MySQL)

```sql
-- Study table
study
├── id: INT AUTO_INCREMENT
├── study_uid: VARCHAR(500)
├── patient_id: INT
├── service_request_id: VARCHAR(100)
├── study_date: DATE
├── study_time: TIME
├── modality_codes: VARCHAR(100)
├── num_instances: INT
└── created_at: TIMESTAMP

-- Series table
series
├── id: INT AUTO_INCREMENT
├── series_uid: VARCHAR(500)
├── study_id: INT (FK to study)
├── modality: VARCHAR(20)
├── series_number: INT
└── num_instances: INT

-- Instance table
instance
├── id: INT AUTO_INCREMENT
├── sop_instance_uid: VARCHAR(500)
├── series_id: INT (FK to series)
├── instance_number: INT
├── file_path: VARCHAR(500)
└── upload_status: ENUM('pending','success','failed')

-- Server configuration
servers
├── id: INT AUTO_INCREMENT
├── ae_title: VARCHAR(100)
├── host: VARCHAR(255)
├── port: INT
├── description: VARCHAR(500)
└── is_active: BOOLEAN
```

### Authentication & Authorization

#### Windows Enabler Authentication
1. **Static API Key** (Worklist endpoint)
   - Header: `Authorization: <STATIC_API_KEY>`
   - Configured via `CARE_RADIOLOGY_WEBHOOK_SECRET` env var
   - Used by: MWL Service for worklist queries

2. **JWT Token** (Upload endpoint)
   - Header: `Authorization: Bearer <JWT_TOKEN>`
   - Obtained from: Django `/api/token/` endpoint
   - Contains: user_id, username, exp, facility context
   - Used by: SCU Service for image uploads

3. **Periodic Validation** (Auth Service)
   - External API: `https://{CARE_BACKEND}/users/login-api`
   - Frequency: Every 24 hours
   - Action on failure: Stops MWL/SCP/SCU services

#### Django Backend Authorization (RBAC)

**Permission Checks:**
```python
# Patient-level permissions
can_read_patient_obj(user, patient)
can_write_patient_obj(user, patient)

# Service Request permissions
can_read_service_request(user, service_request)
can_write_service_request_in_encounter(user, encounter)
can_list_location_service_request(user, location)

# Radiology Report permissions
can_read_radiology_report(user, study_report)
can_write_radiology_report(user, study_report)
```

**Authorization Scopes:**
- **Facility-Wide:** User role within facility organization
- **Location-Specific:** Limited to specific departments (e.g., Radiology)
- **Encounter-Specific:** Access tied to patient encounters
- **Resource-Specific:** Explicit ownership or care team membership

### Configuration Management

#### Windows Enabler (App.config)
```xml
<appSettings>
  <!-- CARE Backend Integration -->
  <add key="careBackendURL" value="https://care.hospital.org" />
  <add key="authURL" value="https://care.hospital.org/api/token/" />
  <add key="worklistURL" value="/api/plugin/care_radiology/dicom/worklist/" />
  <add key="uploadURL" value="/api/plugin/care_radiology/dicom/upload/" />

  <!-- Static API Key -->
  <add key="staticAPIKey" value="your-secret-api-key-here" />

  <!-- JWT Token (if required) -->
  <add key="jwtToken" value="" />

  <!-- Service Configuration -->
  <add key="mwlaetitle" value="MODALITYSCP" />
  <add key="mwlport" value="2008" />
  <add key="sscpaetitle" value="STORAGESCP" />
  <add key="sscpport" value="2007" />

  <!-- Upload Schedule -->
  <add key="scuTimerInterval" value="5000" />  <!-- milliseconds -->

  <!-- Deployment Mode -->
  <add key="deployType" value="1" />  <!-- 1=Hospital, 2=Central -->
</appSettings>
```

#### Django Backend (.env)
```bash
# PostgreSQL Database
POSTGRES_HOST=localhost
POSTGRES_DB=care
POSTGRES_USER=care_user
POSTGRES_PASSWORD=secure_password

# Redis Cache
REDIS_URL=redis://localhost:6379/0

# DCM4CHEE PACS
CARE_RADIOLOGY_DCM4CHEE_DICOMWEB_BASEURL=http://arc:8080/dcm4chee-arc/aets/DCM4CHEE
CARE_RADIOLOGY_DCM4CHEE_DICOMWEB_AUTH_TYPE=none  # or jwt
DCM4CHEE_WEBHOOK_SECRET=your-webhook-secret

# Static API Key for Windows Enabler
CARE_RADIOLOGY_WEBHOOK_SECRET=your-static-api-key

# OHIF Viewer
OHIF_VIEWER_URL=http://localhost:3000

# MinIO S3 Storage
MINIO_ENDPOINT=minio:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin
MINIO_BUCKET_NAME=dicom-bucket

# Celery (Async Tasks)
CELERY_BROKER_URL=redis://localhost:6379/1
```

### Performance & Scalability

#### Redis Caching Strategy
```python
# Cache DICOM study metadata (1 hour TTL)
cache_key = f"radiology:dicom:study:{study_uid}"
cache.set(cache_key, study_data, timeout=3600)

# Cache worklist queries (5 minutes TTL)
cache_key = f"radiology:worklist:{modality}:{date}"
cache.set(cache_key, worklist_data, timeout=300)

# Invalidation on upload
cache.delete(f"radiology:dicom:study:{study_uid}")
```

#### Async Processing with Celery
```python
# Background task for DICOM metadata extraction
@shared_task
def extract_dicom_metadata(study_uid):
    # Query DCM4CHEE for full study metadata
    # Update DicomStudy record with series/instance counts
    # Generate thumbnails
    pass

# Background task for report generation from templates
@shared_task
def generate_report_from_template(study_id, template_id):
    # Apply template to findings/impression
    # Notify radiologist
    pass
```

#### Parallel Study Metadata Fetching
```python
from concurrent.futures import ThreadPoolExecutor, as_completed

with ThreadPoolExecutor(max_workers=10) as executor:
    future_to_study = {
        executor.submit(fetch_dcm4chee_metadata, study): study
        for study in studies
    }
    for future in as_completed(future_to_study):
        result = future.result()
```

### Security Considerations

#### Data Encryption
- **Windows Enabler:** BouncyCastle.Crypto for connection strings
- **Django Backend:** Encrypted fields via django-encrypted-model-fields (if configured)
- **In Transit:** TLS 1.3 for all HTTP communication
- **At Rest:** MinIO server-side encryption (SSE-S3)

#### HIPAA Compliance
- **Audit Trails:** `StudyReportAudit` tracks all report modifications
- **Webhook Logs:** `RadiologyWebhookLogs` for complete event history
- **Access Control:** Role-based with facility/location scoping
- **PHI Protection:** Patient data never logged in plaintext

#### Network Security
- **Firewall Rules:** DICOM ports (2007, 2008) limited to hospital network
- **API Gateway:** Nginx reverse proxy with rate limiting
- **Authentication:** Multi-factor (JWT + static key) for critical endpoints
- **Secrets Management:** Environment variables, never hardcoded

## Architecture

### Solution Structure

The solution (`CARE_DICOM_Enabler.sln`) consists of 10 projects:

#### Core Application
- **Plexus_DICOM_Enabler** - Main WinForms application with Material Design UI
  - Entry point: `Program.cs:18`
  - Main form: `frm_Mainform.cs`
  - Login screen: `Forms/frm_LoginScreen.cs`

#### Windows Services
1. **CARE_Auth_Service** (`Plexus_Auth_Service`)
   - Authenticates user credentials periodically (every 24 hours)
   - Stops other services if authentication fails
   - Controls: MWL SCP, Store SCP, and Store SCU services

2. **CARE_MWL_Service** (`Plexus_MWL_Service`)
   - Modality Worklist SCP (Service Class Provider)
   - Provides patient lists to modalities via DICOM C-FIND
   - Supports three data sources: local list, MySQL database, remote CARE API
   - Default: AE Title "MODALITYSCP", Host 127.0.0.1, Port 2008

3. **CARE_StoreSCP_Service** (`Plexus_StoreSCP_Service`)
   - Store SCP service for receiving DICOM images
   - Implements DICOM C-STORE operations
   - Stores images locally in archive folders
   - Default: AE Title "STORAGESCP", Host 127.0.0.1, Port 2007
   - Implementation: `Network/CStoreSCP.cs`

4. **CARE_SCU_Service** (`Plexus_StoreSCU_Service`)
   - Store SCU (Service Class User) for uploading images
   - Periodically uploads received images to configured remote servers
   - Reads server list from MySQL database

#### Libraries & Utilities
- **CARE.DAL** (`Plexus.Common`)
  - Data Access Layer for MySQL operations
  - Encryption/decryption utilities (`EnDcryption.cs`)
  - Configuration management
  - User validation (`cls_UserDetail.cs`)
  - File: `ucls_DAL.cs`

- **GenerateConnectionString**
  - Utility for generating encrypted database connection strings
  - Tests database connectivity before saving
  - Saves to `config/common.cfg` (XML format)
  - Includes encryption/decryption tools

#### Unit Test Applications
- **Sample_ModalitySCP** - Tests Modality SCP functionality
- **Sample_Store_SCP** - Tests Store SCP and SCU operations
- **Test_SeriLog** - Tests SeriLog logging library
- **Plexus_FileDeleteApp** - Console app for deleting archive folders and files

## Technology Stack

### Frameworks
- **.NET Framework 4.7.2**
- **Windows Forms** with **MaterialSkin 2.3.1** (Material Design theme)

### DICOM Networking
- **fo-dicom 5.0.2** - Fellow Oak DICOM library (primary DICOM framework)
- **fo-dicom.Codecs 5.1.0** - Codec support for image compression
- Supported transfer syntaxes: JPEG-LS, JPEG2000, RLE, JPEG, uncompressed

### Database
- **MySql.Data 8.0.29** - MySQL .NET driver
- Connection string: `Server=localhost;Database=plexus_mi2;Uid=root;Pwd=<password>;`
- Encrypted storage in XML configuration files

### Logging
- **Serilog 2.11.0** - Structured logging framework
- **Serilog.Sinks.File 5.0.0** - File logging
- **Serilog.Sinks.Console 4.0.1** - Console logging

### Security & Encryption
- **BouncyCastle.Crypto 1.8.5** - Cryptography library
- Custom encryption for sensitive data (connection strings, credentials)

### Other Dependencies
- **Google.Protobuf 3.19.4** - Protocol Buffer serialization
- **System.Text.Json 6.0.5** - JSON serialization
- **K4os.Compression.LZ4 1.2.6** - Compression
- **Microsoft.Bcl.AsyncInterfaces 6.0.0** - Async utilities
- **System.Threading.Tasks.Extensions 4.5.4** - Threading extensions

## Configuration Files

### App.config (Main Application)
Located at: `App.config`

Key settings:
```xml
<appSettings>
  <!-- Modality Worklist SCP Settings -->
  <add key="mwlaetitle" value="MODALITYSCP" />
  <add key="mwlhost" value="127.0.0.1" />
  <add key="mwlport" value="2008" />

  <!-- Store SCP Settings -->
  <add key="sscpaetitle" value="STORAGESCP" />
  <add key="sscphost" value="127.0.0.1" />
  <add key="sscpport" value="2007" />

  <!-- Database Connection -->
  <add key="connectionstring" value="Server=localhost;Database=plexus_mi2;Uid=root;Pwd=inzin@123;" />

  <!-- Authentication API -->
  <add key="authURL" value="https://dev.plexusemr.com:8443/plx-api/users/login-api" />

  <!-- Deployment Type: 1=Client (Hospital), 2=Server (Central) -->
  <add key="deployType" value="1" />

  <add key="deviceName" value="Plexus" />
  <add key="checkserver" value="true" />
</appSettings>
```

### config/common.cfg
- Encrypted XML file containing database connection strings
- Generated using GenerateConnectionString utility
- Read by services using `cls_PlexusConfig.cs:` in StoreSCP service

## Main Application Features

The WinForms UI (`frm_Mainform.cs`) provides tabbed interface with:

1. **Server Manager**
   - Install/Uninstall Windows services
   - Start/Stop backend services
   - Control: `UserControls/uctrl_ServerManager.cs`

2. **SCP Settings**
   - Configure Modality SCP settings
   - Configure Store SCP settings
   - Control: `UserControls/uctrl_SCPSettings.cs`

3. **SCU Settings**
   - Configure Store SCU upload details
   - Set destination servers and timing

4. **Server List**
   - Add/Edit/Delete DICOM server endpoints
   - Database-driven server configuration
   - Manage multiple DICOM nodes for communication

5. **View Patient List**
   - Display patient information
   - Shows DICOM transactions (MWL/SCP/Upload)

6. **View Logs**
   - View DICOM communication logs
   - Separate logs for MWL, Store SCP, and Store SCU operations

7. **About Us**
   - Application information and version details

## DICOM Operations Supported

- **C-FIND** - Modality Worklist queries
- **C-STORE** - Image reception and transmission
- **C-ECHO** - Service verification/ping

## Deployment Model

### Deployment Types (deployType setting)
1. **Client Mode (deployType=1)** - Deployed at hospitals/clinics
   - Receives images from local modalities
   - Uploads to central servers

2. **Server Mode (deployType=2)** - Deployed at central locations
   - Acts as central image archive
   - Receives from multiple client installations

### Installation Steps
1. Build solution in Visual Studio 2019
2. Install NuGet packages (see below)
3. Configure database connection using GenerateConnectionString utility
4. Update `App.config` with appropriate AE titles, hosts, and ports
5. Install Windows services using service installers
6. Configure DICOM servers in Server List
7. Start services via Server Manager UI

## Required NuGet Packages

```
fo-dicom 5.0.2
fo-dicom.Codecs 5.1.0
Microsoft.Bcl.AsyncInterfaces 6.0.0
Serilog 2.11.0
Serilog.Sinks.File 5.0.0
System.Buffers 4.5.1
System.Numerics.Vectors 4.5.0
System.Runtime.CompilerServices.Unsafe 6.0.0
System.Text.Encodings.Web 6.0.0
System.Text.Json 6.0.5
System.Threading.Tasks.Extension 4.5.4
System.ValueTuple 4.5.0
MySql.Data 8.0.29
MaterialSkin 2.3.1
BouncyCastle.Crypto 1.8.5
Google.Protobuf 3.19.4
K4os.Compression.LZ4 1.2.6
```

## Directory Structure

```
/
├── CARE.DAL/                      # Data Access Layer library
├── CARE_Auth_Service/             # Authentication Windows Service
├── CARE_MWL_Service/             # Modality Worklist SCP Service
├── CARE_SCU_Service/             # Store SCU Upload Service
├── CARE_StoreSCP_Service/        # Store SCP Reception Service
│   └── Network/
│       └── CStoreSCP.cs          # DICOM Store provider implementation
├── Forms/                         # Application forms
│   └── frm_LoginScreen.cs        # Login screen
├── UserControls/                  # Reusable UI controls
│   ├── uctrl_LoginForm.cs
│   ├── uctrl_SCPSettings.cs
│   └── uctrl_ServerManager.cs
├── GenerateConnectionString/      # Connection string utility
├── Sample_ModalitySCP/           # MWL SCP unit tests
├── Sample_Store_SCP/             # Store SCP/SCU unit tests
├── Test_SeriLog/                 # SeriLog unit tests
├── config/                        # Configuration files
│   └── common.cfg                # Encrypted connection string
├── Images/                        # UI images and icons
├── bin/                          # Build outputs
├── obj/                          # Intermediate objects
├── Program.cs                    # Application entry point
├── frm_Mainform.cs              # Main application form
├── Global.cs                     # Global variables
├── App.config                    # Application configuration
├── packages.config               # NuGet package manifest
├── CARE_DICOM_Enabler.csproj    # Main project file
├── CARE_DICOM_Enabler.sln       # Visual Studio solution
└── README.md                     # Project documentation
```

## Key Classes and Files

### Entry Points
- `Program.cs:18` - Application Main() method
- Sets `Global._applicationPath` from `Application.ExecutablePath`
- Launches `frm_LoginScreen` for authentication

### Authentication Flow
1. User enters credentials in `frm_LoginScreen`
2. Validates against stored credentials (database or config)
3. On success, opens `frm_Mainform`
4. `CARE_Auth_Service` periodically validates (every 24 hours)
5. Stops services if authentication fails

### DICOM Image Flow
1. **Reception**: Modality sends C-STORE → `CARE_StoreSCP_Service` receives → Stores locally
2. **Upload**: `CARE_SCU_Service` monitors archive → Uploads to configured servers → Marks as completed

### Configuration Management
- `CARE.DAL/ucls_DAL.cs` - Database operations
- `CARE.DAL/EnDcryption.cs` - Encrypt/decrypt sensitive data
- `CARE_StoreSCP_Service/config/cls_PlexusConfig.cs` - Read XML config

## Development Guidelines

### Building the Solution
1. Open `CARE_DICOM_Enabler.sln` in Visual Studio 2019
2. Restore NuGet packages
3. Build each project individually or entire solution
4. Output binaries in `/bin/Debug` or `/bin/Release`

### Testing
- Use unit test applications in solution
- `Sample_ModalitySCP` - Test MWL queries
- `Sample_Store_SCP` - Test C-STORE operations
- Validate with actual DICOM modalities or simulators

### Security Considerations
- Database credentials encrypted using BouncyCastle
- Connection strings stored in encrypted XML format
- User authentication required for UI access
- Service-level authentication with periodic validation
- Sensitive data never logged in plaintext

### Logging
- Structured logs via Serilog
- File-based logging with rotation
- Separate logs for each service
- View logs via "View Logs" tab in UI

## Target Users

- Hospital IT administrators
- Radiology department staff
- Healthcare system integrators
- DICOM network operators
- Medical imaging technicians

## Integration Points

### Data Sources (Modality Worklist)
1. Local patient list
2. MySQL database tables
3. Remote CARE system API (`authURL` configuration)

### DICOM Nodes
- Configurable via Server List UI
- Stored in MySQL database
- Multiple servers supported
- Each with AE Title, Host, Port, Description

### External Systems
- Remote CARE EMR system for authentication
- Central PACS/VNA systems for image archival
- RIS systems for worklist data

## Troubleshooting

### Common Issues
1. **Service won't start**: Check Windows Event Log, verify ports not in use
2. **Database connection fails**: Use GenerateConnectionString utility to test
3. **DICOM communication errors**: Verify AE titles, hosts, ports match on both ends
4. **Authentication fails**: Check authURL and network connectivity
5. **Images not uploading**: Verify server list configuration and network access

### Log Locations
- Check application directory for Serilog output files
- Windows Event Viewer for service errors
- View Logs tab in UI for DICOM transaction logs

---

## Local Development Setup Guide

This comprehensive guide will help you set up the complete CARE Radiology stack on your local machine for development and testing.

### Prerequisites

#### System Requirements
- **Operating System:** Windows 10/11 (for DICOM Enabler) + macOS/Linux (for Django backend)
- **RAM:** Minimum 16GB (recommended 32GB for smooth operation)
- **Disk Space:** 50GB+ free space
- **Network:** Stable internet connection for downloading dependencies

#### Required Software

**For Django Backend (macOS/Linux):**
- Python 3.13+
- Docker Desktop 24.0+
- Docker Compose 2.20+
- PostgreSQL 15+ (or use Docker)
- Redis 7.0+ (or use Docker)
- Git
- Make (build tool)
- Node.js 18+ (for OHIF viewer configuration)

**For Windows DICOM Enabler:**
- Windows 10/11 Pro
- Visual Studio 2019 (Community Edition or higher)
- .NET Framework 4.7.2 SDK
- MySQL 8.0+ (or MariaDB 10.6+)
- Git for Windows

**For Testing:**
- DICOM emulator tools (see DICOM Emulator Setup section)

---

### Part 1: Django CARE Backend Setup

#### Step 1.1: Clone Repositories

```bash
# Create project directory
mkdir -p ~/care-projects
cd ~/care-projects

# Clone main CARE repository
git clone https://github.com/ohcnetwork/care.git
cd care

# Clone radiology plugin inside care directory
git clone https://github.com/10bedicu/care_radiology.git
```

#### Step 1.2: Configure Plugin

Create or edit `plug_config.py` in the care root directory:

```python
# plug_config.py
from plugs.manager import Plug

care_radiology_plugin = Plug(
    name="care_radiology",
    package_name="/app/care_radiology",  # Local development path
    version="",  # Empty for local development
    configs={
        # DCM4CHEE DICOMweb API base URL
        "DCM4CHEE_DICOMWEB_BASEURL": "http://arc:8080/dcm4chee-arc/aets/DCM4CHEE",

        # Webhook secret for DCM4CHEE callbacks
        "WEBHOOK_SECRET": "your-random-secret-key-here",
    },
)

# Export plugs list
plugs = [care_radiology_plugin]
```

#### Step 1.3: Enable Editable Plugin Installation

Edit `plugs/manager.py` to install plugin in editable mode:

```python
# Find the subprocess.check_call line and add -e flag
subprocess.check_call(
    [sys.executable, "-m", "pip", "install", "-e", *packages]  # Added -e flag
)
```

#### Step 1.4: Configure Environment Variables

Create `.env` file in care root directory:

```bash
# Database Configuration
POSTGRES_HOST=localhost
POSTGRES_DB=care
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_PORT=5432

# Redis Configuration
REDIS_URL=redis://localhost:6379/0
CELERY_BROKER_URL=redis://localhost:6379/1

# Django Settings
DJANGO_DEBUG=True
DJANGO_SECRET_KEY=your-django-secret-key-change-in-production
DJANGO_ALLOWED_HOSTS=["*"]

# S3 / MinIO (for file uploads)
BUCKET_PROVIDER=aws
BUCKET_REGION=ap-south-1
BUCKET_KEY=minioadmin
BUCKET_SECRET=minioadmin
BUCKET_ENDPOINT=http://localhost:9100
FILE_UPLOAD_BUCKET=patient-bucket
FACILITY_S3_BUCKET=facility-bucket

# Radiology Plugin Configuration
CARE_RADIOLOGY_DCM4CHEE_DICOMWEB_BASEURL=http://localhost:8080/dcm4chee-arc/aets/DCM4CHEE
CARE_RADIOLOGY_WEBHOOK_SECRET=your-static-api-key-for-windows-enabler

# OHIF Viewer URL
OHIF_VIEWER_URL=http://localhost:3000

# Optional: Sentry for error tracking
# SENTRY_DSN=
```

#### Step 1.5: Update Makefile for Radiology Services

Edit `Makefile` in care root to include radiology docker-compose:

```makefile
# Add this line near the top
RADIOLOGY_DOCKER_COMPOSE := ./care_radiology/docker-compose.radiology.yaml

# Modify up target
up:
	docker compose -f docker-compose.yaml -f $(RADIOLOGY_DOCKER_COMPOSE) up -d --wait

# Modify down target
down:
	docker compose -f docker-compose.yaml -f $(RADIOLOGY_DOCKER_COMPOSE) down
```

#### Step 1.6: Start Docker Services

```bash
# Start PostgreSQL, Redis, MinIO, and Radiology stack
make up

# This will start:
# - PostgreSQL (port 5432)
# - Redis (port 6379)
# - MinIO (port 9100)
# - OpenLDAP (port 3890)
# - DCM4CHEE Archive (port 8080)
# - OHIF Viewer (port 3000)
# - Nginx Proxy (port 32314)
```

#### Step 1.7: Setup DCM4CHEE Database

```bash
# Navigate to dcm4che directory
cd care_radiology/docker/dcm4che

# Create DICOM database in PostgreSQL
docker exec -it care-db-1 psql -U postgres -c "CREATE DATABASE dicom;"

# Run database setup scripts
make setup-dicom-db

# This will create:
# - DICOM tables (study, series, instance, patient, etc.)
# - Foreign key constraints
# - Case-insensitive indexes for efficient queries
```

#### Step 1.8: Configure DCM4CHEE Storage (MinIO)

```bash
# Create MinIO bucket for DICOM storage
docker exec -it care-minio-1 bash
mc alias set local http://localhost:9000 minioadmin minioadmin
mc mb local/dicom-bucket
mc policy set public local/dicom-bucket
exit

# Import LDAP configuration for MinIO storage
cd care_radiology/docker/dcm4che

# Edit bucketconfig.ldif to match your setup (if needed)
# Then import into LDAP
make ldap-setup
# When prompted, enter password: admin
```

**bucketconfig.ldif example:**
```ldif
dn: dcmStorageID=minio,dicomDeviceName=dcm4chee-arc,cn=Devices,cn=DICOM Configuration,dc=dcm4che,dc=org
changetype: modify
replace: dcmURI
dcmURI: jclouds:s3:http://host.docker.internal:9100
-
replace: dcmProperty
dcmProperty: jclouds.access-key-id=minioadmin
dcmProperty: jclouds.secret-key=minioadmin
dcmProperty: jclouds.s3.bucket-name=dicom-bucket
dcmProperty: jclouds.s3.path-style-access=true
```

#### Step 1.9: Configure OHIF Viewer

Edit `care_radiology/docker/ohif/app-config.js`:

```javascript
window.config = {
  routerBasename: '/',
  extensions: [],
  modes: [],

  // CRITICAL: These URLs must be accessible from browser
  dataSources: [
    {
      namespace: '@ohif/extension-default.dataSourcesModule.dicomweb',
      sourceName: 'dicomweb',
      configuration: {
        friendlyName: 'DCM4CHEE',
        name: 'DCM4CHEE',

        // For local development, use localhost:32314 (nginx proxy)
        wadoUriRoot: 'http://localhost:32314/dicomweb/dcm4chee-arc/aets/DCM4CHEE/wado',
        qidoRoot: 'http://localhost:32314/dicomweb/dcm4chee-arc/aets/DCM4CHEE/rs',
        wadoRoot: 'http://localhost:32314/dicomweb/dcm4chee-arc/aets/DCM4CHEE/rs',

        qidoSupportsIncludeField: false,
        imageRendering: 'wadors',
        thumbnailRendering: 'wadors',
        enableStudyLazyLoad: true,
        supportsFuzzyMatching: true,
        supportsWildcard: true,
      },
    },
  ],
};
```

**Important:** After editing, restart OHIF container:
```bash
docker compose -f docker-compose.radiology.yaml restart ohif
```

#### Step 1.10: Run Django Migrations

```bash
# Go back to care root
cd ~/care-projects/care

# Create Python virtual environment
python3 -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt

# Run migrations
python manage.py migrate

# Create superuser
python manage.py createsuperuser
# Enter username, email, password when prompted
```

#### Step 1.11: Start Django Development Server

```bash
# Start Django server
python manage.py runserver 0.0.0.0:9000

# In another terminal, start Celery worker (for background tasks)
celery -A config.celery_app worker --loglevel=info
```

#### Step 1.12: Verify Django Setup

Open browser and test endpoints:

```bash
# Django Admin
http://localhost:9000/admin

# API Root
http://localhost:9000/api/

# Radiology Plugin Endpoints
http://localhost:9000/api/plugin/care_radiology/

# DCM4CHEE Management UI
http://localhost:8080/dcm4chee-arc/ui2

# OHIF Viewer
http://localhost:3000
```

**Create test data:**
```bash
# In Django shell
python manage.py shell

from care.emr.models import Patient, Facility
from care.facility.models import FacilityOrganization

# Create facility
facility = Facility.objects.create(name="Test Hospital")

# Create patient
patient = Patient.objects.create(
    name="John Doe",
    gender="M",
    date_of_birth="1980-01-01"
)
```

---

### Part 2: Windows DICOM Enabler Setup

#### Step 2.1: Clone Windows Repository

On your Windows machine:

```powershell
# Create project directory
New-Item -ItemType Directory -Path C:\care-projects
cd C:\care-projects

# Clone repository
git clone https://github.com/your-org/care_radiology_dicom_enabler.git
cd care_radiology_dicom_enabler
```

#### Step 2.2: Install Visual Studio 2019

1. Download Visual Studio 2019 Community Edition
2. During installation, select:
   - **.NET desktop development**
   - **.NET Framework 4.7.2 SDK**
   - **NuGet package manager**
3. Complete installation and restart

#### Step 2.3: Install MySQL Server

```powershell
# Download MySQL 8.0 Installer from https://dev.mysql.com/downloads/installer/

# During installation:
# - Choose "Server only" or "Custom"
# - Set root password (e.g., "root123")
# - Configure MySQL to start on boot
# - Default port: 3306
```

Create DICOM enabler database:

```sql
-- Open MySQL Workbench or command line
mysql -u root -p

CREATE DATABASE plexus_mi2 CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Create tables
USE plexus_mi2;

CREATE TABLE study (
    id INT AUTO_INCREMENT PRIMARY KEY,
    study_uid VARCHAR(500) NOT NULL,
    patient_id INT,
    service_request_id VARCHAR(100),
    study_date DATE,
    study_time TIME,
    modality_codes VARCHAR(100),
    num_instances INT DEFAULT 0,
    upload_status ENUM('pending', 'success', 'failed') DEFAULT 'pending',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_study_uid (study_uid),
    INDEX idx_upload_status (upload_status)
);

CREATE TABLE series (
    id INT AUTO_INCREMENT PRIMARY KEY,
    series_uid VARCHAR(500) NOT NULL,
    study_id INT,
    modality VARCHAR(20),
    series_number INT,
    num_instances INT DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (study_id) REFERENCES study(id) ON DELETE CASCADE,
    INDEX idx_series_uid (series_uid)
);

CREATE TABLE instance (
    id INT AUTO_INCREMENT PRIMARY KEY,
    sop_instance_uid VARCHAR(500) NOT NULL,
    series_id INT,
    instance_number INT,
    file_path VARCHAR(500),
    upload_status ENUM('pending', 'success', 'failed') DEFAULT 'pending',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (series_id) REFERENCES series(id) ON DELETE CASCADE,
    INDEX idx_instance_uid (sop_instance_uid),
    INDEX idx_upload_status (upload_status)
);

CREATE TABLE servers (
    id INT AUTO_INCREMENT PRIMARY KEY,
    ae_title VARCHAR(100) NOT NULL,
    host VARCHAR(255) NOT NULL,
    port INT NOT NULL,
    description VARCHAR(500),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Insert default Django backend server
INSERT INTO servers (ae_title, host, port, description, is_active)
VALUES ('CAREBACKEND', 'localhost', 9000, 'Django CARE Backend', TRUE);
```

#### Step 2.4: Open Solution in Visual Studio

1. Double-click `CARE_DICOM_Enabler.sln`
2. Visual Studio will open with all 10 projects loaded
3. Wait for NuGet package restore to complete

#### Step 2.5: Restore NuGet Packages

```powershell
# In Visual Studio Package Manager Console
Update-Package -reinstall

# Or right-click solution → "Restore NuGet Packages"
```

Required packages (should auto-install):
- fo-dicom 5.0.2
- fo-dicom.Codecs 5.1.0
- MySql.Data 8.0.29
- Serilog 2.11.0
- Serilog.Sinks.File 5.0.0
- MaterialSkin 2.3.1
- BouncyCastle.Crypto 1.8.5

#### Step 2.6: Configure App.config

Edit `App.config` in main project:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <appSettings>
    <!-- Modality Worklist SCP Settings -->
    <add key="mwlaetitle" value="MODALITYSCP" />
    <add key="mwlhost" value="0.0.0.0" />  <!-- Listen on all interfaces -->
    <add key="mwlport" value="2008" />

    <!-- Store SCP Settings -->
    <add key="sscpaetitle" value="STORAGESCP" />
    <add key="sscphost" value="0.0.0.0" />
    <add key="sscpport" value="2007" />

    <!-- MySQL Database Connection -->
    <add key="connectionstring" value="Server=localhost;Database=plexus_mi2;Uid=root;Pwd=root123;" />

    <!-- Django CARE Backend URLs -->
    <add key="careBackendURL" value="http://localhost:9000" />
    <add key="authURL" value="http://localhost:9000/api/token/" />
    <add key="worklistURL" value="/api/plugin/care_radiology/dicom/worklist/" />
    <add key="uploadURL" value="/api/plugin/care_radiology/dicom/upload/" />

    <!-- Static API Key for Worklist (matches Django WEBHOOK_SECRET) -->
    <add key="staticAPIKey" value="your-static-api-key-for-windows-enabler" />

    <!-- JWT Token (leave empty, will be obtained via login) -->
    <add key="jwtToken" value="" />

    <!-- SCU Upload Timer (milliseconds) -->
    <add key="scuTimerInterval" value="5000" />  <!-- Upload every 5 seconds -->

    <!-- Deployment Type: 1=Client (Hospital), 2=Server (Central) -->
    <add key="deployType" value="1" />

    <!-- Device Name -->
    <add key="deviceName" value="CARE-DICOM-Enabler" />

    <!-- Check server connectivity before operations -->
    <add key="checkserver" value="true" />
  </appSettings>
</configuration>
```

#### Step 2.7: Generate Encrypted Connection String

1. Build and run `GenerateConnectionString` project
2. Enter database details:
   - Server: `localhost`
   - Database: `plexus_mi2`
   - Username: `root`
   - Password: `root123`
3. Click "Test Connection" - should show success
4. Click "Generate & Save"
5. Connection string saved to `config/common.cfg` (encrypted XML)

#### Step 2.8: Build Solution

```powershell
# In Visual Studio
# Build → Rebuild Solution
# Or press Ctrl+Shift+B

# Verify no errors in Output window
# All 10 projects should build successfully
```

Output directories:
- Main App: `bin/Debug/CARE_DICOM_Enabler.exe`
- Services: `CARE_*_Service/bin/Debug/*.exe`

#### Step 2.9: Install Windows Services

Open PowerShell as Administrator:

```powershell
cd C:\care-projects\care_radiology_dicom_enabler

# Install Auth Service
sc.exe create "CARE Auth Service" binPath= "C:\care-projects\care_radiology_dicom_enabler\CARE_Auth_Service\bin\Debug\CARE_Auth_Service.exe"

# Install MWL Service
sc.exe create "CARE MWL Service" binPath= "C:\care-projects\care_radiology_dicom_enabler\CARE_MWL_Service\bin\Debug\CARE_MWL_Service.exe"

# Install Store SCP Service
sc.exe create "CARE StoreSCP Service" binPath= "C:\care-projects\care_radiology_dicom_enabler\CARE_StoreSCP_Service\bin\Debug\CARE_StoreSCP_Service.exe"

# Install Store SCU Service
sc.exe create "CARE StoreSCU Service" binPath= "C:\care-projects\care_radiology_dicom_enabler\CARE_SCU_Service\bin\Debug\CARE_SCU_Service.exe"

# Verify installation
sc.exe query "CARE MWL Service"
```

**Alternative:** Use the WinForms UI:
1. Run `CARE_DICOM_Enabler.exe`
2. Login with credentials
3. Go to "Server Manager" tab
4. Click "Install All Services"
5. Click "Start All Services"

#### Step 2.10: Configure Windows Firewall

```powershell
# Allow DICOM ports through firewall
New-NetFirewallRule -DisplayName "DICOM MWL SCP" -Direction Inbound -LocalPort 2008 -Protocol TCP -Action Allow
New-NetFirewallRule -DisplayName "DICOM Store SCP" -Direction Inbound -LocalPort 2007 -Protocol TCP -Action Allow
```

#### Step 2.11: Start Services

```powershell
# Start services manually
net start "CARE Auth Service"
net start "CARE MWL Service"
net start "CARE StoreSCP Service"
net start "CARE StoreSCU Service"

# Check status
sc.exe query "CARE MWL Service"
```

Verify services in Windows Services (`services.msc`):
- All should show "Running" status
- Startup type: Automatic

#### Step 2.12: Configure Server List

1. Run `CARE_DICOM_Enabler.exe`
2. Login with admin credentials
3. Go to "Server List" tab
4. Add Django backend server:
   - **Name:** Django CARE Backend
   - **AE Title:** CAREBACKEND
   - **Host:** localhost
   - **Port:** 9000 (HTTP) - Note: For HTTP upload, use special handler
   - **Description:** Main CARE backend for DICOM upload
   - **Active:** Yes
5. Click "Save"

For DICOM upload, you might need to add DCM4CHEE directly:
   - **Name:** DCM4CHEE PACS
   - **AE Title:** DCM4CHEE
   - **Host:** localhost
   - **Port:** 11112 (DICOM port, not HTTP 8080)
   - **Description:** Direct DICOM upload to PACS
   - **Active:** Yes

---

### Part 3: DICOM Emulator Setup (For Testing)

When you don't have real medical devices, use DICOM emulators to test the complete workflow.

#### Option 1: DCMTK Tools (Recommended)

**Installation on Windows:**
```powershell
# Download DCMTK from https://dicom.offis.de/dcmtk
# Or use Chocolatey
choco install dcmtk
```

**Installation on macOS/Linux:**
```bash
# macOS
brew install dcmtk

# Ubuntu/Debian
sudo apt-get install dcmtk

# Verify installation
storescu --version
```

**Test Worklist Query (C-FIND):**
```bash
# Query worklist from MWL SCP
findscu -v -S -k 0008,0005=ISO_IR\ 100 \
    -k 0008,0050= \
    -k 0010,0010= \
    -k 0010,0020= \
    -k 0040,0100[0].0008,0060= \
    localhost 2008 \
    -aet TESTMODALITY \
    -aec MODALITYSCP

# Expected output: List of patients from Django worklist API
```

**Send Test DICOM Image (C-STORE):**
```bash
# First, get sample DICOM file
wget https://github.com/dcm4che/dcm4chee-arc-light/raw/master/dcm4chee-arc-cdi/src/test/resources/test.dcm

# Send to Store SCP
storescu -v localhost 2007 \
    -aet TESTMODALITY \
    -aec STORAGESCP \
    test.dcm

# Expected output: C-STORE response with success status
```

**Verify Echo (C-ECHO):**
```bash
# Test connectivity to Store SCP
echoscu -v localhost 2007 \
    -aet TESTMODALITY \
    -aec STORAGESCP

# Expected output: Association accepted, echo successful
```

#### Option 2: dcm4che Tools

```bash
# Download dcm4che from https://github.com/dcm4che/dcm4che
wget https://sourceforge.net/projects/dcm4che/files/dcm4che3/5.32.0/dcm4che-5.32.0-bin.zip
unzip dcm4che-5.32.0-bin.zip
cd dcm4che-5.32.0/bin

# Test worklist query
./findscu -c MODALITYSCP@localhost:2008 -m PatientName= -m PatientID=

# Send DICOM file
./storescu -c STORAGESCP@localhost:2007 test.dcm
```

#### Option 3: Weasis DICOM Viewer (GUI-based)

1. Download Weasis from https://weasis.org/
2. Install and launch
3. Configure DICOM nodes:
   - File → Preferences → DICOM
   - Add node: `MODALITYSCP` at `localhost:2008`
   - Add node: `STORAGESCP` at `localhost:2007`
4. Test:
   - Right-click node → C-ECHO to test connectivity
   - Right-click node → C-FIND to query worklist
   - Select files → Send to STORAGESCP

#### Option 4: Orthanc (Full PACS Emulator)

**Setup Orthanc:**
```bash
# Using Docker
docker run -p 4242:4242 -p 8042:8042 jodogne/orthanc

# Web UI: http://localhost:8042
# DICOM port: 4242
# Default credentials: orthanc / orthanc
```

**Configure Orthanc as Modality:**
1. Login to Orthanc web UI
2. Go to Configuration → Modalities
3. Add:
   - **Name:** CARE_DICOM
   - **AET:** STORAGESCP
   - **Host:** host.docker.internal (or your IP)
   - **Port:** 2007
4. Upload DICOM files via web UI
5. Select studies → Send to CARE_DICOM

#### Creating Test DICOM Files

**Use DCMTK to create from scratch:**
```bash
# Create test DICOM file
dump2dcm test.dump test.dcm

# test.dump content:
(0008,0005) CS [ISO_IR 100]
(0008,0016) UI =CTImageStorage
(0008,0018) UI [1.2.840.113619.2.55.3.2609.2.1.1]
(0008,0020) DA [20260514]
(0008,0030) TM [103000]
(0008,0050) SH [ACC123456]
(0008,0060) CS [CT]
(0008,0070) LO [GE MEDICAL SYSTEMS]
(0010,0010) PN [Test^Patient]
(0010,0020) LO [PAT123456]
(0010,0030) DA [19800101]
(0010,0040) CS [M]
(0020,000D) UI [1.2.840.113619.2.55.3.2609.2.1]
(0020,000E) UI [1.2.840.113619.2.55.3.2609.2.1.1]
(0020,0010) SH [STUDY001]
(0020,0011) IS [1]
(0020,0013) IS [1]
(0028,0010) US [512]
(0028,0011) US [512]
(0028,0100) US [16]
(0028,0101) US [16]
(0028,0102) US [15]
(0028,0103) US [0]
(7FE0,0010) OW [0000000... pixel data ...]
```

**Download Sample DICOM Files:**
```bash
# OsiriX Sample Data
wget https://www.osirix-viewer.com/resources/dicom-image-library/

# Rubo Medical Imaging Sample Data
https://github.com/NotAnonymousUser/Orthanc-medical-data

# TCIA (Cancer Imaging Archive)
https://www.cancerimagingarchive.net/
```

---

### Part 4: External Device Integration Guide

This section explains how to connect real medical imaging devices (CT, MRI, X-Ray machines) to the CARE DICOM Enabler.

#### Prerequisites for Device Integration

**Network Requirements:**
- Windows DICOM Enabler and medical device on same network (or routable)
- Fixed IP addresses for both systems (recommended)
- No firewalls blocking ports 2007, 2008
- Network latency < 50ms (preferred)

**Device Requirements:**
- DICOM-compliant imaging modality (CT, MRI, X-Ray, Ultrasound, etc.)
- Support for:
  - **C-FIND** (Modality Worklist query)
  - **C-STORE** (Image transmission)
  - **C-ECHO** (Connectivity test)
- Administrative access to device configuration

**Documentation Needed:**
- Device DICOM Conformance Statement
- Device AE Title (Application Entity Title)
- Device IP address
- Device DICOM port (usually 104 or custom)
- Supported transfer syntaxes

---

#### Step 4.1: Configure Windows DICOM Enabler

**1. Set Static IP on Windows Machine:**
```powershell
# Open Network Connections
ncpa.cpl

# Right-click network adapter → Properties
# Select "Internet Protocol Version 4 (TCP/IPv4)"
# Set static IP (e.g., 192.168.1.100)
# Subnet: 255.255.255.0
# Gateway: 192.168.1.1
# DNS: 8.8.8.8, 8.8.4.4
```

**2. Configure Firewall Rules:**
```powershell
# Allow inbound DICOM connections
New-NetFirewallRule -DisplayName "DICOM MWL SCP Port" -Direction Inbound -LocalPort 2008 -Protocol TCP -Action Allow -Profile Any

New-NetFirewallRule -DisplayName "DICOM Store SCP Port" -Direction Inbound -LocalPort 2007 -Protocol TCP -Action Allow -Profile Any

# Disable Windows Defender temporarily for testing (optional)
Set-MpPreference -DisableRealtimeMonitoring $true
```

**3. Update App.config with Public IP:**
```xml
<appSettings>
  <!-- Use 0.0.0.0 to listen on all interfaces -->
  <add key="mwlhost" value="0.0.0.0" />
  <add key="sscphost" value="0.0.0.0" />

  <!-- Or specify exact IP if multiple NICs -->
  <!-- <add key="mwlhost" value="192.168.1.100" /> -->
</appSettings>
```

**4. Restart Services:**
```powershell
net stop "CARE MWL Service"
net stop "CARE StoreSCP Service"
net start "CARE MWL Service"
net start "CARE StoreSCP Service"
```

---

#### Step 4.2: Device-Specific Configuration

**For GE Healthcare CT Scanner:**

1. Access service menu (usually F10 or hidden key combination during boot)
2. Login with service credentials
3. Navigate to: **System → DICOM → Worklist**
4. Add Worklist Server:
   - **Server Name:** CARE_MWL
   - **AE Title:** MODALITYSCP
   - **IP Address:** 192.168.1.100 (Windows machine IP)
   - **Port:** 2008
   - **Timeout:** 30 seconds
5. Navigate to: **System → DICOM → Storage**
6. Add Storage Destination:
   - **Destination Name:** CARE_STORAGE
   - **AE Title:** STORAGESCP
   - **IP Address:** 192.168.1.100
   - **Port:** 2007
   - **Transfer Syntax:** Explicit VR Little Endian (preferred)
   - **Auto-send:** Yes
7. Test connectivity:
   - **DICOM → Test → Echo to MODALITYSCP** - should succeed
   - **DICOM → Test → Echo to STORAGESCP** - should succeed
8. Save configuration and exit service menu

**For Siemens MRI Scanner:**

1. Access service mode (service key + service menu)
2. Navigate to: **Configuration → Network → DICOM**
3. Add Application Entity:
   - **Name:** CARE_Worklist
   - **Type:** Worklist SCP
   - **AE Title:** MODALITYSCP
   - **Hostname:** 192.168.1.100
   - **Port:** 2008
4. Add Stor age Entity:
   - **Name:** CARE_Archive
   - **Type:** Storage SCP
   - **AE Title:** STORAGESCP
   - **Hostname:** 192.168.1.100
   - **Port:** 2007
   - **Presentation Context:** CT Image Storage, MR Image Storage, etc.
5. Configure Workflow:
   - **Workflow → Worklist Query Settings**
   - **Query Server:** CARE_Worklist
   - **Auto-query on patient registration:** Yes
6. Configure Send:
   - **Data Transfer → Send Destination**
   - **Primary:** CARE_Archive
   - **Send Completed Studies:** Yes
   - **Send Mode:** Automatic
7. Perform C-ECHO test from device menu

**For Philips X-Ray:**

1. Login with admin credentials
2. Open: **Configuration → DICOM Settings**
3. Add Modality Worklist Server:
   - **Description:** CARE Worklist
   - **Application Entity:** MODALITYSCP
   - **Remote Host:** 192.168.1.100
   - **Remote Port:** 2008
   - **Local AE Title:** XRAY_01 (device's own AE title)
   - **Timeout:** 60 seconds
4. Add DICOM Export:
   - **Export Name:** Send to CARE
   - **Remote AE:** STORAGESCP
   - **Remote Host:** 192.168.1.100
   - **Remote Port:** 2007
   - **Local AE:** XRAY_01
   - **Auto-export:** Enabled
5. Test Connection:
   - **DICOM → Connectivity Test**
   - Ping CARE server - should respond
   - Echo to MODALITYSCP - should return success
   - Echo to STORAGESCP - should return success

**For Canon/Toshiba Ultrasound:**

1. Access setup menu (touchscreen: Settings → DICOM)
2. Configure Worklist:
   - **Worklist → Add Server**
   - **Server Name:** CARE
   - **AE Title:** MODALITYSCP
   - **IP:** 192.168.1.100
   - **Port:** 2008
   - **Character Set:** ISO_IR 100 (Western European)
3. Configure Storage:
   - **Send → Add Destination**
   - **Name:** CARE PACS
   - **AE Title:** STORAGESCP
   - **IP:** 192.168.1.100
   - **Port:** 2007
   - **Compression:** JPEG Lossy or None
4. Set as default:
   - **Worklist → Default Server:** CARE
   - **Send → Default Destination:** CARE PACS
5. Test from patient screen:
   - Click "Query Worklist"
   - Should show patients from Django CARE backend

---

#### Step 4.3: Network Verification

**Test network connectivity:**

```powershell
# From Windows machine, ping device
ping 192.168.1.50  # Replace with device IP

# From device (if accessible), ping Windows machine
ping 192.168.1.100

# Test port connectivity
Test-NetConnection -ComputerName 192.168.1.100 -Port 2007
Test-NetConnection -ComputerName 192.168.1.100 -Port 2008
```

**Verify services are listening:**
```powershell
# Check if ports are open
netstat -an | findstr "2007"
netstat -an | findstr "2008"

# Should show:
# TCP    0.0.0.0:2007    0.0.0.0:0    LISTENING
# TCP    0.0.0.0:2008    0.0.0.0:0    LISTENING
```

---

#### Step 4.4: Test Complete Workflow

**1. Create Test Patient in Django:**

```bash
# In Django shell
python manage.py shell

from care.emr.models import Patient, ServiceRequest, Facility
from care.facility.models import HealthcareService

facility = Facility.objects.first()

# Create patient
patient = Patient.objects.create(
    name="Test Patient for Device",
    gender="M",
    date_of_birth="1990-05-15",
    phone_number="+1234567890"
)

# Create radiology order
service = HealthcareService.objects.filter(internal_type="radiology").first()
if not service:
    service = HealthcareService.objects.create(
        facility=facility,
        name="Radiology Department",
        internal_type="radiology"
    )

service_request = ServiceRequest.objects.create(
    facility=facility,
    patient=patient,
    title="CT Chest with Contrast",
    status="active",
    healthcare_service=service,
    code={"coding": [{"system": "http://snomed.info/sct", "code": "77477000", "display": "CT of chest"}]},
    body_site={"coding": [{"system": "http://snomed.info/sct", "code": "51185008", "display": "Chest"}]}
)

print(f"Created patient: {patient.external_id}")
print(f"Created order: {service_request.external_id}")
```

**2. Query Worklist from Device:**
- On imaging device, open worklist query screen
- Click "Query" or "Search Patients"
- Should display: "Test Patient for Device" with CT Chest order
- Select patient from worklist

**3. Perform Imaging Study:**
- Complete imaging acquisition on device
- Device should show patient demographics from worklist
- Acquire images (test scan or actual procedure)

**4. Send Images to CARE:**
- Device should auto-send to STORAGESCP after completion
- Or manually trigger: "Send to CARE PACS"
- Monitor DICOM Enabler logs for C-STORE requests

**5. Verify in Django:**

```python
# Check if DICOM study created
from care_radiology.models import DicomStudy, RadiologyServiceRequest

studies = DicomStudy.objects.filter(patient=patient)
print(f"Found {studies.count()} studies")

for study in studies:
    print(f"Study UID: {study.dicom_study_uid}")
    print(f"Has report: {study.has_report}")
```

**6. View in OHIF:**
- Open: http://localhost:3000
- Should show study in study list
- Click to open in viewer
- Verify images display correctly

**7. Create Radiology Report:**
```bash
# Via Django API
curl -X POST http://localhost:9000/api/plugin/care_radiology/study_report/ \
  -H "Authorization: Bearer <JWT_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "study": "<study_external_id>",
    "modality": "<modality_type_id>",
    "body_part": "<body_part_id>",
    "technique": "CT scan with IV contrast. 5mm axial slices.",
    "findings": "No acute abnormality identified.",
    "impression": "Normal CT chest."
  }'
```

---

#### Step 4.5: Troubleshooting Device Integration

**Problem: Device cannot connect to worklist**

```powershell
# Check if MWL service is running
sc.exe query "CARE MWL Service"

# Check firewall
netsh advfirewall show allprofiles state

# Test with DCMTK from another machine
findscu -v localhost 2008 -aet TESTMODALITY -aec MODALITYSCP

# Check MWL service logs
type "C:\care-projects\care_radiology_dicom_enabler\CARE_MWL_Service\bin\Debug\logs\mwl_service.log"
```

**Problem: Device cannot send images**

```powershell
# Verify Store SCP is listening
netstat -an | findstr "2007"

# Test with storescu
storescu -v localhost 2007 -aet TEST -aec STORAGESCP test.dcm

# Check StoreSCP logs for incoming associations
type "C:\care-projects\care_radiology_dicom_enabler\CARE_StoreSCP_Service\bin\Debug\logs\storescp.log"
```

**Problem: Wrong AE Title**

Device shows: "Association rejected - calling AE title not recognized"

Solution:
- Verify device's AE Title matches configured in `App.config`
- Or add device's AE Title to allowed list
- Edit `CStoreSCP.cs` to accept any calling AE:
```csharp
// In OnCStoreRequestAsync method
// Comment out AE Title validation for testing
// if (callingAE != "EXPECTED_AE") return;
```

**Problem: Images not uploading to Django**

```powershell
# Check SCU service status
sc.exe query "CARE StoreSCU Service"

# Check SCU logs
type "C:\care-projects\care_radiology_dicom_enabler\CARE_SCU_Service\bin\Debug\logs\storescu.log"

# Verify Django is reachable
curl http://localhost:9000/api/plugin/care_radiology/

# Check JWT token is valid
# Update jwtToken in App.config if expired
```

**Problem: Transfer Syntax Not Supported**

Device shows: "No acceptable presentation contexts"

Solution:
- Check device's DICOM Conformance Statement for supported transfer syntaxes
- Update `CStoreSCP.cs` to accept device's transfer syntax:
```csharp
// In OnReceiveAssociationRequest method
pc.AcceptTransferSyntaxes(
    DicomTransferSyntax.ExplicitVRLittleEndian,
    DicomTransferSyntax.ImplicitVRLittleEndian,
    DicomTransferSyntax.JPEGProcess14SV1,  // Add if needed
    DicomTransferSyntax.JPEGLSLossless,
    DicomTransferSyntax.JPEG2000Lossless
);
```

---

### Part 5: Verification and Testing

**Complete Integration Test Checklist:**

- [ ] Django backend running on port 9000
- [ ] PostgreSQL database "care" created and migrated
- [ ] PostgreSQL database "dicom" created with DCM4CHEE schema
- [ ] Redis running on port 6379
- [ ] MinIO running on port 9100 with "dicom-bucket" created
- [ ] DCM4CHEE running on port 8080
- [ ] OHIF viewer accessible on port 3000
- [ ] Nginx proxy running on port 32314
- [ ] Windows DICOM Enabler services all "Running"
- [ ] MySQL database "plexus_mi2" created with tables
- [ ] Can query worklist via DCMTK tools
- [ ] Can send DICOM file via storescu
- [ ] Images appear in Django DicomStudy model
- [ ] Images viewable in OHIF viewer
- [ ] Can create study reports via API
- [ ] Medical device can query worklist
- [ ] Medical device can send images
- [ ] SCU service uploads to Django successfully

---

## License & Credits

Originally developed as Plexus DICOM Enabler, now maintained as CARE Radiology DICOM Enabler.

Built with fo-dicom (Fellow Oak DICOM) - industry-standard .NET DICOM library.

---

## Additional Resources

- **DICOM Standard:** https://www.dicomstandard.org/
- **fo-dicom Documentation:** https://fo-dicom.github.io/
- **DCM4CHEE Documentation:** https://github.com/dcm4che/dcm4chee-arc-light/wiki
- **OHIF Viewer:** https://docs.ohif.org/
- **Django CARE Backend:** https://care-be-docs.ohc.network/
- **DCMTK Tools:** https://support.dcmtk.org/docs/

For questions or issues, refer to the README.md file or contact the development team.
