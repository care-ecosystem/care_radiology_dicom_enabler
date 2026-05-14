using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Plexus_MWL_Service.config;

namespace Plexus_MWL_Service.Database
{
    public class ucls_DAL
    {
        MySqlConnection conConnection = new MySqlConnection();
        MySqlDataAdapter adpAdapter = new MySqlDataAdapter();
        DataSet dstDataSet = new DataSet();
        public ucls_DAL()
        {
            conConnection.ConnectionString = cls_PlexusConfig.ReadDetailsFromXML(@"/configurations/connectString");
        }


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
        private bool openDBConnection(ref string errorString)
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
        private bool closeDBConnection(ref string errorString)
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

        

        public DataSet LoadServerList(ref string errorString)
        {
            DataSet dsResult = null;
            try
            {
                if ( openDBConnection(ref errorString) )
                {     
                    string query = "SELECT pk,name,aetitle,hostaddress,portnumber,description FROM dcm_servers";
                    dsResult = new DataSet();
                    adpAdapter = new MySqlDataAdapter(query, conConnection);
                    adpAdapter.Fill(dsResult);
                    closeDBConnection(ref errorString);
                }
            }
            catch(Exception ex)
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


    }
}
