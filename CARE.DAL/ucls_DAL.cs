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
        /// <param name="facilityId"></param>
        /// <param name="description"></param>
        /// <param name="updateServer"></param>
        /// <param name="errorString"></param>
        /// <returns></returns>
        public bool insertorUpdateServer(string serverName,string aetitle,string hostaddress,string port,string facilityId,string description,string primarykey,bool updateServer , ref string errorString)
        {
            try
            {
                string query = string.Empty;
                if ( openDBConnection(ref errorString))
                {
                    if (!updateServer)
                    {
                        query = "INSERT INTO dcm_servers(name,aetitle,hostaddress,portnumber,facilityid,description) " +
                            "VALUES ('" + serverName + "','" + aetitle + "','" + hostaddress + "','" + port + "','" + facilityId + "','" + description + "')";
                    }
                    else
                    {
                        query = "UPDATE dcm_servers SET name='"+serverName+ "',aetitle='" + aetitle + "',hostaddress='" + hostaddress + "',portnumber='" + port + "',facilityid='" + facilityId + "'," +
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
                    string query = "SELECT pk,name,aetitle,hostaddress,portnumber,facilityid,description FROM dcm_servers";
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
        /// <param name="errorString"></param>
        /// <returns></returns>
        public string InsertOrUpdateStudyInfo(string patientId, string accesionNo, string studyInstanceId, string seriesInstanceId, string seriesNo, string modality,
            string bodyPart, string seriesDesc, string instName, string stationName, string departmentName, string imageInstanceId, int studystatus , ref string errorString)
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
        /// Get the Facility ID to filter the CARE worklist by, from the Facility ID column of the
        /// Server List.
        /// </summary>
        /// <param name="resolvedFrom">Set to a human-readable description of how the value was found,
        /// for logging - or why it could not be.</param>
        public string GetFacilityId(string callingAET, ref string resolvedFrom, ref string errorString)
        {
            string facilityId = string.Empty;
            resolvedFrom = "not resolved";
            try
            {
                if (openDBConnection(ref errorString))
                {
                    // 1. Exact match on the querying modality's AE title.
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT facilityid FROM dcm_servers WHERE aetitle = @aetitle AND facilityid IS NOT NULL AND facilityid <> '' LIMIT 1",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@aetitle", callingAET ?? string.Empty);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            facilityId = result.ToString();
                            resolvedFrom = $"Server List row for AE {callingAET}";
                        }
                    }

                    // 2. No row for this AE - fall back to the only Facility ID configured, if there
                    //    is exactly one.
                    if (string.IsNullOrWhiteSpace(facilityId))
                    {
                        var distinctIds = new System.Collections.Generic.List<string>();
                        using (MySqlCommand cmd = new MySqlCommand(
                            "SELECT DISTINCT facilityid FROM dcm_servers WHERE facilityid IS NOT NULL AND facilityid <> ''",
                            conConnection))
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                distinctIds.Add(reader.GetString(0));
                            }
                        }

                        if (distinctIds.Count == 1)
                        {
                            facilityId = distinctIds[0];
                            resolvedFrom = "the only Facility ID in the Server List";
                        }
                        else if (distinctIds.Count > 1)
                        {
                            resolvedFrom = $"ambiguous - {distinctIds.Count} different Facility IDs in the Server List and no row matches AE {callingAET}; add a row for this AE title to disambiguate";
                        }
                        else
                        {
                            resolvedFrom = "no Facility ID entered in the Server List";
                        }
                    }
                }
                closeDBConnection(ref errorString);
            }
            catch (Exception ex)
            {
                errorString = $"Getting Facility ID for AETitle {callingAET} failed with expection" + ex.Message;
                facilityId = string.Empty;
            }
            return facilityId;
        }


        /// <summary>
        /// Brings the local care_worklist table in line with one CARE worklist response. Accession
        /// numbers not yet in the table are inserted; rows already there are left untouched. Rows
        /// still SCHEDULED whose accession number is not in this response are marked COMPLETED.
        /// Call only with a response the CARE API reported as successful - an empty list marks every
        /// scheduled row completed.
        /// </summary>
        public bool SyncCareWorklist(List<CareWorklistRecord> records, ref int insertedCount, ref int completedCount, ref string errorString)
        {
            insertedCount = 0;
            completedCount = 0;
            MySqlTransaction transaction = null;
            try
            {
                if (!openDBConnection(ref errorString))
                    return false;

                transaction = conConnection.BeginTransaction();

                const string insertQuery =
                    "INSERT IGNORE INTO care_worklist (accession_number, status, service_request_id, service_request_external_id, service_request_name, " +
                    "service_request_date, service_request_body_site, service_request_description, service_request_modality, service_request_procedure_id, " +
                    "service_request_priority, service_request_technician_instruction, service_request_patient_instruction, created_by_prefix, " +
                    "created_by_first_name, created_by_last_name, facility_id, facility_name, patient_id, patient_external_id, patient_name, " +
                    "patient_address, patient_phone_number, patient_gender, patient_age, patient_uhid) VALUES " +
                    "(@accession_number, 'SCHEDULED', @service_request_id, @service_request_external_id, @service_request_name, " +
                    "@service_request_date, @service_request_body_site, @service_request_description, @service_request_modality, @service_request_procedure_id, " +
                    "@service_request_priority, @service_request_technician_instruction, @service_request_patient_instruction, @created_by_prefix, " +
                    "@created_by_first_name, @created_by_last_name, @facility_id, @facility_name, @patient_id, @patient_external_id, @patient_name, " +
                    "@patient_address, @patient_phone_number, @patient_gender, @patient_age, @patient_uhid)";

                var accessionNumbers = new List<string>();
                foreach (CareWorklistRecord record in records)
                {
                    if (string.IsNullOrWhiteSpace(record.AccessionNumber))
                        continue;
                    accessionNumbers.Add(record.AccessionNumber);

                    using (MySqlCommand cmd = new MySqlCommand(insertQuery, conConnection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@accession_number", record.AccessionNumber);
                        cmd.Parameters.AddWithValue("@service_request_id", DbValue(record.ServiceRequestId));
                        cmd.Parameters.AddWithValue("@service_request_external_id", DbValue(record.ServiceRequestExternalId));
                        cmd.Parameters.AddWithValue("@service_request_name", DbValue(record.ServiceRequestName));
                        cmd.Parameters.AddWithValue("@service_request_date", record.ServiceRequestDate.HasValue ? (object)record.ServiceRequestDate.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@service_request_body_site", DbValue(record.ServiceRequestBodySite));
                        cmd.Parameters.AddWithValue("@service_request_description", DbValue(record.ServiceRequestDescription));
                        cmd.Parameters.AddWithValue("@service_request_modality", DbValue(record.ServiceRequestModality));
                        cmd.Parameters.AddWithValue("@service_request_procedure_id", DbValue(record.ServiceRequestProcedureId));
                        cmd.Parameters.AddWithValue("@service_request_priority", DbValue(record.ServiceRequestPriority));
                        cmd.Parameters.AddWithValue("@service_request_technician_instruction", DbValue(record.ServiceRequestTechnicianInstruction));
                        cmd.Parameters.AddWithValue("@service_request_patient_instruction", DbValue(record.ServiceRequestPatientInstruction));
                        cmd.Parameters.AddWithValue("@created_by_prefix", DbValue(record.CreatedByPrefix));
                        cmd.Parameters.AddWithValue("@created_by_first_name", DbValue(record.CreatedByFirstName));
                        cmd.Parameters.AddWithValue("@created_by_last_name", DbValue(record.CreatedByLastName));
                        cmd.Parameters.AddWithValue("@facility_id", DbValue(record.FacilityId));
                        cmd.Parameters.AddWithValue("@facility_name", DbValue(record.FacilityName));
                        cmd.Parameters.AddWithValue("@patient_id", DbValue(record.PatientId));
                        cmd.Parameters.AddWithValue("@patient_external_id", DbValue(record.PatientExternalId));
                        cmd.Parameters.AddWithValue("@patient_name", DbValue(record.PatientName));
                        cmd.Parameters.AddWithValue("@patient_address", DbValue(record.PatientAddress));
                        cmd.Parameters.AddWithValue("@patient_phone_number", DbValue(record.PatientPhoneNumber));
                        cmd.Parameters.AddWithValue("@patient_gender", DbValue(record.PatientGender));
                        cmd.Parameters.AddWithValue("@patient_age", record.PatientAge.HasValue ? (object)record.PatientAge.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@patient_uhid", DbValue(record.PatientUhid));
                        insertedCount += cmd.ExecuteNonQuery();
                    }
                }

                string completeQuery = "UPDATE care_worklist SET status = 'COMPLETED' WHERE status <> 'COMPLETED'";
                using (MySqlCommand cmd = new MySqlCommand(string.Empty, conConnection, transaction))
                {
                    if (accessionNumbers.Count > 0)
                    {
                        var placeholders = new List<string>();
                        for (int i = 0; i < accessionNumbers.Count; i++)
                        {
                            placeholders.Add("@acc" + i);
                            cmd.Parameters.AddWithValue("@acc" + i, accessionNumbers[i]);
                        }
                        completeQuery += " AND accession_number NOT IN (" + string.Join(",", placeholders) + ")";
                    }
                    cmd.CommandText = completeQuery;
                    completedCount = cmd.ExecuteNonQuery();
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
        /// Returns the CARE patient external ID (UUID) saved in care_worklist for an accession number,
        /// or empty when the accession number is not in the table.
        /// </summary>
        public string GetCarePatientIdByAccessionNo(string accessionNo, ref string errorString)
        {
            string patientId = string.Empty;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT patient_external_id FROM care_worklist WHERE accession_number = @accession_number LIMIT 1",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@accession_number", accessionNo ?? string.Empty);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            patientId = result.ToString();
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
        /// Records the outcome of uploading one DICOM file to CARE in care_study_upload. A retry of
        /// the same file updates its existing row with the latest status and log and increments
        /// retry_count. When countAsFailure is true failure_count is incremented as well, and
        /// failureCount and retryCount return the row's failure_count and retry_count after the save.
        /// </summary>
        public bool SaveStudyUpload(string studyUid, string accessionNumber, string fileName, string status, string log, bool countAsFailure, ref int failureCount, ref int retryCount, ref string errorString)
        {
            bool saved = false;
            failureCount = 0;
            retryCount = 0;
            try
            {
                if (openDBConnection(ref errorString))
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "INSERT INTO care_study_upload (study_uid, accession_number, file_name, status, log, failure_count) VALUES (@study_uid, @accession_number, @file_name, @status, @log, @failure_increment) " +
                        "ON DUPLICATE KEY UPDATE status = VALUES(status), log = VALUES(log), retry_count = retry_count + 1, failure_count = failure_count + VALUES(failure_count)",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@study_uid", studyUid ?? string.Empty);
                        cmd.Parameters.AddWithValue("@accession_number", DbValue(accessionNumber));
                        cmd.Parameters.AddWithValue("@file_name", fileName ?? string.Empty);
                        cmd.Parameters.AddWithValue("@status", status);
                        cmd.Parameters.AddWithValue("@log", DbValue(log));
                        cmd.Parameters.AddWithValue("@failure_increment", countAsFailure ? 1 : 0);
                        cmd.ExecuteNonQuery();
                        saved = true;
                    }

                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT failure_count, retry_count FROM care_study_upload WHERE study_uid = @study_uid AND file_name = @file_name",
                        conConnection))
                    {
                        cmd.Parameters.AddWithValue("@study_uid", studyUid ?? string.Empty);
                        cmd.Parameters.AddWithValue("@file_name", fileName ?? string.Empty);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                failureCount = Convert.ToInt32(reader["failure_count"]);
                                retryCount = Convert.ToInt32(reader["retry_count"]);
                            }
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


    }


    /// <summary>
    /// One CARE worklist API result flattened into the columns of the care_worklist table.
    /// </summary>
    public class CareWorklistRecord
    {
        public string AccessionNumber { get; set; }
        public string ServiceRequestId { get; set; }
        public string ServiceRequestExternalId { get; set; }
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
        public string PatientExternalId { get; set; }
        public string PatientName { get; set; }
        public string PatientAddress { get; set; }
        public string PatientPhoneNumber { get; set; }
        public string PatientGender { get; set; }
        public int? PatientAge { get; set; }
        public string PatientUhid { get; set; }
    }
}
