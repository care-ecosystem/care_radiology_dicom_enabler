-- CARE integration tables used by the DICOM Enabler services.
-- Run via Initializer.ps1 / Initializer.bat after schema.sql. Safe to re-run: tables use IF NOT EXISTS.
USE `plexus_mi2`;

-- CARE service requests from the worklist API (service_request object), one row per CARE
-- service request id. Refreshed with the latest values each time the worklist is fetched.
-- modality is not in the service_request object: it is the modality of the first DICOM device in
-- the result's locations; a result without one keeps the modality already stored. Each device's own
-- modality is in care_service_request_device. referring_physician is who raised the request.
CREATE TABLE IF NOT EXISTS `care_service_request` (
  `pk` bigint(20) NOT NULL AUTO_INCREMENT,
  `service_request_id` varchar(64) NOT NULL,
  `name` varchar(255) DEFAULT NULL,
  `date` datetime DEFAULT NULL,
  `body_site` text DEFAULT NULL,
  `body_site_id` varchar(64) DEFAULT NULL,
  `description` text DEFAULT NULL,
  `modality` varchar(16) DEFAULT NULL,
  `referring_physician` varchar(255) DEFAULT NULL,
  `procedure_id` varchar(64) DEFAULT NULL,
  `priority` varchar(32) DEFAULT NULL,
  `technician_instruction` text DEFAULT NULL,
  `patient_instruction` text DEFAULT NULL,
  `created_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`pk`),
  UNIQUE KEY `uq_care_service_request_id` (`service_request_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Locations and DICOM devices a CARE service request is scheduled on, from the worklist API
-- (locations array, each with its devices). One row per device, or one row with NULL device columns
-- for a location without devices. device_ae_title is the device's Scheduled Station AE Title
-- (0040,0001), not a calling AE title; device_modality is its modality. A C-FIND is answered with the
-- requests scheduled on a device of the modality and Scheduled Station AE Title it asks for.
-- A service request's rows are replaced each time the worklist is fetched.
CREATE TABLE IF NOT EXISTS `care_service_request_device` (
  `pk` bigint(20) NOT NULL AUTO_INCREMENT,
  `service_request_pk` bigint(20) NOT NULL,
  `location_id` varchar(64) DEFAULT NULL,
  `location_name` varchar(255) DEFAULT NULL,
  `device_id` varchar(64) DEFAULT NULL,
  `device_name` varchar(255) DEFAULT NULL,
  `device_ae_title` varchar(64) DEFAULT NULL,
  `device_modality` varchar(16) DEFAULT NULL,
  `created_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`pk`),
  KEY `idx_care_service_request_device_sr_pk` (`service_request_pk`),
  KEY `idx_care_service_request_device_device_id` (`device_id`),
  KEY `idx_care_service_request_device_modality` (`device_modality`),
  CONSTRAINT `fk_care_service_request_device_sr` FOREIGN KEY (`service_request_pk`) REFERENCES `care_service_request` (`pk`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Upgrade: columns added to tables created by an older version of this script. Each is added only
-- while it is missing, so re-running this script changes nothing.
DROP PROCEDURE IF EXISTS `care_add_column`;
DELIMITER //
CREATE PROCEDURE `care_add_column`(IN table_name_in VARCHAR(64), IN column_name_in VARCHAR(64), IN definition_in VARCHAR(500))
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.COLUMNS
                 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = table_name_in AND COLUMN_NAME = column_name_in) THEN
    SET @care_add_column_sql = CONCAT('ALTER TABLE `', table_name_in, '` ADD COLUMN `', column_name_in, '` ', definition_in);
    PREPARE care_add_column_stmt FROM @care_add_column_sql;
    EXECUTE care_add_column_stmt;
    DEALLOCATE PREPARE care_add_column_stmt;
  END IF;
END //
DELIMITER ;
CALL `care_add_column`('care_service_request', 'body_site_id', 'varchar(64) DEFAULT NULL AFTER `body_site`');
CALL `care_add_column`('care_service_request', 'referring_physician', 'varchar(255) DEFAULT NULL AFTER `modality`');
CALL `care_add_column`('care_service_request_device', 'device_ae_title', 'varchar(64) DEFAULT NULL AFTER `device_name`');
CALL `care_add_column`('care_service_request_device', 'device_modality', 'varchar(16) DEFAULT NULL AFTER `device_ae_title`');
DROP PROCEDURE IF EXISTS `care_add_column`;

SET @care_device_modality_index = (
  SELECT IF(COUNT(*) > 0, 'DO 0',
            'ALTER TABLE `care_service_request_device` ADD KEY `idx_care_service_request_device_modality` (`device_modality`)')
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'care_service_request_device' AND INDEX_NAME = 'idx_care_service_request_device_modality');
PREPARE care_device_modality_index_stmt FROM @care_device_modality_index;
EXECUTE care_device_modality_index_stmt;
DEALLOCATE PREPARE care_device_modality_index_stmt;

-- The DICOM devices (modality machines such as CT or X-Ray) connected to the selected DICOM Enabler,
-- one row per CARE device id, from GET .../external/modalities/?dicom_enabler={id}. Refreshed by the
-- Devices tab's Sync and by Start Service(s), only once the user confirms the changes found: rows are
-- inserted or updated with CARE's latest values, and the enabler's rows CARE no longer lists are deleted.
-- details is the device's full JSON as CARE returned it (from GET
-- .../external/modalities/{device_id}/ while it is unassigned, else its list entry), shown when the
-- device is clicked; the other columns are its summary.
CREATE TABLE IF NOT EXISTS `care_devices` (
  `pk` bigint(20) NOT NULL AUTO_INCREMENT,
  `device_id` varchar(64) NOT NULL,
  `facility_id` varchar(64) DEFAULT NULL,
  `dicom_enabler_id` varchar(64) DEFAULT NULL,
  `device_name` varchar(255) DEFAULT NULL,
  `device_type` varchar(32) DEFAULT NULL,
  `modality` varchar(16) DEFAULT NULL,
  `ae_title` varchar(64) DEFAULT NULL,
  `ip` varchar(64) DEFAULT NULL,
  `location_id` varchar(64) DEFAULT NULL,
  `location_name` varchar(255) DEFAULT NULL,
  `status` varchar(32) DEFAULT NULL,
  `availability_status` varchar(32) DEFAULT NULL,
  `assignment_status` varchar(32) DEFAULT NULL,
  `last_synced_at` varchar(64) DEFAULT NULL,
  `details` text DEFAULT NULL,
  `created_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`pk`),
  UNIQUE KEY `uq_care_devices_device_id` (`device_id`),
  KEY `idx_care_devices_dicom_enabler_id` (`dicom_enabler_id`),
  KEY `idx_care_devices_ae_title` (`ae_title`)
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
-- and patient. fetched_at is when the enabler first read the row (e.g. to check the accession
-- number of a file to upload) and last_fetched_at is when it last read it; both stay NULL until the row is read.
-- study_uid is NULL until a DICOM file with the row's accession number is picked up for upload,
-- then holds the file's StudyInstanceUID. When files for the same accession number carry
-- different StudyInstanceUIDs, each one is added once, separated by ','.
-- scan_status is the last study_status sent to CARE for the row's service request: NULL until then,
-- START_PENDING when IN_PROGRESS could not be sent when the modality sent MPPS IN PROGRESS (by the
-- MWL service) or before the first upload of a file with the row's accession number (by the SCU
-- service) - the SCU service retries it every cycle -, STARTED once IN_PROGRESS was sent, COMPLETED
-- once COMPLETED was sent after its last file was uploaded (MPPS COMPLETED does not send it).
-- CANCEL_PENDING when CANCELLED could not be sent when the modality sent MPPS DISCONTINUED - the SCU
-- service retries it every cycle -, CANCELLED once it was sent; COMPLETED is never sent for a
-- cancelled row. scan_started_at, scan_completed_at and scan_cancelled_at are when each was sent.
CREATE TABLE IF NOT EXISTS `care_worklist` (
  `pk` bigint(20) NOT NULL AUTO_INCREMENT,
  `accession_number` varchar(64) NOT NULL,
  `status` varchar(20) NOT NULL DEFAULT 'SCHEDULED',
  `service_request_pk` bigint(20) DEFAULT NULL,
  `patient_pk` bigint(20) DEFAULT NULL,
  `facility_id` varchar(64) DEFAULT NULL,
  `facility_name` varchar(255) DEFAULT NULL,
  `study_uid` text DEFAULT NULL,
  `fetched_at` datetime DEFAULT NULL,
  `last_fetched_at` datetime DEFAULT NULL,
  `scan_status` varchar(20) DEFAULT NULL,
  `scan_started_at` datetime DEFAULT NULL,
  `scan_completed_at` datetime DEFAULT NULL,
  `scan_cancelled_at` datetime DEFAULT NULL,
  `created_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`pk`),
  UNIQUE KEY `uq_care_worklist_accession_number` (`accession_number`),
  KEY `idx_care_worklist_status` (`status`),
  KEY `idx_care_worklist_scan_status` (`scan_status`),
  KEY `idx_care_worklist_service_request_pk` (`service_request_pk`),
  KEY `idx_care_worklist_patient_pk` (`patient_pk`),
  CONSTRAINT `fk_care_worklist_service_request` FOREIGN KEY (`service_request_pk`) REFERENCES `care_service_request` (`pk`),
  CONSTRAINT `fk_care_worklist_patient` FOREIGN KEY (`patient_pk`) REFERENCES `care_patient` (`pk`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Outcome of each DICOM instance upload to CARE, one row per instance. instance_key identifies
-- the instance: StudyInstanceUID|SeriesInstanceUID|SOPInstanceUID AES-encrypted with a fixed IV,
-- so the same instance always gives the same key (ascii_bin, since base64 is case-sensitive).
-- A file is matched to its row by instance_key, never by file_name, so a resent or renamed copy
-- of an instance already uploaded is found. A file that failed with a
-- retryable error stays in the SCP folder and is retried, so a retry updates the same row with
-- the latest status and log and increments retry_count (0 on the first attempt). last_retry_time
-- is when the file was last sent to CARE: set on the first upload attempt and refreshed on every
-- retry. The next retry waits upload_retry_delay_minutes * 2^retry_count after last_retry_time.
-- After maxUploadRetries retries the file is moved to FailedSCP\<dd-MM-yyyy> (the limit is set
-- in care_config or CARE_SCU_Service App.config). Errors that cannot succeed on a retry (HTTP
-- 400/409, no AccessionNumber, accession number not in the CARE worklist, a UID missing) move it
-- right away; a file with a UID missing is keyed by the UIDs it has.
-- worklist_pk links the file to the care_worklist row with its accession number; it stays NULL
-- when the file has no accession number or it is not in care_worklist.
CREATE TABLE IF NOT EXISTS `care_sync_upload` (
  `pk` bigint(20) NOT NULL AUTO_INCREMENT,
  `worklist_pk` bigint(20) DEFAULT NULL,
  `instance_key` varchar(400) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `study_uid` varchar(250) NOT NULL DEFAULT '',
  `accession_number` varchar(64) DEFAULT NULL,
  `file_name` varchar(255) NOT NULL,
  `status` varchar(20) NOT NULL,
  `log` text DEFAULT NULL,
  `retry_count` int(11) NOT NULL DEFAULT 0,
  `last_retry_time` datetime DEFAULT NULL,
  `created_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`pk`),
  UNIQUE KEY `uq_care_sync_upload_instance_key` (`instance_key`),
  KEY `idx_care_sync_upload_accession_number` (`accession_number`),
  KEY `idx_care_sync_upload_status` (`status`),
  KEY `idx_care_sync_upload_worklist_pk` (`worklist_pk`),
  CONSTRAINT `fk_care_sync_upload_worklist` FOREIGN KEY (`worklist_pk`) REFERENCES `care_worklist` (`pk`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Integration settings for this enabler's facility, one row per setting, edited in the
-- Configuration tab. Start Services offers to fill each blank setting with its default (the App.config
-- value baked in at build time, or the built-in default) so the stored values are shown and can be
-- changed; it is stored only if the user confirms. A value still blank means the service uses that default. facility_id has no default:
-- the user must enter it, and the services are not started until it is set. API keys and tokens
-- stay in App.config.
CREATE TABLE IF NOT EXISTS `care_config` (
  `config_key` varchar(100) NOT NULL,
  `config_value` text DEFAULT NULL,
  `description` varchar(500) DEFAULT NULL,
  `updated_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`config_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- INSERT IGNORE adds keys missing from older installs without overwriting values already set.
INSERT IGNORE INTO `care_config` (`config_key`, `config_value`, `description`) VALUES
  ('facility_id', NULL, 'CARE Facility ID used to fetch the worklist, upload DICOM files and send scan status updates. Blank = App.config careFacilityId; one of them is required.'),
  ('care_from_date', NULL, 'Earliest worklist date fetched from CARE (yyyy-MM-dd HH:mm:ss). Blank = App.config careFromDate.'),
  ('scu_poll_interval_seconds', NULL, 'Seconds between scans of the SCP folder for files to upload. Blank = 5. Restart the SCU service to apply.'),
  ('worklist_refresh_start_seconds', NULL, 'Seconds after the MWL service starts before the first worklist refresh. Blank = App.config. Restart the MWL service to apply.'),
  ('worklist_refresh_interval_seconds', NULL, 'Seconds between worklist refreshes. Blank = App.config. Restart the MWL service to apply.'),
  ('max_upload_retries', NULL, 'Upload retries before a file is moved to the failed folder. Blank = App.config maxUploadRetries.'),
  ('upload_retry_delay_minutes', NULL, 'Minutes after a failed upload before the first retry; the wait doubles after each retry (2, 4, 8...). Blank = 2.'),
  ('scan_completed_wait_seconds', NULL, 'Seconds after the last upload of an accession number, with none of its files left in the SCP folder, before COMPLETED is sent to CARE. Blank = App.config scanCompletedWaitSeconds, or 60.'),
  ('scp_folder', NULL, 'Folder where received DICOM files are saved and picked up for upload. Blank = SCP under the install folder. Restart the services to apply.'),
  ('failed_scp_folder', NULL, 'Folder files are moved to after the upload retry limit is hit. Blank = FailedSCP under the install folder.'),
  ('dicom_enabler_id', NULL, 'CARE id of the DICOM Enabler this enabler is, selected in the Configuration tab from the facility''s unassigned DICOM Enablers. Blank = none selected.'),
  ('dicom_enabler_name', NULL, 'Name of the selected DICOM Enabler (dicom_enabler_id), shown in the Configuration tab.'),
  ('dicom_enabler_details', NULL, 'Full details (JSON) of the selected DICOM Enabler as CARE returned them when it was selected, including its DICOM endpoints. CARE no longer returns them once it is assigned.');

-- Upgrade: the worklist is no longer filtered by modality, so the care_modality setting is removed.
-- Deleting a row that is not there does nothing, so re-running this script changes nothing.
DELETE FROM `care_config` WHERE `config_key` = 'care_modality';

-- Upgrade: config_value was varchar(1000), too short for dicom_enabler_details. Widened to TEXT only
-- while it is still varchar, so re-running this script changes nothing.
SET @care_config_upgrade = (
  SELECT IF(COUNT(*) > 0, 'ALTER TABLE `care_config` MODIFY `config_value` text DEFAULT NULL', 'DO 0')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'care_config' AND COLUMN_NAME = 'config_value' AND DATA_TYPE = 'varchar');
PREPARE care_config_upgrade_stmt FROM @care_config_upgrade;
EXECUTE care_config_upgrade_stmt;
DEALLOCATE PREPARE care_config_upgrade_stmt;
