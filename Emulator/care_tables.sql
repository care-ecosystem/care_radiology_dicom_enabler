-- CARE integration tables used by the DICOM Enabler services.
-- Run via Initializer.ps1 / Initializer.bat after schema.sql. Safe to re-run: tables use IF NOT EXISTS.
USE `plexus_mi2`;

-- Local copy of the CARE worklist, one row per accession number.
CREATE TABLE IF NOT EXISTS `care_worklist` (
  `pk` bigint(20) NOT NULL AUTO_INCREMENT,
  `accession_number` varchar(64) NOT NULL,
  `status` varchar(20) NOT NULL DEFAULT 'SCHEDULED',
  `service_request_id` varchar(64) DEFAULT NULL,
  `service_request_external_id` varchar(64) DEFAULT NULL,
  `service_request_name` varchar(255) DEFAULT NULL,
  `service_request_date` datetime DEFAULT NULL,
  `service_request_body_site` text DEFAULT NULL,
  `service_request_description` text DEFAULT NULL,
  `service_request_modality` varchar(16) DEFAULT NULL,
  `service_request_procedure_id` varchar(64) DEFAULT NULL,
  `service_request_priority` varchar(32) DEFAULT NULL,
  `service_request_technician_instruction` text DEFAULT NULL,
  `service_request_patient_instruction` text DEFAULT NULL,
  `created_by_prefix` varchar(64) DEFAULT NULL,
  `created_by_first_name` varchar(255) DEFAULT NULL,
  `created_by_last_name` varchar(255) DEFAULT NULL,
  `facility_id` varchar(64) DEFAULT NULL,
  `facility_name` varchar(255) DEFAULT NULL,
  `patient_id` varchar(64) DEFAULT NULL,
  `patient_external_id` varchar(64) DEFAULT NULL,
  `patient_name` varchar(255) DEFAULT NULL,
  `patient_address` text DEFAULT NULL,
  `patient_phone_number` varchar(32) DEFAULT NULL,
  `patient_gender` varchar(16) DEFAULT NULL,
  `patient_age` int(11) DEFAULT NULL,
  `patient_uhid` varchar(64) DEFAULT NULL,
  `created_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`pk`),
  UNIQUE KEY `uq_care_worklist_accession_number` (`accession_number`),
  KEY `idx_care_worklist_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Outcome of each DICOM file upload to CARE, one row per file. A failed file stays in the SCP
-- folder and is retried, so a retry updates the same row with the latest status and log and
-- increments retry_count (0 on the first attempt). failure_count does not count HTTP 429 (rate
-- limiting), HTTP 401, 403, any 5xx or network errors. After maxUploadFailures counted failures the
-- file is moved to FailedSCP\<dd-MM-yyyy>; after maxUploadRetries retries of any kind it is moved to
-- FailedSCP\OtherFailure\<dd-MM-yyyy> (both limits are set in CARE_SCU_Service App.config).
CREATE TABLE IF NOT EXISTS `care_study_upload` (
  `pk` bigint(20) NOT NULL AUTO_INCREMENT,
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
  KEY `idx_care_study_upload_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
