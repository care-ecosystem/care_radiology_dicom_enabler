-- CARE integration tables used by the DICOM Enabler services.
-- Run via Initializer.ps1 / Initializer.bat after schema.sql. Safe to re-run: tables use IF NOT EXISTS.
USE `plexus_mi2`;

-- CARE service requests from the worklist API (service_request object), one row per CARE
-- service request id. Refreshed with the latest values each time the worklist is fetched.
CREATE TABLE IF NOT EXISTS `care_service_request` (
  `pk` bigint(20) NOT NULL AUTO_INCREMENT,
  `service_request_id` varchar(64) NOT NULL,
  `name` varchar(255) DEFAULT NULL,
  `date` datetime DEFAULT NULL,
  `body_site` text DEFAULT NULL,
  `description` text DEFAULT NULL,
  `modality` varchar(16) DEFAULT NULL,
  `procedure_id` varchar(64) DEFAULT NULL,
  `priority` varchar(32) DEFAULT NULL,
  `technician_instruction` text DEFAULT NULL,
  `patient_instruction` text DEFAULT NULL,
  `created_by_prefix` varchar(64) DEFAULT NULL,
  `created_by_first_name` varchar(255) DEFAULT NULL,
  `created_by_last_name` varchar(255) DEFAULT NULL,
  `created_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`pk`),
  UNIQUE KEY `uq_care_service_request_id` (`service_request_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- CARE patients from the worklist API (patient object), one row per CARE patient id.
-- Refreshed with the latest values each time the worklist is fetched.
CREATE TABLE IF NOT EXISTS `care_patient` (
  `pk` bigint(20) NOT NULL AUTO_INCREMENT,
  `patient_id` varchar(64) NOT NULL,
  `name` varchar(255) DEFAULT NULL,
  `gender` varchar(16) DEFAULT NULL,
  `age` int(11) DEFAULT NULL,
  `patient_uhid` varchar(64) DEFAULT NULL,
  `created_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`pk`),
  UNIQUE KEY `uq_care_patient_id` (`patient_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Local copy of the CARE worklist, one row per accession number, linked to its service request
-- and patient.
CREATE TABLE IF NOT EXISTS `care_worklist` (
  `pk` bigint(20) NOT NULL AUTO_INCREMENT,
  `accession_number` varchar(64) NOT NULL,
  `status` varchar(20) NOT NULL DEFAULT 'SCHEDULED',
  `service_request_pk` bigint(20) DEFAULT NULL,
  `patient_pk` bigint(20) DEFAULT NULL,
  `facility_id` varchar(64) DEFAULT NULL,
  `facility_name` varchar(255) DEFAULT NULL,
  `created_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`pk`),
  UNIQUE KEY `uq_care_worklist_accession_number` (`accession_number`),
  KEY `idx_care_worklist_status` (`status`),
  KEY `idx_care_worklist_service_request_pk` (`service_request_pk`),
  KEY `idx_care_worklist_patient_pk` (`patient_pk`),
  CONSTRAINT `fk_care_worklist_service_request` FOREIGN KEY (`service_request_pk`) REFERENCES `care_service_request` (`pk`),
  CONSTRAINT `fk_care_worklist_patient` FOREIGN KEY (`patient_pk`) REFERENCES `care_patient` (`pk`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Outcome of each DICOM file upload to CARE, one row per file. A failed file stays in the SCP
-- folder and is retried, so a retry updates the same row with the latest status and log and
-- increments retry_count (0 on the first attempt). failure_count does not count HTTP 429 (rate
-- limiting), HTTP 401, 403, any 5xx or network errors. After maxUploadFailures counted failures the
-- file is moved to FailedSCP\<dd-MM-yyyy>; after maxUploadRetries retries of any kind it is moved to
-- FailedSCP\OtherFailure\<dd-MM-yyyy> (both limits are set in CARE_SCU_Service App.config).
-- worklist_pk links the file to the care_worklist row with its accession number; it stays NULL
-- when the file has no accession number or it is not in care_worklist.
CREATE TABLE IF NOT EXISTS `care_study_upload` (
  `pk` bigint(20) NOT NULL AUTO_INCREMENT,
  `worklist_pk` bigint(20) DEFAULT NULL,
  `study_uid` varchar(250) NOT NULL DEFAULT '',
  `accession_number` varchar(64) DEFAULT NULL,
  `file_name` varchar(255) NOT NULL,
  `status` varchar(20) NOT NULL,
  `log` text DEFAULT NULL,
  `retry_count` int(11) NOT NULL DEFAULT 0,
  `failure_count` int(11) NOT NULL DEFAULT 0,
  `created_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`pk`),
  UNIQUE KEY `uq_care_study_upload_study_file` (`study_uid`, `file_name`),
  KEY `idx_care_study_upload_accession_number` (`accession_number`),
  KEY `idx_care_study_upload_status` (`status`),
  KEY `idx_care_study_upload_worklist_pk` (`worklist_pk`),
  CONSTRAINT `fk_care_study_upload_worklist` FOREIGN KEY (`worklist_pk`) REFERENCES `care_worklist` (`pk`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
