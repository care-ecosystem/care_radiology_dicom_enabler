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

## License & Credits

Originally developed as Plexus DICOM Enabler, now maintained as CARE Radiology DICOM Enabler.

Built with fo-dicom (Fellow Oak DICOM) - industry-standard .NET DICOM library.

---

For questions or issues, refer to the README.md file or contact the development team.
