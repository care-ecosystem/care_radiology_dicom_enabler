using System;

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


    }
}
