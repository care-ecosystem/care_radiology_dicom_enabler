-- Full plexus_mi2 schema used by the DICOM Enabler services and the Modality Emulator.
-- Run via Initializer.ps1 / Initializer.bat. Safe to re-run: tables use IF NOT EXISTS,
-- and stored procedures are dropped and recreated each time.
ALTER USER 'root'@'localhost' IDENTIFIED BY 'inzin@123';
FLUSH PRIVILEGES;

CREATE DATABASE IF NOT EXISTS `plexus_mi2` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `plexus_mi2`;

-- ---------------------------------------------------------------------------
-- Tables
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS `dcm_servers` (
  `pk` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `aetitle` varchar(50) NOT NULL,
  `hostaddress` varchar(255) NOT NULL,
  `portnumber` varchar(45) NOT NULL,
  `description` mediumtext DEFAULT NULL,
  PRIMARY KEY (`pk`),
  UNIQUE KEY `pk_UNIQUE` (`pk`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS `patient` (
  `pk` bigint(20) NOT NULL AUTO_INCREMENT,
  `merge_fk` bigint(20) DEFAULT NULL,
  `pat_id` varchar(250) DEFAULT NULL,
  `pat_id_issuer` varchar(250) DEFAULT NULL,
  `pat_name` varchar(250) NOT NULL,
  `pat_fn_sx` varchar(250) DEFAULT NULL,
  `pat_gn_sx` varchar(250) DEFAULT NULL,
  `pat_i_name` varchar(250) DEFAULT NULL,
  `pat_p_name` varchar(250) DEFAULT NULL,
  `pat_birthdate` varchar(250) DEFAULT NULL,
  `pat_sex` varchar(250) DEFAULT NULL,
  `pat_custom1` varchar(250) DEFAULT NULL,
  `pat_custom2` varchar(250) DEFAULT NULL,
  `pat_custom3` varchar(250) DEFAULT NULL,
  `updated_time` datetime DEFAULT NULL,
  `created_time` datetime DEFAULT NULL,
  `pat_attrs` longblob DEFAULT NULL,
  `passportnumber` varchar(45) DEFAULT NULL,
  `ic_number` varchar(45) DEFAULT NULL,
  PRIMARY KEY (`pk`),
  KEY `pat_merge_fk` (`merge_fk`),
  KEY `pat_id` (`pat_id`(64),`pat_id_issuer`(64)),
  KEY `pat_name` (`pat_name`(64)),
  KEY `pat_fn_sx` (`pat_fn_sx`(16)),
  KEY `pat_gn_sx` (`pat_gn_sx`(16)),
  KEY `pat_i_name` (`pat_i_name`(64)),
  KEY `pat_p_name` (`pat_p_name`(64)),
  KEY `pat_birthdate` (`pat_birthdate`(8)),
  KEY `pat_sex` (`pat_sex`(1)),
  KEY `pat_custom1` (`pat_custom1`(64)),
  KEY `pat_custom2` (`pat_custom2`(64)),
  KEY `pat_custom3` (`pat_custom3`(64))
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

CREATE TABLE IF NOT EXISTS `study` (
  `pk` bigint(20) NOT NULL AUTO_INCREMENT,
  `patient_fk` bigint(20) DEFAULT NULL,
  `accno_issuer_fk` bigint(20) DEFAULT NULL,
  `study_iuid` varchar(250) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL,
  `study_id` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `study_datetime` datetime DEFAULT NULL,
  `accession_no` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `ref_physician` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `ref_phys_fn_sx` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `ref_phys_gn_sx` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `ref_phys_i_name` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `ref_phys_p_name` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `study_desc` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `study_custom1` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `study_custom2` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `study_custom3` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `study_status_id` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `mods_in_study` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `cuids_in_study` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `num_series` int(11) NOT NULL,
  `num_instances` int(11) NOT NULL,
  `ext_retr_aet` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `retrieve_aets` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `fileset_iuid` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `fileset_id` varchar(250) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `availability` int(11) NOT NULL DEFAULT 0,
  `study_status` int(11) NOT NULL DEFAULT 0,
  `checked_time` datetime DEFAULT NULL,
  `updated_time` datetime DEFAULT NULL,
  `created_time` datetime DEFAULT NULL,
  `study_attrs` longblob DEFAULT NULL,
  `studyreportstatus` int(11) DEFAULT NULL,
  `InstId` int(11) NOT NULL DEFAULT 0,
  `ReportValidateStatus` varchar(10) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `IsStudyAssigned` varchar(10) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `ReportRequired` varchar(2) CHARACTER SET utf8 COLLATE utf8_unicode_ci DEFAULT NULL,
  `AssignedDate` datetime DEFAULT NULL,
  `examroom` varchar(45) DEFAULT NULL,
  `procedureid` varchar(45) DEFAULT NULL,
  `procedurestepid` varchar(45) DEFAULT NULL,
  `hospitalname` varchar(250) DEFAULT NULL,
  `examdate` datetime DEFAULT NULL,
  PRIMARY KEY (`pk`),
  KEY `patient_fk` (`patient_fk`),
  KEY `accno_issuer_fk` (`accno_issuer_fk`),
  KEY `study_id` (`study_id`(64)),
  KEY `study_datetime` (`study_datetime`),
  KEY `accession_no` (`accession_no`(16)),
  KEY `ref_physician` (`ref_physician`(64)),
  KEY `ref_phys_fn_sx` (`ref_phys_fn_sx`(16)),
  KEY `ref_phys_gn_sx` (`ref_phys_gn_sx`(16)),
  KEY `ref_phys_i_name` (`ref_phys_i_name`(64)),
  KEY `ref_phys_p_name` (`ref_phys_p_name`(64)),
  KEY `study_desc` (`study_desc`(64)),
  KEY `study_custom1` (`study_custom1`(64)),
  KEY `study_custom2` (`study_custom2`(64)),
  KEY `study_custom3` (`study_custom3`(64)),
  KEY `study_status_id` (`study_status_id`(16)),
  KEY `study_checked` (`checked_time`),
  KEY `study_created` (`created_time`),
  KEY `study_updated` (`updated_time`),
  KEY `study_status` (`study_status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS `series` (
  `pk` bigint(20) NOT NULL AUTO_INCREMENT,
  `study_fk` bigint(20) DEFAULT NULL,
  `mpps_fk` bigint(20) DEFAULT NULL,
  `inst_code_fk` bigint(20) DEFAULT NULL,
  `series_iuid` varchar(250) NOT NULL,
  `series_no` varchar(250) DEFAULT NULL,
  `modality` varchar(250) DEFAULT NULL,
  `body_part` varchar(250) DEFAULT NULL,
  `laterality` varchar(250) DEFAULT NULL,
  `series_desc` varchar(250) DEFAULT NULL,
  `institution` varchar(250) DEFAULT NULL,
  `station_name` varchar(250) DEFAULT NULL,
  `department` varchar(250) DEFAULT NULL,
  `perf_physician` varchar(250) DEFAULT NULL,
  `perf_phys_fn_sx` varchar(250) DEFAULT NULL,
  `perf_phys_gn_sx` varchar(250) DEFAULT NULL,
  `perf_phys_i_name` varchar(250) DEFAULT NULL,
  `perf_phys_p_name` varchar(250) DEFAULT NULL,
  `pps_start` datetime DEFAULT NULL,
  `pps_iuid` varchar(250) DEFAULT NULL,
  `series_custom1` varchar(250) DEFAULT NULL,
  `series_custom2` varchar(250) DEFAULT NULL,
  `series_custom3` varchar(250) DEFAULT NULL,
  `num_instances` int(11) DEFAULT NULL,
  `src_aet` varchar(250) DEFAULT NULL,
  `ext_retr_aet` varchar(250) DEFAULT NULL,
  `retrieve_aets` varchar(250) DEFAULT NULL,
  `fileset_iuid` varchar(250) DEFAULT NULL,
  `fileset_id` varchar(250) DEFAULT NULL,
  `availability` int(11) NOT NULL,
  `series_status` int(11) NOT NULL,
  `created_time` datetime DEFAULT NULL,
  `updated_time` datetime DEFAULT NULL,
  `series_attrs` longblob DEFAULT NULL,
  PRIMARY KEY (`pk`),
  UNIQUE KEY `series_iuid` (`series_iuid`(64)),
  KEY `study_fk` (`study_fk`),
  KEY `series_mpps_fk` (`mpps_fk`),
  KEY `series_inst_code_fk` (`inst_code_fk`),
  KEY `series_no` (`series_no`(16)),
  KEY `modality` (`modality`(16)),
  KEY `body_part` (`body_part`(16)),
  KEY `laterality` (`laterality`(16)),
  KEY `series_desc` (`series_desc`(64)),
  KEY `institution` (`institution`(64)),
  KEY `station_name` (`station_name`(16)),
  KEY `department` (`department`(64)),
  KEY `perf_physician` (`perf_physician`(64)),
  KEY `perf_phys_fn_sx` (`perf_phys_fn_sx`(16)),
  KEY `perf_phys_gn_sx` (`perf_phys_gn_sx`(16)),
  KEY `perf_phys_i_name` (`perf_phys_i_name`(64)),
  KEY `perf_phys_p_name` (`perf_phys_p_name`(64)),
  KEY `series_pps_start` (`pps_start`),
  KEY `series_pps_iuid` (`pps_iuid`(64)),
  KEY `series_custom1` (`series_custom1`(64)),
  KEY `series_custom2` (`series_custom2`(64)),
  KEY `series_custom3` (`series_custom3`(64)),
  KEY `series_src_aet` (`src_aet`(64)),
  KEY `series_status` (`series_status`),
  KEY `series_created` (`created_time`),
  KEY `series_updated` (`updated_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

CREATE TABLE IF NOT EXISTS `instance` (
  `pk` bigint(20) NOT NULL AUTO_INCREMENT,
  `series_fk` bigint(20) DEFAULT NULL,
  `srcode_fk` bigint(20) DEFAULT NULL,
  `media_fk` bigint(20) DEFAULT NULL,
  `sop_iuid` varchar(250) CHARACTER SET latin1 COLLATE latin1_bin NOT NULL,
  `sop_cuid` varchar(250) CHARACTER SET latin1 COLLATE latin1_bin NOT NULL,
  `inst_no` varchar(250) CHARACTER SET latin1 COLLATE latin1_bin DEFAULT NULL,
  `content_datetime` datetime DEFAULT NULL,
  `sr_complete` varchar(250) CHARACTER SET latin1 COLLATE latin1_bin DEFAULT NULL,
  `sr_verified` varchar(250) CHARACTER SET latin1 COLLATE latin1_bin DEFAULT NULL,
  `inst_custom1` varchar(250) CHARACTER SET latin1 COLLATE latin1_bin DEFAULT NULL,
  `inst_custom2` varchar(250) CHARACTER SET latin1 COLLATE latin1_bin DEFAULT NULL,
  `inst_custom3` varchar(250) CHARACTER SET latin1 COLLATE latin1_bin DEFAULT NULL,
  `ext_retr_aet` varchar(250) CHARACTER SET latin1 COLLATE latin1_bin DEFAULT NULL,
  `retrieve_aets` varchar(250) CHARACTER SET latin1 COLLATE latin1_bin DEFAULT NULL,
  `availability` int(11) NOT NULL,
  `inst_status` int(11) NOT NULL,
  `all_attrs` bit(1) NOT NULL,
  `commitment` bit(1) NOT NULL,
  `archived` bit(1) NOT NULL,
  `updated_time` datetime DEFAULT NULL,
  `created_time` datetime DEFAULT NULL,
  `inst_attrs` longblob DEFAULT NULL,
  PRIMARY KEY (`pk`),
  UNIQUE KEY `sop_iuid` (`sop_iuid`(64)),
  KEY `series_fk` (`series_fk`),
  KEY `srcode_fk` (`srcode_fk`),
  KEY `media_fk` (`media_fk`),
  KEY `sop_cuid` (`sop_cuid`(64)),
  KEY `inst_no` (`inst_no`(16)),
  KEY `content_datetime` (`content_datetime`),
  KEY `sr_complete` (`sr_complete`(16)),
  KEY `sr_verified` (`sr_verified`(16)),
  KEY `inst_custom1` (`inst_custom1`(64)),
  KEY `inst_custom2` (`inst_custom2`(64)),
  KEY `inst_custom3` (`inst_custom3`(64)),
  KEY `ext_retr_aet` (`ext_retr_aet`(16)),
  KEY `commitment` (`commitment`),
  KEY `inst_status` (`inst_status`),
  KEY `inst_created` (`created_time`),
  KEY `inst_archived` (`archived`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

CREATE TABLE IF NOT EXISTS `userdetails` (
  `userid` int(11) NOT NULL AUTO_INCREMENT,
  `username` varchar(40) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL,
  `password` varchar(40) NOT NULL DEFAULT '',
  `instid_fk` int(11) NOT NULL,
  `address1` varchar(50) DEFAULT NULL,
  `address2` varchar(50) DEFAULT NULL,
  `emailaddress` varchar(50) DEFAULT NULL,
  `city` varchar(25) DEFAULT NULL,
  `state` varchar(25) DEFAULT NULL,
  `country` varchar(25) DEFAULT NULL,
  `userroleid_fk` int(11) DEFAULT NULL,
  `createddate` datetime DEFAULT NULL,
  `status` varchar(11) DEFAULT NULL,
  `UserSignature` varchar(255) DEFAULT NULL,
  `AllowOnlyRefStudy` varchar(255) DEFAULT NULL,
  `UserSignText` mediumtext DEFAULT NULL,
  `WorklistRow` int(11) DEFAULT NULL,
  `userlogo` varchar(255) DEFAULT NULL,
  `UserFullName` varchar(255) DEFAULT NULL,
  `UserCode` varchar(45) DEFAULT NULL,
  `NoofStudytoAssignPerDay` int(11) DEFAULT NULL,
  `AssignPriority` int(11) DEFAULT NULL,
  `CurrentDateAssignCount` int(11) DEFAULT NULL,
  `LastAssignDate` datetime DEFAULT NULL,
  `EnableAssignRule` int(11) DEFAULT NULL,
  PRIMARY KEY (`userid`),
  KEY `instid_fk` (`instid_fk`),
  KEY `userroleid_fk` (`userroleid_fk`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

-- ---------------------------------------------------------------------------
-- Stored procedures (dropped and recreated so this script is safe to re-run)
-- ---------------------------------------------------------------------------

DROP PROCEDURE IF EXISTS `push_pat_data`;

DELIMITER $$
CREATE DEFINER=`root`@`localhost` PROCEDURE `push_pat_data`(IN patient_id VARCHAR(250),IN accessionno VARCHAR(64), IN dob DATETIME,
IN first_name VARCHAR(255),
IN last_name VARCHAR(255),IN sex VARCHAR(8),IN title VARCHAR(24),IN modality VARCHAR(5),IN exam_desc VARCHAR(255) ,IN exam_room VARCHAR(255) ,
IN hosp_name VARCHAR(255),IN perf_physician_name VARCHAR(255),IN ref_physician_name VARCHAR(255),IN procedure_id VARCHAR(25),
IN procedure_stepid VARCHAR(255),
IN ae_title VARCHAR(255),IN exam_datetime DATETIME,OUT outreturnstatus INT(11))
BEGIN
    Declare patientname VARCHAR(250);
    Declare patientuid INT(11);
    Declare createdpatuid INT(11);

    SET PatientUID = 0;

    SET outreturnstatus = 0;
    SET patientname = CONCAT(first_name, '^' ,last_name);
-- Select InReportId;
    Select  pat.pk INTO patientuid From patient pat , study st
 Where pat.pat_id = patient_id AND pat.pk = st.patient_fk AND accession_no = accessionno;

    IF(patientuid > 0) THEN
        -- Patient Id With accesion no already exists
        SET outreturnstatus = -1;

    ELSE

        Select  pk INTO patientuid From patient
 Where pat_id = patient_id;
        IF(patientuid > 0) THEN
        -- Patient Id already exists.
        -- Create new Study entry for the existing patient

        INSERT INTO study (patient_fk,accession_no,ref_physician,study_desc,mods_in_study,examroom,procedureid,procedurestepid,
        hospitalname,retrieve_aets,ext_retr_aet,examdate,study_status,study_iuid,num_series,num_instances)
        VALUES
        (patientuid,accessionno,ref_physician_name,exam_desc,modality,exam_room,procedure_id,procedure_stepid,hosp_name,
        ae_title,ae_title,exam_datetime,0,'0',0,0);

        SET outreturnstatus = 0;

        ELSE
        INSERT INTO patient (pat_id,pat_name,pat_birthdate,pat_sex,created_time,updated_time)
        VALUES
        (patient_id,patientname,dob,sex,now(),now());
        SET createdpatuid = LAST_INSERT_ID();

        INSERT INTO study (patient_fk,accession_no,ref_physician,study_desc,mods_in_study,examroom,procedureid,procedurestepid,
        hospitalname,retrieve_aets,ext_retr_aet,examdate,study_status,study_iuid,num_series,num_instances)
        VALUES
        (createdpatuid,accessionno,ref_physician_name,exam_desc,modality,exam_room,procedure_id,procedure_stepid,hosp_name,
        ae_title,ae_title,exam_datetime,0,'0',0,0);
        SET outreturnstatus = 0;
        END IF;


    END IF;

END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS `push_patdicom_details`;

DELIMITER $$
CREATE DEFINER=`root`@`localhost` PROCEDURE `push_patdicom_details`(IN patient_id VARCHAR(250),IN accession_no VARCHAR(64),
IN studyinstanceid VARCHAR(250),IN seriesinstanceid VARCHAR(250),IN seriesno VARCHAR(250),IN modality VARCHAR(250)
,IN bodypart VARCHAR(250),IN series_desc VARCHAR(250),IN institution VARCHAR(250),IN stationname VARCHAR(250),
IN department VARCHAR(250),IN imageinstanceid VARCHAR(250),IN studystatus INT(11),IN sopclassuid VARCHAR(250),OUT outreturnstatus INT(11))
proc_label: BEGIN

    Declare patientuid INT(11);
    Declare createdpatuid INT(11);

    DECLARE getpatientid INT(11);
    DECLARE getstudyid INT(11);
    DECLARE getseriesid INT(11);
    DECLARE getimageid INT(11);

    DECLARE getstudyseriescount INT(11);
    DECLARE getstudyimagecount INT(11);
    DECLARE getseriesimagecount INT(11);

    SET getpatientid = 0;
    SET getstudyid = 0;
    SET getseriesid = 0;
    SET getimageid = 0;
    SET getstudyseriescount = 0;
    SET getstudyimagecount = 0;
    SET getseriesimagecount = 0;

    SET PatientUID = 0;

    SET outreturnstatus = 0;
        /*
        Select  pk INTO getpatientid From patient
        Where pat_id = pat_id;
            IF(getpatientid > 0) THEN
            SET outreturnstatus = 0;
            Else
            SET outreturnstatus = -2;
            LEAVE proc_label;
            END IF;
        */

    -- Check image instance id already exists , if so return return status -1
    -- Else check check for study instnace id , if already exists
    Select  pk INTO getimageid From instance
 Where sop_iuid = imageinstanceid;
    IF(getimageid > 0) THEN
        -- Image already exists
        UPDATE STUDY SET updated_time = now(),study_status=studystatus WHERE study_iuid = studyinstanceid;

        SET outreturnstatus = -1;

    ELSE

        Select  pk INTO getstudyid From study
        Where study_iuid = studyinstanceid;
        Select  num_series INTO getstudyseriescount From study
        Where study_iuid = studyinstanceid;
        Select  num_instances INTO getstudyimagecount From study
        Where study_iuid = studyinstanceid;


        IF(getstudyid > 0) THEN
        -- Image already exists

        Select  pk INTO getseriesid From series
        Where series_iuid = seriesinstanceid;
        Select  num_instances INTO getseriesimagecount From series
        Where series_iuid = seriesinstanceid;


            IF(getseriesid > 0) THEN

            SET getstudyimagecount = getstudyimagecount + 1;
            SET getseriesimagecount = getseriesimagecount + 1;
            UPDATE STUDY SET updated_time = now(),num_instances=getstudyimagecount,study_status=studystatus
            WHERE study_iuid = studyinstanceid;
            -- Image already exists
            UPDATE SERIES SET updated_time = now(),num_instances=getseriesimagecount
            WHERE series_iuid = seriesinstanceid;
            INSERT INTO INSTANCE (series_fk,sop_iuid,created_time,updated_time,sop_cuid)
            VALUES
            (getseriesid,imageinstanceid,now(),now(),sopclassuid);
            ELSE
            SET getstudyseriescount = getstudyseriescount + 1;
            SET getstudyimagecount = getstudyimagecount + 1;
            UPDATE STUDY SET updated_time = now(),num_series=getstudyseriescount,num_instances=getstudyimagecount,study_status=studystatus
            WHERE study_iuid = studyinstanceid;
            INSERT INTO SERIES (study_fk,series_iuid,series_no,modality,body_part,series_desc,institution,station_name,department,num_instances,created_time,updated_time)
            VALUES
            (getstudyid,seriesinstanceid,seriesno,modality,bodypart,series_desc,institution,stationname,department,1,now(),now());
            SET getseriesid = LAST_INSERT_ID();
            INSERT INTO INSTANCE (series_fk,sop_iuid,created_time,updated_time,sop_cuid)
            VALUES
            (getseriesid,imageinstanceid,now(),now(),sopclassuid);

            END IF;
        ELSE
            Select  pk INTO getpatientid From patient
        Where pat_id = patient_id;
            IF(getpatientid > 0) THEN
            UPDATE STUDY SET study_iuid = studyinstanceid , num_series = 1, num_instances = 1 , study_status=studystatus, updated_time = now()
        WHERE patient_fk = getpatientid;
            Select  pk INTO getstudyid From study
        Where study_iuid = studyinstanceid;
            INSERT INTO SERIES (study_fk,series_iuid,series_no,modality,body_part,series_desc,institution,station_name,department,num_instances,
created_time,updated_time)
            VALUES
            (getstudyid,seriesinstanceid,seriesno,modality,bodypart,series_desc,institution,stationname,department,1,now(),now());
            SET getseriesid = LAST_INSERT_ID();
            INSERT INTO INSTANCE (series_fk,sop_iuid,created_time,updated_time,sop_cuid)
            VALUES
            (getseriesid,imageinstanceid,now(),now(),sopclassuid);
            ELSE
            SET outreturnstatus = -2;
            END IF;
        END IF;

        END IF;

END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS `updatestatus`;

DELIMITER $$
CREATE DEFINER=`root`@`localhost` PROCEDURE `updatestatus`(in studyinstanceids VARCHAR(250),in studystatus INT(11))
BEGIN
    UPDATE study SET study_status=studystatus WHERE study_iuid=studyinstanceids;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS `updatestatus_ascno`;

DELIMITER $$
CREATE DEFINER=`root`@`localhost` PROCEDURE `updatestatus_ascno`(in accessionnos VARCHAR(250),in studystatus INT(11))
BEGIN
    UPDATE study SET study_status=studystatus WHERE accession_no=accessionnos;
END$$
DELIMITER ;
