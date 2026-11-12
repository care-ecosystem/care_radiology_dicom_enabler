using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;

using System.Xml;
using MySql.Data.MySqlClient;

namespace Plexus.Common.Database
{
    public class ucls_DAL
    {
        MySqlConnection conConnection = new MySqlConnection();
        MySqlDataAdapter adpAdapter = new MySqlDataAdapter();
        DataSet dstDataSet = new DataSet();
        string _applicationDirectory = string.Empty;
        public ucls_DAL(string applicationDirectory)
        {
            _applicationDirectory = applicationDirectory;
            conConnection.ConnectionString = ucls_EnDcryption.DecryptString(EncKey.encdeKey,getConnectionString());
        }


        /// <summary>
        /// Get Connection String from Confirguraiton file
        /// </summary>
        /// <returns></returns>
        public string getConnectionString()
        {
            string connString = string.Empty;
            string xmlPath = Path.Combine(_applicationDirectory, "cfg/common.cfg");
            XmlDocument configDoc = new XmlDocument();
            configDoc.Load(xmlPath);

            XmlNode csNode = configDoc.SelectSingleNode("/configurations/connectString");
            if (csNode != null)
            {
                connString = csNode.InnerText;
            }
            return connString;
        }


        /// <summary>
        /// Deletes the data of the worklist orders linked to no DICOM device (saved by a whole-facility fetch):
        /// service requests with no care_service_request_device row that has a device_id, and care_worklist rows
        /// with no service request. Removed, in one transaction: their care_sync_upload rows; the Plexus study,
        /// series and instance rows of their accession numbers, and Plexus patients left with no study; their
        /// care_worklist rows; the service requests (their care_service_request_device location rows go with them,
        /// ON DELETE CASCADE); and care_patient rows no longer used by any care_worklist row. Orders linked to a
        /// device are not touched. deleted gets the number of rows deleted per table. Returns false with
        /// errorString set (and nothing deleted) on failure.
        /// </summary>
        public bool DeleteUnlinkedOrders(ref Dictionary<string, int> deleted, ref string errorString)
        {
            deleted = new Dictionary<string, int>();
            // Lambdas cannot use the ref parameter, so the counts are collected here and copied at the end
            var counts = new Dictionary<string, int>();
            MySqlTransaction transaction = null;
            try
            {
                if (!openDBConnection(ref errorString))
                    return false;

                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE'", conConnection))
                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        existing.Add(reader.GetString(0));
                }
                bool plexusTables = existing.Contains("study") && existing.Contains("series") && existing.Contains("instance") && existing.Contains("patient");

                transaction = conConnection.BeginTransaction();
                Func<string, int> run = sql =>
                {
                    using (MySqlCommand cmd = new MySqlCommand(sql, conConnection, transaction))
                        return cmd.ExecuteNonQuery();
                };
                Action<string, string> delete = (table, sql) =>
                {
                    int count = run(sql);
                    counts[table] = (counts.TryGetValue(table, out int before) ? before : 0) + count;
                };

                // The orders linked to no device and their worklist rows. MySQL cannot open a temporary table
                // twice in one statement, so each one is used once per statement below.
                run("DROP TEMPORARY TABLE IF EXISTS tmp_unlinked_sr, tmp_unlinked_wl, tmp_unlinked_study");
                run("CREATE TEMPORARY TABLE tmp_unlinked_sr (pk BIGINT PRIMARY KEY) " +
                    "SELECT sr.pk FROM care_service_request sr WHERE NOT EXISTS " +
                    "(SELECT 1 FROM care_service_request_device d WHERE d.service_request_pk = sr.pk AND d.device_id IS NOT NULL)");
                run("CREATE TEMPORARY TABLE tmp_unlinked_wl (pk BIGINT PRIMARY KEY, accession_number VARCHAR(64), patient_pk BIGINT) " +
                    "SELECT w.pk, w.accession_number, w.patient_pk FROM care_worklist w " +
                    "WHERE w.service_request_pk IS NULL OR w.service_request_pk IN (SELECT pk FROM tmp_unlinked_sr)");

                if (existing.Contains("care_sync_upload"))
                {
                    delete("care_sync_upload", "DELETE u FROM care_sync_upload u JOIN tmp_unlinked_wl t ON u.worklist_pk = t.pk");
                    delete("care_sync_upload", "DELETE u FROM care_sync_upload u JOIN tmp_unlinked_wl t ON u.accession_number = t.accession_number");
                }

                if (plexusTables)
                {
                    run("CREATE TEMPORARY TABLE tmp_unlinked_study (pk BIGINT PRIMARY KEY, patient_fk BIGINT) " +
                        "SELECT st.pk, st.patient_fk FROM study st JOIN tmp_unlinked_wl t ON st.accession_no = t.accession_number");
                    delete("instance", "DELETE i FROM instance i JOIN series s ON i.series_fk = s.pk JOIN tmp_unlinked_study t ON s.study_fk = t.pk");
                    delete("series", "DELETE s FROM series s JOIN tmp_unlinked_study t ON s.study_fk = t.pk");
                    delete("study", "DELETE st FROM study st JOIN tmp_unlinked_study t ON st.pk = t.pk");
                    delete("patient", "DELETE p FROM patient p WHERE p.pk IN (SELECT patient_fk FROM tmp_unlinked_study) " +
                                      "AND NOT EXISTS (SELECT 1 FROM study s WHERE s.patient_fk = p.pk)");
                }

                delete("care_worklist", "DELETE w FROM care_worklist w JOIN tmp_unlinked_wl t ON w.pk = t.pk");
                delete("care_service_request", "DELETE sr FROM care_service_request sr JOIN tmp_unlinked_sr t ON sr.pk = t.pk");
                delete("care_patient", "DELETE p FROM care_patient p WHERE p.pk IN (SELECT patient_pk FROM tmp_unlinked_wl) " +
                                       "AND NOT EXISTS (SELECT 1 FROM care_worklist w WHERE w.patient_pk = p.pk)");

                transaction.Commit();
                transaction = null;
                run("DROP TEMPORARY TABLE IF EXISTS tmp_unlinked_sr, tmp_unlinked_wl, tmp_unlinked_study");
                closeDBConnection(ref errorString);
                deleted = counts;
                return true;
            }
            catch (Exception ex)
            {
                try { transaction?.Rollback(); } catch { }
                errorString = "Deleting the orders linked to no device failed with exception " + ex.Message;
                return false;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public void Dispose()
        {
            if (dstDataSet != null )
                dstDataSet.Dispose();
            if (adpAdapter != null )
                adpAdapter.Dispose();
            if (conConnection != null )
                conConnection.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool openDBConnection(ref string errorString)
        {
            try
            {
                if (conConnection.State == ConnectionState.Closed)
                    conConnection.Open();

            }
            catch(Exception ex)
            {
                errorString = ex.Message;
                return false;
            }
            return true;

        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool closeDBConnection(ref string errorString)
        {
            try
            {
                if (conConnection.State == ConnectionState.Open)
                    conConnection.Close();

            }
            catch (Exception ex)
            {
                errorString = ex.Message;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Insert of Update Server Details
        /// </summary>
        /// <param name="serverName"></param>
        /// <param name="aetitle"></param>
        /// <param name="hostaddress"></param>
        /// <param name="port"></param>
        /// <param name="description"></param>
        /// <param name="updateServer"></param>
        /// <param name="errorString"></param>
        /// <returns></returns>
        public bool insertorUpdateServer(string serverName,string aetitle,string hostaddress,string port,string description,string primarykey,bool updateServer , ref string errorString)
        {
            try
            {
                string query = string.Empty;
                if ( openDBConnection(ref errorString))
                {
                    if (!updateServer)
                    {
                        query = "INSERT INTO dcm_servers(name,aetitle,hostaddress,portnumber,description) " +
                            "VALUES ('" + serverName + "','" + aetitle + "','" + hostaddress + "','" + port + "','" + description + "')";
                    }
                    else
                    {
                        query = "UPDATE dcm_servers SET name='"+serverName+ "',aetitle='" + aetitle + "',hostaddress='" + hostaddress + "',portnumber='" + port + "'," +
                            "description='" + description + "' WHERE pk="+ primarykey + "" ;
                    }
                    MySqlCommand command = new MySqlCommand(query, conConnection);
                    command.ExecuteNonQuery();
                    conConnection.Close();
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = ex.Message;
                return false;
            }
            return true;
        }


        public bool DeleteServer(string primarykey, ref string errorString)
        {
            try
            {
                string query = string.Empty;
                if (openDBConnection(ref errorString))
                {
                    query = "DELETE FROM dcm_servers WHERE pk = "+ primarykey + "";
                    MySqlCommand command = new MySqlCommand(query, conConnection);
                    command.ExecuteNonQuery();
                    conConnection.Close();
                }
                else
                {
                    errorString = "Error Opening DB Connection";
                    return false;
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = ex.Message;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="errorString"></param>
        /// <returns></returns>
        public DataSet LoadPatientList(ref string errorString)
        {
            DataSet dsResult = null;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    string query = "SELECT patient.pat_id as pat_id,patient.pat_name as pat_name,patient.pat_sex as pat_sex,patient.pat_birthdate as pat_birthdate,study.accession_no as accession_no,study.mods_in_study as modality, study.study_status as study_status ,study.num_series as num_series,study.num_instances as num_instance FROM patient, study WHERE patient.pk = study.patient_fk";
                    dsResult = new DataSet();
                    adpAdapter = new MySqlDataAdapter(query, conConnection);
                    adpAdapter.Fill(dsResult);
                    closeDBConnection(ref errorString);
                }
            }
            catch (Exception ex)
            {
                errorString = ex.Message;
            }

            return dsResult;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="errorString"></param>
        /// <returns></returns>
        /// <summary>
        /// 
        /// </summary>
        /// <param name="errorString"></param>
        /// <returns></returns>
        public DataSet LoadServerList(ref string errorString)
        {
            DataSet dsResult = null;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    string query = "SELECT pk,name,aetitle,hostaddress,portnumber,description FROM dcm_servers";
                    dsResult = new DataSet();
                    adpAdapter = new MySqlDataAdapter(query, conConnection);
                    adpAdapter.Fill(dsResult);
                    closeDBConnection(ref errorString);
                }
            }
            catch (Exception ex)
            {
                errorString = ex.Message;
            }

            return dsResult;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="errorString"></param>
        /// <returns></returns>
        public DataSet GetWorklistData(ref string errorString)
        {
            DataSet dsResult = null;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    string query = @"SELECT patient.pat_id,patient.pat_name as pat_name,patient.pat_sex as pat_sex,patient.pat_birthdate as pat_birthdate,study.accession_no as accession_no,study.mods_in_study as modality,study.study_desc as exam_desc,study.examroom as exam_room, study.hospitalname as hospitalname, study.ref_physician as perform_phys,
                                    study.procedureid as procedureid,study.procedurestepid as procedurestepid,study.study_iuid as study_iuid,
                                    study.retrieve_aets as aetitle,study.ref_physician as ref_physician,study.examdate as examdate
                                    FROM patient, study WHERE patient.pk = study.patient_fk";
                    dsResult = new DataSet();
                    adpAdapter = new MySqlDataAdapter(query, conConnection);
                    adpAdapter.Fill(dsResult);
                    closeDBConnection(ref errorString);
                }
            }
            catch (Exception ex)
            {
                errorString = ex.Message;
            }

            return dsResult;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="patientId"></param>
        /// <param name="accesionNo"></param>
        /// <param name="studyInstanceId"></param>
        /// <param name="seriesInstanceId"></param>
        /// <param name="seriesNo"></param>
        /// <param name="modality"></param>
        /// <param name="bodyPart"></param>
        /// <param name="seriesDesc"></param>
        /// <param name="instName"></param>
        /// <param name="stationName"></param>
        /// <param name="departmentName"></param>
        /// <param name="imageInstanceId"></param>
        /// <param name="studystatus"></param>
        /// <param name="sopClassUid"></param>
        /// <param name="errorString"></param>
        /// <returns></returns>
        public string InsertOrUpdateStudyInfo(string patientId, string accesionNo, string studyInstanceId, string seriesInstanceId, string seriesNo, string modality,
            string bodyPart, string seriesDesc, string instName, string stationName, string departmentName, string imageInstanceId, int studystatus, string sopClassUid, ref string errorString)
        {
            string retVal = string.Empty;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand("push_patdicom_details", conConnection))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@patient_id", patientId);
                        cmd.Parameters.AddWithValue("@accession_no", accesionNo);
                        cmd.Parameters.AddWithValue("@studyinstanceid", studyInstanceId);
                        cmd.Parameters.AddWithValue("@seriesinstanceid", seriesInstanceId);
                        cmd.Parameters.AddWithValue("@seriesno", seriesNo);
                        cmd.Parameters.AddWithValue("@modality", modality);
                        cmd.Parameters.AddWithValue("@bodypart", bodyPart);
                        cmd.Parameters.AddWithValue("@series_desc", seriesDesc);
                        cmd.Parameters.AddWithValue("@institution", instName);
                        cmd.Parameters.AddWithValue("@stationname", stationName);
                        cmd.Parameters.AddWithValue("@department", departmentName);
                        cmd.Parameters.AddWithValue("@imageinstanceid", imageInstanceId);
                        cmd.Parameters.AddWithValue("@studystatus", studystatus);
                        cmd.Parameters.AddWithValue("@sopclassuid", sopClassUid);
                        
                        cmd.Parameters.Add("outreturnstatus", MySqlDbType.String);
                        cmd.Parameters["outreturnstatus"].Direction = ParameterDirection.Output;
                        cmd.ExecuteNonQuery();

                        // this is how we can get the value in the output parameter after stored proc has executed
                        var outParamValue = cmd.Parameters["outreturnstatus"].Value;
                        if (outParamValue != null)
                            retVal = outParamValue.ToString();
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = $"Error Inserting Data to Database for StudyInstanceID {studyInstanceId} and for ImageInstanceId {imageInstanceId} with expection" + ex.Message;
            }
            return retVal;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="studyInstanceIds"></param>
        /// <param name="status"></param>
        /// <param name="errorString"></param>
        /// <returns></returns>

        public string UpdateStudyStatus(string studyInstanceIds, int status, ref string errorString)
        {
            string retVal = string.Empty;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand("updatestatus", conConnection))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@studyinstanceids", studyInstanceIds);
                        cmd.Parameters.AddWithValue("@studystatus", status);
                        cmd.ExecuteNonQuery();
                        //// this is how we can get the value in the output parameter after stored proc has executed
                        //var outParamValue = cmd.Parameters["outreturnstatus"].Value;
                        //if (outParamValue != null)
                        //    retVal = outParamValue.ToString();
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = $"Error Updating Status for StudyInstanceID {studyInstanceIds} with expection" + ex.Message;
            }
            return retVal;
        }


        public string UpdateStudyStatusByAscNo(string accessionNos, int status, ref string errorString)
        {
            string retVal = string.Empty;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand("updatestatus_ascno", conConnection))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@accessionnos", accessionNos);
                        cmd.Parameters.AddWithValue("@studystatus", status);
                        cmd.ExecuteNonQuery();
                        //// this is how we can get the value in the output parameter after stored proc has executed
                        //var outParamValue = cmd.Parameters["outreturnstatus"].Value;
                        //if (outParamValue != null)
                        //    retVal = outParamValue.ToString();
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = $"Error Updating Status for StudyInstanceID {accessionNos} with expection" + ex.Message;
            }
            return retVal;
        }


        public bool validateAETitle(string callingAET,string hostAddress,ref string errorString)
        {
            bool bRetVal = false;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    string selectQuery = $"SELECT count(*) FROM dcm_servers WHERE aetitle='{callingAET}' and hostaddress='{hostAddress}'";
                    using (MySqlCommand cmd = new MySqlCommand(selectQuery, conConnection))
                    {
                        var count = cmd.ExecuteScalar();
                        if (count != null)
                        {
                            if (Convert.ToInt32(count) > 0)
                            {
                                bRetVal = true;
                            }
                        }
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = $"Validating ATTitle for failed with expection" + ex.Message;
                bRetVal = false;
            }
            return bRetVal;
        }


        /// <summary>
        /// Get the Facility ID to filter the CARE worklist by, from the facility_id setting in
        /// care_config, or when that is blank, the careFacilityId baked into the running service's
        /// App.config at build time. Each enabler serves one facility, so every calling AE gets the
        /// same value; callingAET is only used in the error message.
        /// </summary>
        /// <param name="resolvedFrom">Set to a human-readable description of how the value was found,
        /// for logging - or why it could not be.</param>
        public string GetFacilityId(string callingAET, ref string resolvedFrom, ref string errorString)
        {
            string facilityId = string.Empty;
            resolvedFrom = "not resolved";
            try
            {
                facilityId = GetConfigValue("facility_id", string.Empty, ref errorString);
                if (!string.IsNullOrEmpty(errorString))
                    errorString = $"Getting Facility ID for AETitle {callingAET} failed: " + errorString;
                else if (!string.IsNullOrWhiteSpace(facilityId))
                    resolvedFrom = "facility_id in the Configuration tab";
                else
                {
                    facilityId = (ConfigurationManager.AppSettings["careFacilityId"] ?? string.Empty).Trim();
                    resolvedFrom = string.IsNullOrWhiteSpace(facilityId)
                        ? "no Facility ID entered in the Configuration tab or set in App.config careFacilityId"
                        : "App.config careFacilityId (Configuration tab facility_id is blank)";
                }
            }
            catch (Exception ex)
            {
                errorString = $"Getting Facility ID for AETitle {callingAET} failed with expection" + ex.Message;
                facilityId = string.Empty;
            }
            return facilityId;
        }


        /// <summary>
        /// Returns the value of a care_config setting, or defaultValue when the setting is blank,
        /// not in care_config, or cannot be read (errorString is then set).
        /// </summary>
        public string GetConfigValue(string configKey, string defaultValue, ref string errorString)
        {
            string value = defaultValue;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT config_value FROM care_config WHERE config_key = @config_key",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@config_key", configKey);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value && !string.IsNullOrWhiteSpace(result.ToString()))
                            value = result.ToString().Trim();
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                closeDBConnection(ref errorString);
                errorString = $"Reading {configKey} from care_config failed with exception " + ex.Message;
                value = defaultValue;
            }
            return value;
        }


        /// <summary>
        /// Loads every care_config setting for the Configuration tab.
        /// </summary>
        public DataSet LoadConfig(ref string errorString)
        {
            DataSet dsResult = null;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    string query = "SELECT config_key, config_value, description FROM care_config ORDER BY config_key";
                    dsResult = new DataSet();
                    adpAdapter = new MySqlDataAdapter(query, conConnection);
                    adpAdapter.Fill(dsResult);
                    closeDBConnection(ref errorString);
                }
            }
            catch (Exception ex)
            {
                errorString = ex.Message;
            }

            return dsResult;
        }


        /// <summary>
        /// Saves the values of existing care_config settings in one transaction. A blank value is
        /// stored as NULL.
        /// </summary>
        public bool SaveConfig(Dictionary<string, string> values, ref string errorString)
        {
            MySqlTransaction transaction = null;
            try
            {
                if (!openDBConnection(ref errorString))
                    return false;

                transaction = conConnection.BeginTransaction();
                foreach (KeyValuePair<string, string> setting in values)
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "UPDATE care_config SET config_value = @config_value WHERE config_key = @config_key",
                        conConnection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@config_key", setting.Key);
                        cmd.Parameters.AddWithValue("@config_value", DbValue(setting.Value?.Trim()));
                        cmd.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
                closeDBConnection(ref errorString);
                return true;
            }
            catch (Exception ex)
            {
                try { transaction?.Rollback(); } catch { }
                closeDBConnection(ref errorString);
                errorString = "Saving care_config failed with exception " + ex.Message;
                return false;
            }
        }


        /// <summary>
        /// Stores a value for each care_config setting that is still blank, in one transaction.
        /// Settings that already have a value are left as they are.
        /// </summary>
        public bool FillBlankConfig(Dictionary<string, string> values, ref string errorString)
        {
            MySqlTransaction transaction = null;
            try
            {
                if (!openDBConnection(ref errorString))
                    return false;

                transaction = conConnection.BeginTransaction();
                foreach (KeyValuePair<string, string> setting in values)
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "UPDATE care_config SET config_value = @config_value WHERE config_key = @config_key " +
                        "AND (config_value IS NULL OR TRIM(config_value) = '')",
                        conConnection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@config_key", setting.Key);
                        cmd.Parameters.AddWithValue("@config_value", DbValue(setting.Value?.Trim()));
                        cmd.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
                closeDBConnection(ref errorString);
                return true;
            }
            catch (Exception ex)
            {
                try { transaction?.Rollback(); } catch { }
                closeDBConnection(ref errorString);
                errorString = "Filling care_config defaults failed with exception " + ex.Message;
                return false;
            }
        }


        /// <summary>
        /// Brings the local CARE tables in line with one CARE worklist response. Each item's patient
        /// and service request are inserted into care_patient and care_service_request, or refreshed
        /// with the latest values if already there (a blank modality keeps the one already saved).
        /// Accession numbers not yet in care_worklist are inserted, linked to that patient and service
        /// request; worklist rows already there are left
        /// untouched. Rows still SCHEDULED whose accession number is not in this response are marked
        /// COMPLETED, but only rows for the facilityId the response was fetched with, so changing it in
        /// the Configuration tab leaves the other facility's rows as they are. With a blank facilityId
        /// no row is marked COMPLETED. Call only with a response the CARE API reported as successful -
        /// an empty list marks every scheduled row for that facility completed.
        /// </summary>
        public bool SyncCareWorklist(List<CareWorklistRecord> records, string facilityId, ref int insertedCount, ref int completedCount, ref string errorString)
        {
            insertedCount = 0;
            completedCount = 0;
            MySqlTransaction transaction = null;
            try
            {
                if (!openDBConnection(ref errorString))
                    return false;

                transaction = conConnection.BeginTransaction();

                const string patientQuery =
                    "INSERT INTO care_patient (patient_id, name, gender, age, patient_uhid) VALUES " +
                    "(@patient_id, @name, @gender, @age, @patient_uhid) " +
                    "ON DUPLICATE KEY UPDATE name = VALUES(name), gender = VALUES(gender), age = VALUES(age), patient_uhid = VALUES(patient_uhid)";

                const string serviceRequestQuery =
                    "INSERT INTO care_service_request (service_request_id, name, date, body_site, body_site_id, description, modality, referring_physician, " +
                    "procedure_id, priority, technician_instruction, patient_instruction) VALUES " +
                    "(@service_request_id, @name, @date, @body_site, @body_site_id, @description, @modality, @referring_physician, " +
                    "@procedure_id, @priority, @technician_instruction, @patient_instruction) " +
                    "ON DUPLICATE KEY UPDATE name = VALUES(name), date = VALUES(date), body_site = VALUES(body_site), body_site_id = VALUES(body_site_id), " +
                    "description = VALUES(description), modality = COALESCE(VALUES(modality), modality), referring_physician = VALUES(referring_physician), " +
                    "procedure_id = VALUES(procedure_id), priority = VALUES(priority), " +
                    "technician_instruction = VALUES(technician_instruction), patient_instruction = VALUES(patient_instruction)";

                const string deleteDevicesQuery =
                    "DELETE d FROM care_service_request_device d JOIN care_service_request sr ON sr.pk = d.service_request_pk " +
                    "WHERE sr.service_request_id = @service_request_id";

                const string deviceQuery =
                    "INSERT INTO care_service_request_device (service_request_pk, location_id, location_name, device_id, device_name, device_ae_title, device_modality) " +
                    "SELECT pk, @location_id, @location_name, @device_id, @device_name, @device_ae_title, @device_modality " +
                    "FROM care_service_request WHERE service_request_id = @service_request_id";

                const string worklistQuery =
                    "INSERT IGNORE INTO care_worklist (accession_number, status, service_request_pk, patient_pk, facility_id, facility_name) VALUES " +
                    "(@accession_number, 'SCHEDULED', (SELECT pk FROM care_service_request WHERE service_request_id = @service_request_id), " +
                    "(SELECT pk FROM care_patient WHERE patient_id = @patient_id), @facility_id, @facility_name)";

                var accessionNumbers = new List<string>();
                foreach (CareWorklistRecord record in records)
                {
                    if (string.IsNullOrWhiteSpace(record.AccessionNumber))
                        continue;
                    accessionNumbers.Add(record.AccessionNumber);

                    if (!string.IsNullOrWhiteSpace(record.PatientId))
                    {
                        using (MySqlCommand cmd = new MySqlCommand(patientQuery, conConnection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@patient_id", record.PatientId);
                            cmd.Parameters.AddWithValue("@name", DbValue(record.PatientName));
                            cmd.Parameters.AddWithValue("@gender", DbValue(record.PatientGender));
                            cmd.Parameters.AddWithValue("@age", record.PatientAge.HasValue ? (object)record.PatientAge.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@patient_uhid", DbValue(record.PatientUhid));
                            cmd.ExecuteNonQuery();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(record.ServiceRequestId))
                    {
                        using (MySqlCommand cmd = new MySqlCommand(serviceRequestQuery, conConnection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@service_request_id", record.ServiceRequestId);
                            cmd.Parameters.AddWithValue("@name", DbValue(record.ServiceRequestName));
                            cmd.Parameters.AddWithValue("@date", record.ServiceRequestDate.HasValue ? (object)record.ServiceRequestDate.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@body_site", DbValue(record.ServiceRequestBodySite));
                            cmd.Parameters.AddWithValue("@body_site_id", DbValue(record.ServiceRequestBodySiteId));
                            cmd.Parameters.AddWithValue("@description", DbValue(record.ServiceRequestDescription));
                            cmd.Parameters.AddWithValue("@modality", DbValue(record.ServiceRequestModality));
                            cmd.Parameters.AddWithValue("@referring_physician", DbValue(record.ServiceRequestReferringPhysician));
                            cmd.Parameters.AddWithValue("@procedure_id", DbValue(record.ServiceRequestProcedureId));
                            cmd.Parameters.AddWithValue("@priority", DbValue(record.ServiceRequestPriority));
                            cmd.Parameters.AddWithValue("@technician_instruction", DbValue(record.ServiceRequestTechnicianInstruction));
                            cmd.Parameters.AddWithValue("@patient_instruction", DbValue(record.ServiceRequestPatientInstruction));
                            cmd.ExecuteNonQuery();
                        }

                        using (MySqlCommand cmd = new MySqlCommand(deleteDevicesQuery, conConnection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@service_request_id", record.ServiceRequestId);
                            cmd.ExecuteNonQuery();
                        }

                        foreach (CareWorklistDevice device in record.Devices ?? new List<CareWorklistDevice>())
                        {
                            using (MySqlCommand cmd = new MySqlCommand(deviceQuery, conConnection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@service_request_id", record.ServiceRequestId);
                                cmd.Parameters.AddWithValue("@location_id", DbValue(device.LocationId));
                                cmd.Parameters.AddWithValue("@location_name", DbValue(device.LocationName));
                                cmd.Parameters.AddWithValue("@device_id", DbValue(device.DeviceId));
                                cmd.Parameters.AddWithValue("@device_name", DbValue(device.DeviceName));
                                cmd.Parameters.AddWithValue("@device_ae_title", DbValue(device.DeviceAeTitle));
                                cmd.Parameters.AddWithValue("@device_modality", DbValue(device.DeviceModality));
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }

                    using (MySqlCommand cmd = new MySqlCommand(worklistQuery, conConnection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@accession_number", record.AccessionNumber);
                        cmd.Parameters.AddWithValue("@service_request_id", DbValue(record.ServiceRequestId));
                        cmd.Parameters.AddWithValue("@patient_id", DbValue(record.PatientId));
                        cmd.Parameters.AddWithValue("@facility_id", DbValue(record.FacilityId));
                        cmd.Parameters.AddWithValue("@facility_name", DbValue(record.FacilityName));
                        insertedCount += cmd.ExecuteNonQuery();
                    }
                }

                if (!string.IsNullOrWhiteSpace(facilityId))
                {
                    string completeQuery =
                        "UPDATE care_worklist w " +
                        "SET w.status = 'COMPLETED' WHERE w.status <> 'COMPLETED' AND w.facility_id = @facility_id";
                    using (MySqlCommand cmd = new MySqlCommand(string.Empty, conConnection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@facility_id", facilityId.Trim());
                        if (accessionNumbers.Count > 0)
                        {
                            var placeholders = new List<string>();
                            for (int i = 0; i < accessionNumbers.Count; i++)
                            {
                                placeholders.Add("@acc" + i);
                                cmd.Parameters.AddWithValue("@acc" + i, accessionNumbers[i]);
                            }
                            completeQuery += " AND w.accession_number NOT IN (" + string.Join(",", placeholders) + ")";
                        }
                        cmd.CommandText = completeQuery;
                        completedCount = cmd.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
                closeDBConnection(ref errorString);
                return true;
            }
            catch (Exception ex)
            {
                try { transaction?.Rollback(); } catch { }
                closeDBConnection(ref errorString);
                errorString = "Syncing CARE worklist to care_worklist failed with exception " + ex.Message;
                insertedCount = 0;
                completedCount = 0;
                return false;
            }
        }


        /// <summary>
        /// Brings care_devices in line with the DICOM devices CARE lists for a DICOM Enabler: each device
        /// is inserted, or updated with CARE's latest values, and the enabler's rows for devices CARE no
        /// longer lists (removed, or connected to another enabler) are deleted. One transaction.
        /// </summary>
        public bool SyncCareDevices(string dicomEnablerId, List<CareDeviceRecord> devices, ref int savedCount, ref int deletedCount, ref string errorString)
        {
            savedCount = 0;
            deletedCount = 0;
            if (string.IsNullOrWhiteSpace(dicomEnablerId))
            {
                errorString = "Saving care_devices needs the DICOM Enabler id.";
                return false;
            }
            MySqlTransaction transaction = null;
            try
            {
                if (!openDBConnection(ref errorString))
                    return false;
                transaction = conConnection.BeginTransaction();

                var deviceIds = new List<string>();
                foreach (CareDeviceRecord device in devices ?? new List<CareDeviceRecord>())
                {
                    if (string.IsNullOrWhiteSpace(device?.DeviceId))
                        continue;
                    deviceIds.Add(device.DeviceId.Trim());
                    UpsertCareDevice(device, dicomEnablerId.Trim(), transaction);
                    savedCount++;
                }

                using (MySqlCommand cmd = new MySqlCommand(string.Empty, conConnection, transaction))
                {
                    string deleteQuery = "DELETE FROM care_devices WHERE dicom_enabler_id = @dicom_enabler_id";
                    cmd.Parameters.AddWithValue("@dicom_enabler_id", dicomEnablerId.Trim());
                    if (deviceIds.Count > 0)
                    {
                        var placeholders = new List<string>();
                        for (int i = 0; i < deviceIds.Count; i++)
                        {
                            placeholders.Add("@id" + i);
                            cmd.Parameters.AddWithValue("@id" + i, deviceIds[i]);
                        }
                        deleteQuery += " AND device_id NOT IN (" + string.Join(",", placeholders) + ")";
                    }
                    cmd.CommandText = deleteQuery;
                    deletedCount = cmd.ExecuteNonQuery();
                }

                transaction.Commit();
                closeDBConnection(ref errorString);
                return true;
            }
            catch (Exception ex)
            {
                try { transaction?.Rollback(); } catch { }
                closeDBConnection(ref errorString);
                errorString = "Saving care_devices failed with exception " + ex.Message;
                return false;
            }
        }


        /// <summary>
        /// Inserts or updates one care_devices row (a DICOM Enabler is saved this way), in one transaction.
        /// The row's dicom_enabler_id is the record's (null for a DICOM Enabler).
        /// </summary>
        public bool SaveCareDevice(CareDeviceRecord device, ref string errorString)
        {
            if (string.IsNullOrWhiteSpace(device?.DeviceId))
            {
                errorString = "Saving to care_devices needs the device id.";
                return false;
            }
            MySqlTransaction transaction = null;
            try
            {
                if (!openDBConnection(ref errorString))
                    return false;
                transaction = conConnection.BeginTransaction();
                UpsertCareDevice(device, device.DicomEnablerId, transaction);
                transaction.Commit();
                closeDBConnection(ref errorString);
                return true;
            }
            catch (Exception ex)
            {
                try { transaction?.Rollback(); } catch { }
                closeDBConnection(ref errorString);
                errorString = "Saving to care_devices failed with exception " + ex.Message;
                return false;
            }
        }


        /// <summary>
        /// Deletes a DICOM Enabler's row and the rows of the DICOM devices saved for it, in one transaction,
        /// once it is no longer this enabler's (replaced or cleared in the Configuration tab).
        /// </summary>
        public bool DeleteCareDevicesOfEnabler(string dicomEnablerId, ref int deletedCount, ref string errorString)
        {
            deletedCount = 0;
            if (string.IsNullOrWhiteSpace(dicomEnablerId))
                return true;
            try
            {
                if (!openDBConnection(ref errorString))
                    return false;
                using (MySqlCommand cmd = new MySqlCommand(
                    "DELETE FROM care_devices WHERE device_id = @id OR dicom_enabler_id = @id", conConnection))
                {
                    cmd.Parameters.AddWithValue("@id", dicomEnablerId.Trim());
                    deletedCount = cmd.ExecuteNonQuery();
                }
                closeDBConnection(ref errorString);
                return true;
            }
            catch (Exception ex)
            {
                closeDBConnection(ref errorString);
                errorString = "Deleting from care_devices failed with exception " + ex.Message;
                return false;
            }
        }


        /// <summary>
        /// Sets assignment_status of the given care_devices rows (rows not saved are skipped), after CARE was
        /// told the same: Sync sets the DICOM Enabler and its devices to assigned in CARE. updatedCount is
        /// the number of rows whose status changed.
        /// </summary>
        public bool SetCareDevicesAssignmentStatus(IEnumerable<string> deviceIds, string assignmentStatus, ref int updatedCount, ref string errorString)
        {
            updatedCount = 0;
            var ids = (deviceIds ?? Enumerable.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct().ToList();
            if (ids.Count == 0)
                return true;
            try
            {
                if (!openDBConnection(ref errorString))
                    return false;
                foreach (string id in ids)
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "UPDATE care_devices SET assignment_status = @status, updated_time = CURRENT_TIMESTAMP " +
                        "WHERE device_id = @id AND (assignment_status IS NULL OR assignment_status <> @status)", conConnection))
                    {
                        cmd.Parameters.AddWithValue("@status", assignmentStatus);
                        cmd.Parameters.AddWithValue("@id", id);
                        updatedCount += cmd.ExecuteNonQuery();
                    }
                }
                closeDBConnection(ref errorString);
                return true;
            }
            catch (Exception ex)
            {
                closeDBConnection(ref errorString);
                errorString = "Updating assignment_status in care_devices failed with exception " + ex.Message;
                return false;
            }
        }

        /// <summary>One care_devices row inserted, or updated with the record's values, inside the given transaction.</summary>
        private void UpsertCareDevice(CareDeviceRecord device, string dicomEnablerId, MySqlTransaction transaction)
        {
            const string upsertQuery =
                "INSERT INTO care_devices (device_id, facility_id, dicom_enabler_id, device_name, device_type, modality, ae_title, ip, " +
                "location_id, location_name, status, availability_status, assignment_status, last_synced_at, " +
                "store_scp_ae_title, store_scp_host, store_scp_port, modality_scp_ae_title, modality_scp_host, modality_scp_port) VALUES " +
                "(@device_id, @facility_id, @dicom_enabler_id, @device_name, @device_type, @modality, @ae_title, @ip, " +
                "@location_id, @location_name, @status, @availability_status, @assignment_status, @last_synced_at, " +
                "@store_scp_ae_title, @store_scp_host, @store_scp_port, @modality_scp_ae_title, @modality_scp_host, @modality_scp_port) " +
                "ON DUPLICATE KEY UPDATE facility_id = VALUES(facility_id), dicom_enabler_id = VALUES(dicom_enabler_id), " +
                "device_name = VALUES(device_name), device_type = VALUES(device_type), modality = VALUES(modality), ae_title = VALUES(ae_title), " +
                "ip = VALUES(ip), location_id = VALUES(location_id), location_name = VALUES(location_name), status = VALUES(status), " +
                "availability_status = VALUES(availability_status), assignment_status = VALUES(assignment_status), " +
                "last_synced_at = VALUES(last_synced_at), store_scp_ae_title = VALUES(store_scp_ae_title), " +
                "store_scp_host = VALUES(store_scp_host), store_scp_port = VALUES(store_scp_port), " +
                "modality_scp_ae_title = VALUES(modality_scp_ae_title), modality_scp_host = VALUES(modality_scp_host), " +
                "modality_scp_port = VALUES(modality_scp_port), updated_time = CURRENT_TIMESTAMP";

            using (MySqlCommand cmd = new MySqlCommand(upsertQuery, conConnection, transaction))
            {
                cmd.Parameters.AddWithValue("@device_id", device.DeviceId.Trim());
                cmd.Parameters.AddWithValue("@facility_id", DbValue(device.FacilityId));
                cmd.Parameters.AddWithValue("@dicom_enabler_id", DbValue(dicomEnablerId));
                cmd.Parameters.AddWithValue("@device_name", DbValue(device.DeviceName));
                cmd.Parameters.AddWithValue("@device_type", DbValue(device.DeviceType));
                cmd.Parameters.AddWithValue("@modality", DbValue(device.Modality));
                cmd.Parameters.AddWithValue("@ae_title", DbValue(device.AeTitle));
                cmd.Parameters.AddWithValue("@ip", DbValue(device.Ip));
                cmd.Parameters.AddWithValue("@location_id", DbValue(device.LocationId));
                cmd.Parameters.AddWithValue("@location_name", DbValue(device.LocationName));
                cmd.Parameters.AddWithValue("@status", DbValue(device.Status));
                cmd.Parameters.AddWithValue("@availability_status", DbValue(device.AvailabilityStatus));
                cmd.Parameters.AddWithValue("@assignment_status", DbValue(device.AssignmentStatus));
                cmd.Parameters.AddWithValue("@last_synced_at", DbValue(device.LastSyncedAt));
                cmd.Parameters.AddWithValue("@store_scp_ae_title", DbValue(device.StoreScpAeTitle));
                cmd.Parameters.AddWithValue("@store_scp_host", DbValue(device.StoreScpHost));
                cmd.Parameters.AddWithValue("@store_scp_port", device.StoreScpPort.HasValue ? (object)device.StoreScpPort.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@modality_scp_ae_title", DbValue(device.ModalityScpAeTitle));
                cmd.Parameters.AddWithValue("@modality_scp_host", DbValue(device.ModalityScpHost));
                cmd.Parameters.AddWithValue("@modality_scp_port", device.ModalityScpPort.HasValue ? (object)device.ModalityScpPort.Value : DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }


        /// <summary>
        /// The care_devices rows of a DICOM Enabler, by modality then name. Empty when there are none
        /// or they cannot be read (errorString is then set).
        /// </summary>
        public List<CareDeviceRecord> GetCareDevices(string dicomEnablerId, ref string errorString)
        {
            var devices = new List<CareDeviceRecord>();
            if (string.IsNullOrWhiteSpace(dicomEnablerId))
                return devices;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT * FROM care_devices WHERE dicom_enabler_id = @dicom_enabler_id ORDER BY modality, device_name",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@dicom_enabler_id", dicomEnablerId.Trim());
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                devices.Add(ReadCareDevice(reader));
                        }
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                closeDBConnection(ref errorString);
                errorString = "Reading care_devices failed with exception " + ex.Message;
                devices.Clear();
            }
            return devices;
        }


        /// <summary>One care_devices row by CARE device id, or null when it is not there or cannot be read.</summary>
        public CareDeviceRecord GetCareDevice(string deviceId, ref string errorString)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return null;
            CareDeviceRecord device = null;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand("SELECT * FROM care_devices WHERE device_id = @device_id", conConnection))
                    {
                        cmd.Parameters.AddWithValue("@device_id", deviceId.Trim());
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                device = ReadCareDevice(reader);
                        }
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                closeDBConnection(ref errorString);
                errorString = "Reading care_devices failed with exception " + ex.Message;
                device = null;
            }
            return device;
        }

        private static CareDeviceRecord ReadCareDevice(MySqlDataReader reader)
        {
            return new CareDeviceRecord
            {
                DeviceId = DbString(reader, "device_id"),
                FacilityId = DbString(reader, "facility_id"),
                DicomEnablerId = DbString(reader, "dicom_enabler_id"),
                DeviceName = DbString(reader, "device_name"),
                DeviceType = DbString(reader, "device_type"),
                Modality = DbString(reader, "modality"),
                AeTitle = DbString(reader, "ae_title"),
                Ip = DbString(reader, "ip"),
                LocationId = DbString(reader, "location_id"),
                LocationName = DbString(reader, "location_name"),
                Status = DbString(reader, "status"),
                AvailabilityStatus = DbString(reader, "availability_status"),
                AssignmentStatus = DbString(reader, "assignment_status"),
                LastSyncedAt = DbString(reader, "last_synced_at"),
                StoreScpAeTitle = DbString(reader, "store_scp_ae_title"),
                StoreScpHost = DbString(reader, "store_scp_host"),
                StoreScpPort = reader["store_scp_port"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["store_scp_port"]),
                ModalityScpAeTitle = DbString(reader, "modality_scp_ae_title"),
                ModalityScpHost = DbString(reader, "modality_scp_host"),
                ModalityScpPort = reader["modality_scp_port"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["modality_scp_port"]),
                UpdatedTime = reader["updated_time"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["updated_time"])
            };
        }


        /// <summary>
        /// Returns the SCHEDULED care_worklist rows for a facility with their care_patient and
        /// care_service_request details, the worklist the MWL service shares with modalities. Only
        /// rows whose scan has not started are returned (scan_status NULL), so a study that is in
        /// progress, completed or cancelled is not offered to the modality again. Only rows for that
        /// facility are returned, and when modality is set, only service requests scheduled on a DICOM
        /// device of that modality (care_service_request_device.device_modality), or, for a request
        /// with no device modality stored, whose own modality is that one. A request linked to no DICOM
        /// device (saved by a whole-facility fetch) has no modality from CARE and is returned for every
        /// modality; the MWL service shares it only while worklist_all_devices is on. A blank modality
        /// returns every modality. A blank facilityId returns nothing.
        /// </summary>
        public List<CareWorklistRecord> GetScheduledCareWorklist(string facilityId, string modality, ref string errorString)
        {
            var records = new List<CareWorklistRecord>();
            if (string.IsNullOrWhiteSpace(facilityId))
                return records;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT w.accession_number, w.facility_id, w.facility_name, " +
                        "sr.service_request_id, sr.name AS sr_name, sr.date AS sr_date, sr.body_site, sr.body_site_id, sr.description, sr.modality, " +
                        "sr.referring_physician, sr.procedure_id, sr.priority, sr.technician_instruction, sr.patient_instruction, " +
                        // The Scheduled Station AE Titles: the AE titles of the request's DICOM devices of the
                        // requested modality (or with none stored, for rows saved before device modalities were)
                        "(SELECT GROUP_CONCAT(DISTINCT d.device_ae_title ORDER BY d.device_ae_title SEPARATOR '|') " +
                        "  FROM care_service_request_device d WHERE d.service_request_pk = sr.pk AND d.device_ae_title IS NOT NULL " +
                        "  AND (@modality = '' OR d.device_modality IS NULL OR UPPER(d.device_modality) = UPPER(@modality))) AS device_ae_titles, " +
                        "EXISTS (SELECT 1 FROM care_service_request_device d WHERE d.service_request_pk = sr.pk AND d.device_id IS NOT NULL) AS has_devices, " +
                        "p.patient_id, p.name AS patient_name, p.gender, p.age, p.patient_uhid " +
                        "FROM care_worklist w " +
                        "LEFT JOIN care_service_request sr ON sr.pk = w.service_request_pk " +
                        "LEFT JOIN care_patient p ON p.pk = w.patient_pk " +
                        "WHERE w.status = 'SCHEDULED' AND w.scan_status IS NULL AND w.facility_id = @facility_id " +
                        "AND (@modality = '' " +
                        "  OR EXISTS (SELECT 1 FROM care_service_request_device d WHERE d.service_request_pk = sr.pk AND UPPER(d.device_modality) = UPPER(@modality)) " +
                        "  OR (UPPER(sr.modality) = UPPER(@modality) AND NOT EXISTS (SELECT 1 FROM care_service_request_device d " +
                        "      WHERE d.service_request_pk = sr.pk AND d.device_modality IS NOT NULL)) " +
                        "  OR NOT EXISTS (SELECT 1 FROM care_service_request_device d WHERE d.service_request_pk = sr.pk AND d.device_id IS NOT NULL)) " +
                        "ORDER BY w.pk",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@facility_id", facilityId.Trim());
                        cmd.Parameters.AddWithValue("@modality", (modality ?? string.Empty).Trim());
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                records.Add(new CareWorklistRecord
                                {
                                    AccessionNumber = DbString(reader, "accession_number"),
                                    FacilityId = DbString(reader, "facility_id"),
                                    FacilityName = DbString(reader, "facility_name"),
                                    ServiceRequestId = DbString(reader, "service_request_id"),
                                    ServiceRequestName = DbString(reader, "sr_name"),
                                    ServiceRequestDate = reader["sr_date"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["sr_date"]),
                                    ServiceRequestBodySite = DbString(reader, "body_site"),
                                    ServiceRequestBodySiteId = DbString(reader, "body_site_id"),
                                    ServiceRequestDescription = DbString(reader, "description"),
                                    ServiceRequestModality = DbString(reader, "modality"),
                                    ServiceRequestReferringPhysician = DbString(reader, "referring_physician"),
                                    DeviceAeTitles = (DbString(reader, "device_ae_titles") ?? string.Empty)
                                                     .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                                    HasDevices = reader["has_devices"] != DBNull.Value && Convert.ToInt32(reader["has_devices"]) != 0,
                                    ServiceRequestProcedureId = DbString(reader, "procedure_id"),
                                    ServiceRequestPriority = DbString(reader, "priority"),
                                    ServiceRequestTechnicianInstruction = DbString(reader, "technician_instruction"),
                                    ServiceRequestPatientInstruction = DbString(reader, "patient_instruction"),
                                    PatientId = DbString(reader, "patient_id"),
                                    PatientName = DbString(reader, "patient_name"),
                                    PatientGender = DbString(reader, "gender"),
                                    PatientAge = reader["age"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["age"]),
                                    PatientUhid = DbString(reader, "patient_uhid")
                                });
                            }
                        }
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = "Reading the worklist from care_worklist failed with exception " + ex.Message;
                records.Clear();
            }
            return records;
        }


        /// <summary>
        /// True when care_worklist has a row with the accession number, whether or not it is linked
        /// to a care_patient row. A row that is found has fetched_at set to now on its first read and
        /// last_fetched_at set to now on every read.
        /// </summary>
        public bool IsAccessionNoInCareWorklist(string accessionNo, ref string errorString)
        {
            bool found = false;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM care_worklist WHERE accession_number = @accession_number",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@accession_number", accessionNo ?? string.Empty);
                        found = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }

                    if (found)
                    {
                        using (MySqlCommand cmd = new MySqlCommand(
                            "UPDATE care_worklist SET fetched_at = COALESCE(fetched_at, NOW()), last_fetched_at = NOW(), updated_time = updated_time WHERE accession_number = @accession_number",
                            conConnection))
                        {
                            cmd.Parameters.AddWithValue("@accession_number", accessionNo);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = $"Checking care_worklist for Accession No {accessionNo} failed with exception " + ex.Message;
                found = false;
            }
            return found;
        }


        /// <summary>
        /// The CARE service request id and scan_status of the care_worklist row with an accession
        /// number. Returns false when the accession number is not in care_worklist.
        /// </summary>
        public bool GetScanState(string accessionNo, ref string serviceRequestId, ref string scanStatus, ref string errorString)
        {
            bool found = false;
            serviceRequestId = null;
            scanStatus = null;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT sr.service_request_id, w.scan_status FROM care_worklist w " +
                        "LEFT JOIN care_service_request sr ON sr.pk = w.service_request_pk WHERE w.accession_number = @accession_number",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@accession_number", accessionNo ?? string.Empty);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                found = true;
                                serviceRequestId = DbString(reader, "service_request_id");
                                scanStatus = DbString(reader, "scan_status");
                            }
                        }
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = $"Reading the scan status for Accession No {accessionNo} failed with exception " + ex.Message;
                found = false;
            }
            return found;
        }


        /// <summary>
        /// Sets scan_status on the care_worklist row with an accession number to START_PENDING, STARTED,
        /// COMPLETED, CANCEL_PENDING or CANCELLED, with scan_started_at, scan_completed_at or
        /// scan_cancelled_at set to now for STARTED, COMPLETED or CANCELLED.
        /// </summary>
        public bool SetScanStatus(string accessionNo, string scanStatus, ref string errorString)
        {
            bool updated = false;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "UPDATE care_worklist SET scan_status = @scan_status, " +
                        "scan_started_at = IF(@scan_status = 'STARTED', NOW(), scan_started_at), " +
                        "scan_completed_at = IF(@scan_status = 'COMPLETED', NOW(), scan_completed_at), " +
                        "scan_cancelled_at = IF(@scan_status = 'CANCELLED', NOW(), scan_cancelled_at), " +
                        "updated_time = updated_time WHERE accession_number = @accession_number",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@scan_status", scanStatus);
                        cmd.Parameters.AddWithValue("@accession_number", accessionNo);
                        updated = cmd.ExecuteNonQuery() > 0;
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = $"Setting the scan status for Accession No {accessionNo} failed with exception " + ex.Message;
                updated = false;
            }
            return updated;
        }


        /// <summary>
        /// The facility's care_worklist rows with a scan_status (START_PENDING: IN_PROGRESS not sent yet;
        /// STARTED: IN_PROGRESS sent, COMPLETED not yet; CANCEL_PENDING: CANCELLED not sent yet), with their service request id and when a
        /// file with the accession number was last sent to CARE (the latest care_sync_upload
        /// last_retry_time, or scan_started_at when there is none), and how many of its care_sync_upload
        /// rows are SUCCESS and FAILED.
        /// </summary>
        public List<ScanInProgress> GetScansWithStatus(string facilityId, string scanStatus, ref string errorString)
        {
            var scans = new List<ScanInProgress>();
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT w.accession_number, sr.service_request_id, " +
                        "COALESCE((SELECT MAX(u.last_retry_time) FROM care_sync_upload u WHERE u.accession_number = w.accession_number), w.scan_started_at) AS last_upload_time, " +
                        "(SELECT COUNT(*) FROM care_sync_upload u WHERE u.accession_number = w.accession_number AND u.status = 'SUCCESS') AS success_count, " +
                        "(SELECT COUNT(*) FROM care_sync_upload u WHERE u.accession_number = w.accession_number AND u.status = 'FAILED') AS failed_count " +
                        "FROM care_worklist w LEFT JOIN care_service_request sr ON sr.pk = w.service_request_pk " +
                        "WHERE w.scan_status = @scan_status AND w.facility_id = @facility_id",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@scan_status", scanStatus);
                        cmd.Parameters.AddWithValue("@facility_id", facilityId ?? string.Empty);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                scans.Add(new ScanInProgress
                                {
                                    AccessionNumber = DbString(reader, "accession_number"),
                                    ServiceRequestId = DbString(reader, "service_request_id"),
                                    LastUploadTime = reader["last_upload_time"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["last_upload_time"]),
                                    SuccessfulUploads = Convert.ToInt32(reader["success_count"]),
                                    FailedUploads = Convert.ToInt32(reader["failed_count"])
                                });
                            }
                        }
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = "Reading the scans in progress failed with exception " + ex.Message;
                scans.Clear();
            }
            return scans;
        }


        /// <summary>
        /// The care_sync_upload instance_key for a DICOM instance: StudyInstanceUID|SeriesInstanceUID|SOPInstanceUID
        /// AES-encrypted. The IV is fixed, so the same instance always gives the same key and a file is
        /// matched to its row by its instance, not by its file name.
        /// </summary>
        public static string BuildInstanceKey(string studyUid, string seriesUid, string sopInstanceUid)
        {
            return ucls_EnDcryption.EncryptString(EncKey.encdeKey, $"{studyUid}|{seriesUid}|{sopInstanceUid}");
        }


        /// <summary>
        /// The status, retry_count and last_retry_time of the care_sync_upload row for an instance_key. Returns false when the instance has no row (it has not been uploaded yet).
        /// </summary>
        public bool GetUploadRetryState(string instanceKey, ref string status, ref int retryCount, ref DateTime? lastRetryTime, ref string errorString)
        {
            bool found = false;
            status = null;
            retryCount = 0;
            lastRetryTime = null;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT status, retry_count, last_retry_time FROM care_sync_upload WHERE instance_key = @instance_key",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@instance_key", instanceKey);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                found = true;
                                status = DbString(reader, "status");
                                retryCount = Convert.ToInt32(reader["retry_count"]);
                                if (reader["last_retry_time"] != DBNull.Value)
                                    lastRetryTime = Convert.ToDateTime(reader["last_retry_time"]);
                            }
                        }
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = "Reading the upload retry state failed with exception " + ex.Message;
                found = false;
            }
            return found;
        }


        /// <summary>
        /// Sets last_retry_time to now and log to the given reason on the FAILED care_sync_upload row
        /// for an instance_key, without changing retry_count, for a retry skipped or failed because CARE
        /// could not be reached.
        /// </summary>
        public bool UpdateUploadRetryTime(string instanceKey, string log, ref string errorString)
        {
            bool updated = false;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "UPDATE care_sync_upload SET last_retry_time = NOW(), log = @log WHERE instance_key = @instance_key AND status = 'FAILED'",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@instance_key", instanceKey);
                        cmd.Parameters.AddWithValue("@log", DbValue(log));
                        updated = cmd.ExecuteNonQuery() > 0;
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = "Updating the upload retry time failed with exception " + ex.Message;
                updated = false;
            }
            return updated;
        }


        /// <summary>
        /// Records the outcome of uploading one DICOM instance to CARE in care_sync_upload. The row is
        /// found by instanceKey (see BuildInstanceKey), so a retry of the same instance updates its
        /// existing row with the latest status, log and file name and increments retry_count, even if
        /// the file was renamed or resent. last_retry_time is set to the attempt time on the first upload
        /// and on every retry, and retryCount returns the row's retry_count after the save.
        /// worklist_pk is set from the care_worklist row with the accession number, when there is one,
        /// and studyUid is added to that row's study_uid (comma-separated) if it is not already there.
        /// </summary>
        public bool SaveStudyUpload(string instanceKey, string studyUid, string accessionNumber, string fileName, string status, string log, ref int retryCount, ref string errorString)
        {
            bool saved = false;
            retryCount = 0;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "INSERT INTO care_sync_upload (worklist_pk, instance_key, study_uid, accession_number, file_name, status, log, last_retry_time) VALUES " +
                        "((SELECT pk FROM care_worklist WHERE accession_number = @accession_number LIMIT 1), @instance_key, @study_uid, @accession_number, @file_name, @status, @log, NOW()) " +
                        "ON DUPLICATE KEY UPDATE worklist_pk = COALESCE(VALUES(worklist_pk), worklist_pk), file_name = VALUES(file_name), status = VALUES(status), log = VALUES(log), " +
                        "retry_count = retry_count + 1, last_retry_time = VALUES(last_retry_time)",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@instance_key", instanceKey);
                        cmd.Parameters.AddWithValue("@study_uid", studyUid ?? string.Empty);
                        cmd.Parameters.AddWithValue("@accession_number", DbValue(accessionNumber));
                        cmd.Parameters.AddWithValue("@file_name", fileName ?? string.Empty);
                        cmd.Parameters.AddWithValue("@status", status);
                        cmd.Parameters.AddWithValue("@log", DbValue(log));
                        cmd.ExecuteNonQuery();
                        saved = true;
                    }

                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT retry_count FROM care_sync_upload WHERE instance_key = @instance_key",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@instance_key", instanceKey);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                retryCount = Convert.ToInt32(reader["retry_count"]);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(studyUid) && !string.IsNullOrWhiteSpace(accessionNumber))
                    {
                        using (MySqlCommand cmd = new MySqlCommand(
                            "UPDATE care_worklist SET study_uid = IF(study_uid IS NULL OR study_uid = '', @study_uid, CONCAT(study_uid, ',', @study_uid)), " +
                            "updated_time = updated_time " +
                            "WHERE accession_number = @accession_number AND (study_uid IS NULL OR FIND_IN_SET(@study_uid, study_uid) = 0)",
                            conConnection))
                        {
                            cmd.Parameters.AddWithValue("@study_uid", studyUid);
                            cmd.Parameters.AddWithValue("@accession_number", accessionNumber);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = $"Saving upload status for file {fileName} failed with exception " + ex.Message;
            }
            return saved;
        }


        private static object DbValue(string value)
        {
            return string.IsNullOrEmpty(value) ? (object)DBNull.Value : value;
        }

        private static string DbString(MySqlDataReader reader, string column)
        {
            return reader[column] == DBNull.Value ? null : reader[column].ToString();
        }


    }


    /// <summary>
    /// A care_worklist row with a scan_status, e.g. IN_PROGRESS sent to CARE but not yet COMPLETED.
    /// </summary>
    public class ScanInProgress
    {
        public string AccessionNumber { get; set; }
        public string ServiceRequestId { get; set; }
        public DateTime? LastUploadTime { get; set; }
        public int SuccessfulUploads { get; set; }
        public int FailedUploads { get; set; }
    }


    /// <summary>
    /// One CARE worklist API result, split by SyncCareWorklist across care_service_request,
    /// care_service_request_device, care_patient and care_worklist. Devices is filled only from the
    /// API response; GetScheduledCareWorklist leaves it empty.
    /// </summary>
    public class CareWorklistRecord
    {
        public string AccessionNumber { get; set; }
        public string ServiceRequestId { get; set; }
        public string ServiceRequestName { get; set; }
        public DateTime? ServiceRequestDate { get; set; }
        public string ServiceRequestBodySite { get; set; }
        public string ServiceRequestBodySiteId { get; set; }
        public string ServiceRequestDescription { get; set; }
        public string ServiceRequestModality { get; set; }
        public string ServiceRequestReferringPhysician { get; set; }
        public string ServiceRequestProcedureId { get; set; }
        public string ServiceRequestPriority { get; set; }
        public string ServiceRequestTechnicianInstruction { get; set; }
        public string ServiceRequestPatientInstruction { get; set; }
        public string FacilityId { get; set; }
        public string FacilityName { get; set; }
        public string PatientId { get; set; }
        public string PatientName { get; set; }
        public string PatientGender { get; set; }
        public int? PatientAge { get; set; }
        public string PatientUhid { get; set; }
        public List<CareWorklistDevice> Devices { get; set; } = new List<CareWorklistDevice>();
        /// <summary>
        /// Read by GetScheduledCareWorklist: the AE titles of the DICOM devices the request is scheduled
        /// on (of the requested modality), its Scheduled Station AE Titles (0040,0001).
        /// </summary>
        public List<string> DeviceAeTitles { get; set; } = new List<string>();
        /// <summary>
        /// Read by GetScheduledCareWorklist: true when the request is mapped to any DICOM device in CARE.
        /// Such a request is only sent to its devices' AE titles; one with no device goes to every device.
        /// </summary>
        public bool HasDevices { get; set; }
    }


    /// <summary>
    /// One care_service_request_device row: a DICOM device a service request is scheduled on, with
    /// its location. The device fields are null for a location without devices. DeviceAeTitle is the
    /// Scheduled Station AE Title (0040,0001) of the worklist items for that device, not a calling AE.
    /// </summary>
    public class CareWorklistDevice
    {
        public string LocationId { get; set; }
        public string LocationName { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string DeviceAeTitle { get; set; }
        public string DeviceModality { get; set; }
    }


    /// <summary>
    /// One care_devices row, one column per field CARE returns: a DICOM device (modality machine) connected
    /// to the selected DICOM Enabler (DicomEnablerId set; Modality, AeTitle, Ip from its machine), or the
    /// DICOM Enabler itself (DeviceType dicom-enabler, DicomEnablerId null) with its DICOM endpoints.
    /// </summary>
    public class CareDeviceRecord
    {
        public string DeviceId { get; set; }
        public string FacilityId { get; set; }
        public string DicomEnablerId { get; set; }
        public string DeviceName { get; set; }
        public string DeviceType { get; set; }
        public string Modality { get; set; }
        public string AeTitle { get; set; }
        public string Ip { get; set; }
        public string LocationId { get; set; }
        public string LocationName { get; set; }
        public string Status { get; set; }
        public string AvailabilityStatus { get; set; }
        public string AssignmentStatus { get; set; }
        public string LastSyncedAt { get; set; }
        /// <summary>DICOM Enabler only: its Store SCP endpoint (store_scp).</summary>
        public string StoreScpAeTitle { get; set; }
        public string StoreScpHost { get; set; }
        public int? StoreScpPort { get; set; }
        /// <summary>DICOM Enabler only: its Modality Worklist SCP endpoint (modality_scp).</summary>
        public string ModalityScpAeTitle { get; set; }
        public string ModalityScpHost { get; set; }
        public int? ModalityScpPort { get; set; }
        public DateTime? UpdatedTime { get; set; }
    }
}
