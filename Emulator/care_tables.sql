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
-- increments retry_count (0 on the first attempt). After maxUploadRetries retries the file is moved
-- to FailedSCP\<dd-MM-yyyy> (the limit is set in care_config or CARE_SCU_Service App.config).
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
  `created_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`pk`),
  UNIQUE KEY `uq_care_study_upload_study_file` (`study_uid`, `file_name`),
  KEY `idx_care_study_upload_accession_number` (`accession_number`),
  KEY `idx_care_study_upload_status` (`status`),
  KEY `idx_care_study_upload_worklist_pk` (`worklist_pk`),
  CONSTRAINT `fk_care_study_upload_worklist` FOREIGN KEY (`worklist_pk`) REFERENCES `care_worklist` (`pk`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Integration settings for this enabler's facility, one row per setting, edited in the
-- Configuration tab. facility_id is required. For every other key a blank value means the
-- service uses its App.config value (or built-in default), so existing installs keep working
-- until a value is entered here. API keys and tokens stay in App.config.
CREATE TABLE IF NOT EXISTS `care_config` (
  `config_key` varchar(100) NOT NULL,
  `config_value` varchar(1000) DEFAULT NULL,
  `description` varchar(500) DEFAULT NULL,
  `updated_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`config_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- INSERT IGNORE adds keys missing from older installs without overwriting values already set.
INSERT IGNORE INTO `care_config` (`config_key`, `config_value`, `description`) VALUES
  ('facility_id', NULL, 'CARE Facility ID used to fetch the worklist and in MPPS updates. Required.'),
  ('care_modality', NULL, 'Modality filter for the CARE worklist. Blank = App.config careModality.'),
  ('care_from_date', NULL, 'Earliest worklist date fetched from CARE (yyyy-MM-dd HH:mm:ss). Blank = App.config careFromDate.'),
  ('scu_poll_interval_seconds', NULL, 'Seconds between scans of the SCP folder for files to upload. Blank = 5. Restart the SCU service to apply.'),
  ('worklist_refresh_start_seconds', NULL, 'Seconds after the MWL service starts before the first worklist refresh. Blank = App.config. Restart the MWL service to apply.'),
  ('worklist_refresh_interval_seconds', NULL, 'Seconds between worklist refreshes. Blank = App.config. Restart the MWL service to apply.'),
  ('max_upload_retries', NULL, 'Upload retries before a file is moved to the failed folder. Blank = App.config maxUploadRetries.'),
  ('scp_folder', NULL, 'Folder where received DICOM files are saved and picked up for upload. Blank = SCP under the install folder. Restart the services to apply.'),
  ('failed_scp_folder', NULL, 'Folder files are moved to after the upload retry limit is hit. Blank = FailedSCP under the install folder.');
