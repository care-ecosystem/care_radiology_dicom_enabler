using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

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
        /// care_config. Each enabler serves one facility, so every calling AE gets the same value;
        /// callingAET is only used in the error message.
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
                else if (string.IsNullOrWhiteSpace(facilityId))
                    resolvedFrom = "no Facility ID entered in the Configuration tab";
                else
                    resolvedFrom = "facility_id in the Configuration tab";
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
        /// Brings the local CARE tables in line with one CARE worklist response. Each item's patient
        /// and service request are inserted into care_patient and care_service_request, or refreshed
        /// with the latest values if already there. Accession numbers not yet in care_worklist are
        /// inserted, linked to that patient and service request; worklist rows already there are left
        /// untouched. Rows still SCHEDULED whose accession number is not in this response are marked
        /// COMPLETED, but only rows for the facilityId and modality the response was fetched with
        /// (a blank modality covers every modality), so changing either in the Configuration tab leaves
        /// the other facility's or modality's rows as they are. With a blank facilityId no row is marked
        /// COMPLETED. Call only with a response the CARE API reported as successful - an empty list
        /// marks every scheduled row for that facility and modality completed.
        /// </summary>
        public bool SyncCareWorklist(List<CareWorklistRecord> records, string facilityId, string modality, ref int insertedCount, ref int completedCount, ref string errorString)
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
                    "INSERT INTO care_service_request (service_request_id, name, date, body_site, description, modality, procedure_id, priority, " +
                    "technician_instruction, patient_instruction, created_by_prefix, created_by_first_name, created_by_last_name) VALUES " +
                    "(@service_request_id, @name, @date, @body_site, @description, @modality, @procedure_id, @priority, " +
                    "@technician_instruction, @patient_instruction, @created_by_prefix, @created_by_first_name, @created_by_last_name) " +
                    "ON DUPLICATE KEY UPDATE name = VALUES(name), date = VALUES(date), body_site = VALUES(body_site), description = VALUES(description), " +
                    "modality = VALUES(modality), procedure_id = VALUES(procedure_id), priority = VALUES(priority), " +
                    "technician_instruction = VALUES(technician_instruction), patient_instruction = VALUES(patient_instruction), " +
                    "created_by_prefix = VALUES(created_by_prefix), created_by_first_name = VALUES(created_by_first_name), created_by_last_name = VALUES(created_by_last_name)";

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
                            cmd.Parameters.AddWithValue("@description", DbValue(record.ServiceRequestDescription));
                            cmd.Parameters.AddWithValue("@modality", DbValue(record.ServiceRequestModality));
                            cmd.Parameters.AddWithValue("@procedure_id", DbValue(record.ServiceRequestProcedureId));
                            cmd.Parameters.AddWithValue("@priority", DbValue(record.ServiceRequestPriority));
                            cmd.Parameters.AddWithValue("@technician_instruction", DbValue(record.ServiceRequestTechnicianInstruction));
                            cmd.Parameters.AddWithValue("@patient_instruction", DbValue(record.ServiceRequestPatientInstruction));
                            cmd.Parameters.AddWithValue("@created_by_prefix", DbValue(record.CreatedByPrefix));
                            cmd.Parameters.AddWithValue("@created_by_first_name", DbValue(record.CreatedByFirstName));
                            cmd.Parameters.AddWithValue("@created_by_last_name", DbValue(record.CreatedByLastName));
                            cmd.ExecuteNonQuery();
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
                        "UPDATE care_worklist w LEFT JOIN care_service_request sr ON sr.pk = w.service_request_pk " +
                        "SET w.status = 'COMPLETED' WHERE w.status <> 'COMPLETED' AND w.facility_id = @facility_id " +
                        "AND (@modality = '' OR sr.modality = @modality)";
                    using (MySqlCommand cmd = new MySqlCommand(string.Empty, conConnection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@facility_id", facilityId.Trim());
                        cmd.Parameters.AddWithValue("@modality", (modality ?? string.Empty).Trim());
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
        /// Returns the SCHEDULED care_worklist rows for a facility with their care_patient and
        /// care_service_request details, the worklist the MWL service shares with modalities. Only
        /// rows for that facility are returned, and when modality is set, only service requests with
        /// that modality; a blank modality returns every modality. A blank facilityId returns nothing.
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
                        "sr.service_request_id, sr.name AS sr_name, sr.date AS sr_date, sr.body_site, sr.description, sr.modality, sr.procedure_id, sr.priority, " +
                        "sr.technician_instruction, sr.patient_instruction, sr.created_by_prefix, sr.created_by_first_name, sr.created_by_last_name, " +
                        "p.patient_id, p.name AS patient_name, p.gender, p.age, p.patient_uhid " +
                        "FROM care_worklist w " +
                        "LEFT JOIN care_service_request sr ON sr.pk = w.service_request_pk " +
                        "LEFT JOIN care_patient p ON p.pk = w.patient_pk " +
                        "WHERE w.status = 'SCHEDULED' AND w.facility_id = @facility_id " +
                        "AND (@modality = '' OR sr.modality = @modality) " +
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
                                    ServiceRequestDescription = DbString(reader, "description"),
                                    ServiceRequestModality = DbString(reader, "modality"),
                                    ServiceRequestProcedureId = DbString(reader, "procedure_id"),
                                    ServiceRequestPriority = DbString(reader, "priority"),
                                    ServiceRequestTechnicianInstruction = DbString(reader, "technician_instruction"),
                                    ServiceRequestPatientInstruction = DbString(reader, "patient_instruction"),
                                    CreatedByPrefix = DbString(reader, "created_by_prefix"),
                                    CreatedByFirstName = DbString(reader, "created_by_first_name"),
                                    CreatedByLastName = DbString(reader, "created_by_last_name"),
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
        /// Returns the CARE patient ID (UUID) from care_patient for the care_worklist row with an
        /// accession number, or empty when the accession number is not in care_worklist. A row that
        /// is found has fetched_at set to now on its first read and last_fetched_at set to now on
        /// every read.
        /// </summary>
        public string GetCarePatientIdByAccessionNo(string accessionNo, ref string errorString)
        {
            string patientId = string.Empty;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT p.patient_id FROM care_worklist w JOIN care_patient p ON p.pk = w.patient_pk WHERE w.accession_number = @accession_number LIMIT 1",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@accession_number", accessionNo ?? string.Empty);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            patientId = result.ToString();
                    }

                    if (!string.IsNullOrEmpty(patientId))
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
                errorString = $"Getting CARE patient ID for Accession No {accessionNo} failed with exception " + ex.Message;
                patientId = string.Empty;
            }
            return patientId;
        }


        /// <summary>
        /// True when care_worklist has a row with the accession number, whether or not it is linked
        /// to a care_patient row.
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
        /// The retry_count and last_retry_time of the latest FAILED care_sync_upload row for a file
        /// name. Returns false when the file has no FAILED row (it has not failed yet).
        /// </summary>
        public bool GetUploadRetryState(string fileName, ref int retryCount, ref DateTime? lastRetryTime, ref string errorString)
        {
            bool found = false;
            retryCount = 0;
            lastRetryTime = null;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT retry_count, last_retry_time FROM care_sync_upload WHERE file_name = @file_name AND status = 'FAILED' " +
                        "ORDER BY last_retry_time DESC LIMIT 1",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@file_name", fileName ?? string.Empty);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                found = true;
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
                errorString = $"Reading the upload retry state for file {fileName} failed with exception " + ex.Message;
                found = false;
            }
            return found;
        }


        /// <summary>
        /// Sets last_retry_time to now and log to the given reason on the latest FAILED care_sync_upload
        /// row for a file name, without changing retry_count, for a retry skipped or failed because CARE
        /// could not be reached.
        /// </summary>
        public bool UpdateUploadRetryTime(string fileName, string log, ref string errorString)
        {
            bool updated = false;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "UPDATE care_sync_upload SET last_retry_time = NOW(), log = @log WHERE file_name = @file_name AND status = 'FAILED' " +
                        "ORDER BY last_retry_time DESC LIMIT 1",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@file_name", fileName ?? string.Empty);
                        cmd.Parameters.AddWithValue("@log", DbValue(log));
                        updated = cmd.ExecuteNonQuery() > 0;
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = $"Updating the upload retry time for file {fileName} failed with exception " + ex.Message;
                updated = false;
            }
            return updated;
        }


        /// <summary>
        /// Records the outcome of uploading one DICOM file to CARE in care_sync_upload. A retry of
        /// the same file updates its existing row with the latest status and log and increments
        /// retry_count. last_retry_time is set to the attempt time on the first upload and on every
        /// retry, and retryCount returns the row's retry_count after the save.
        /// worklist_pk is set from the care_worklist row with the accession number, when there is one,
        /// and studyUid is added to that row's study_uid (comma-separated) if it is not already there.
        /// </summary>
        public bool SaveStudyUpload(string studyUid, string accessionNumber, string fileName, string status, string log, ref int retryCount, ref string errorString)
        {
            bool saved = false;
            retryCount = 0;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "INSERT INTO care_sync_upload (worklist_pk, study_uid, accession_number, file_name, status, log, last_retry_time) VALUES " +
                        "((SELECT pk FROM care_worklist WHERE accession_number = @accession_number LIMIT 1), @study_uid, @accession_number, @file_name, @status, @log, NOW()) " +
                        "ON DUPLICATE KEY UPDATE worklist_pk = COALESCE(VALUES(worklist_pk), worklist_pk), status = VALUES(status), log = VALUES(log), " +
                        "retry_count = retry_count + 1, last_retry_time = VALUES(last_retry_time)",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@study_uid", studyUid ?? string.Empty);
                        cmd.Parameters.AddWithValue("@accession_number", DbValue(accessionNumber));
                        cmd.Parameters.AddWithValue("@file_name", fileName ?? string.Empty);
                        cmd.Parameters.AddWithValue("@status", status);
                        cmd.Parameters.AddWithValue("@log", DbValue(log));
                        cmd.ExecuteNonQuery();
                        saved = true;
                    }

                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT retry_count FROM care_sync_upload WHERE study_uid = @study_uid AND file_name = @file_name",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@study_uid", studyUid ?? string.Empty);
                        cmd.Parameters.AddWithValue("@file_name", fileName ?? string.Empty);
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
    /// One CARE worklist API result, split by SyncCareWorklist across care_service_request,
    /// care_patient and care_worklist.
    /// </summary>
    public class CareWorklistRecord
    {
        public string AccessionNumber { get; set; }
        public string ServiceRequestId { get; set; }
        public string ServiceRequestName { get; set; }
        public DateTime? ServiceRequestDate { get; set; }
        public string ServiceRequestBodySite { get; set; }
        public string ServiceRequestDescription { get; set; }
        public string ServiceRequestModality { get; set; }
        public string ServiceRequestProcedureId { get; set; }
        public string ServiceRequestPriority { get; set; }
        public string ServiceRequestTechnicianInstruction { get; set; }
        public string ServiceRequestPatientInstruction { get; set; }
        public string CreatedByPrefix { get; set; }
        public string CreatedByFirstName { get; set; }
        public string CreatedByLastName { get; set; }
        public string FacilityId { get; set; }
        public string FacilityName { get; set; }
        public string PatientId { get; set; }
        public string PatientName { get; set; }
        public string PatientGender { get; set; }
        public int? PatientAge { get; set; }
        public string PatientUhid { get; set; }
    }
}
