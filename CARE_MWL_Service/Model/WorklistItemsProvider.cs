// Copyright (c) 2012-2022 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).


using Org.BouncyCastle.Utilities;
using Plexus.Common.Database;
using Plexus_MWL_Service.logs;
using Sample_ModalitySCP.Model;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace Worklist_SCP.Model
{
    public class WorklistItemsProvider : IWorklistItemsSource
    {

        
        /// <summary>
        /// This method returns some hard coded worklist items - of course they should be loaded from database or some other service
        /// </summary>
        public List<WorklistItem> GetAllCurrentWorklistItems()
        {
            var item1 = new WorklistItem
            {
                AccessionNumber = "26042022100448",
                DateOfBirth = new DateTime(1980, 4, 15),
                PatientID = "100015",
                Surname = "BENSON",
                Forename = "MARIA",
                Sex = "F",
                Title = null,

                Modality = "MR",
                ExamDescription = "mr knee left",
                ExamRoom = "MR1",
                HospitalName = null,
                PerformingPhysician = null,
                ProcedureID = "200001",
                ProcedureStepID = "200002",
                StudyUID = "1.2.34.567890.1234567890.1",
                ScheduledAET = "OEC9800",
                ReferringPhysician = "Karthick^Bal^Md",
                ExamDateAndTime = DateTime.Now
            };

            var item2 = new WorklistItem
            {
                AccessionNumber = "26042022120448",
                DateOfBirth = new DateTime(1975, 2, 14),
                PatientID = "100016",
                Surname = "JOHN",
                Forename = "MILLER",
                Sex = "M",
                Title = null,

                Modality = "MR",
                ExamDescription = "mr knee right",
                ExamRoom = "MR1",
                HospitalName = null,
                PerformingPhysician = null,
                ProcedureID = "200003",
                ProcedureStepID = "200004",
                StudyUID = "1.2.34.567890.1234567890.2",
                ScheduledAET = "OEC9800",
                ReferringPhysician = "Karthick^Bal^Md",
                ExamDateAndTime = DateTime.Now
            };

            var item3 = new WorklistItem
            {
                AccessionNumber = "25042022160448",
                DateOfBirth = new DateTime(1984, 10, 2),
                PatientID = "100019",
                Surname = "JOHNSON",
                Forename = "ALBERT",
                Sex = "M",
                Title = null,

                Modality = "CR",
                ExamDescription = "cp",
                ExamRoom = "CR2",
                HospitalName = null,
                PerformingPhysician = null,
                ProcedureID = "200005",
                ProcedureStepID = "200006",
                StudyUID = "1.2.34.567890.1234567890.3",
                ScheduledAET = "OEC9800",
                ReferringPhysician = "Peter^John^Md",
                ExamDateAndTime = DateTime.Now
            };

            return new List<WorklistItem> { item1, item2, item3 };
        }



        public List<WorklistItem> GetAllCurrentWorklistItemsFromDB()
        {

            string errorString = string.Empty;
            List<WorklistItem> objWorkListItems = new List<WorklistItem>();

            // Get Patient Worklist from Database
            ucls_DAL objDAL = new ucls_DAL(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
            // Get Worklist Items from the Database
            DataSet dsResult = objDAL.GetWorklistData(ref errorString);
            objDAL.Dispose();

            if (dsResult != null && dsResult.Tables[0].Rows.Count > 0 && errorString == string.Empty)
            {

                foreach (DataRow dRow in dsResult.Tables[0].Rows)
                {
                    WorklistItem mwlItem = new WorklistItem();
                    if (dRow["accession_no"] != null)
                        mwlItem.AccessionNumber = dRow["accession_no"].ToString();
                    if (dRow["pat_birthdate"] != null)
                        mwlItem.DateOfBirth = Convert.ToDateTime(dRow["pat_birthdate"]);


                    if (dRow["pat_id"] != null)
                        mwlItem.PatientID = dRow["pat_id"].ToString();

                    // Get Patient Name
                    if (dRow["pat_name"] != null)
                    {
                        if (dRow["pat_name"].ToString().Contains("^"))
                        {
                            string[] patNames = dRow["pat_name"].ToString().Split('^');
                            mwlItem.Surname = patNames[0];
                            mwlItem.Forename = patNames[1];
                        }
                        else
                        {
                            mwlItem.Surname = dRow["pat_name"].ToString();
                            mwlItem.Forename = string.Empty;
                        }
                    }

                    if (dRow["pat_sex"] != null)
                        mwlItem.Sex = dRow["pat_sex"].ToString();
                    /*if (dRow["pat_sex"] != null)
                        mwlItem.Title = dRow["pat_sex"].ToString();*/
                    if (dRow["modality"] != null)
                        mwlItem.Modality = dRow["modality"].ToString();
                    if (dRow["exam_desc"] != null)
                        mwlItem.ExamDescription = dRow["exam_desc"].ToString();
                    if (dRow["exam_room"] != null)
                        mwlItem.ExamDescription = dRow["exam_room"].ToString();
                    if (dRow["hospitalname"] != null)
                        mwlItem.HospitalName = dRow["hospitalname"].ToString();
                    if (dRow["perform_phys"] != null)
                        mwlItem.PerformingPhysician = dRow["perform_phys"].ToString();
                    if (dRow["procedureid"] != null)
                        mwlItem.ProcedureID = dRow["procedureid"].ToString();
                    if (dRow["procedurestepid"] != null)
                        mwlItem.ProcedureStepID = dRow["procedurestepid"].ToString();
                    if (dRow["study_iuid"] != null)
                        mwlItem.StudyUID = dRow["study_iuid"].ToString();
                    if (dRow["aetitle"] != null)
                        mwlItem.ScheduledAET = dRow["aetitle"].ToString();
                    if (dRow["ref_physician"] != null)
                        mwlItem.ReferringPhysician = dRow["ref_physician"].ToString();
                    if (dRow["examdate"] != null)
                        mwlItem.ExamDateAndTime = Convert.ToDateTime(dRow["examdate"]);

                    objWorkListItems.Add(mwlItem);
                }
            }
            return objWorkListItems;
        }

        public List<WorklistItem> GetAllCurrentWorklistItemsFromCareAsync()
        {
            List<WorklistItem> objWorkListItems = new List<WorklistItem>();
            ucls_ReadWriteLog objReadWriteLog = new ucls_ReadWriteLog();

            try
            {
                string errorString = string.Empty;

                Task<string> task = GetCareWorklistDetailsAsync();
                string responseBody = task.Result;

                CareWorklistResponse careResponse = JsonConvert.DeserializeObject<CareWorklistResponse>(responseBody);

                if (careResponse != null &&
                    careResponse.status != null &&
                    careResponse.status.Equals("success", StringComparison.OrdinalIgnoreCase) &&
                    careResponse.results != null &&
                    careResponse.results.Count > 0 &&
                    errorString == string.Empty)
                {
                    foreach (var item in careResponse.results)
                    {
                        WorklistItem mwlItem = new WorklistItem();
                        mwlItem.AccessionNumber = string.Empty;
                        string acc_servicerequestid = item.service_request != null ? item.service_request.external_id ?? string.Empty : string.Empty;

                        string[] parts = acc_servicerequestid.Split('-');

                        string result = parts[parts.Length - 2] + parts[parts.Length - 1];

                        mwlItem.AccessionNumber = result;// "5850ac6768c9407a95cbc7c5bb547d21"; 

                        if (item.patient != null)
                        {
                            mwlItem.PatientID = item.patient.external_id ?? item.patient.id ?? string.Empty;

                            if (!string.IsNullOrWhiteSpace(item.patient.name))
                            {
                                string[] patNames = item.patient.name.Trim().Split(' ');

                                if (patNames.Length > 1)
                                {
                                    mwlItem.Surname = patNames[0];
                                    mwlItem.Forename = string.Join(" ", patNames.Skip(1));
                                }
                                else
                                {
                                    mwlItem.Surname = item.patient.name;
                                    mwlItem.Forename = string.Empty;
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(item.patient.gender))
                                mwlItem.Sex = NormalizeSex(item.patient.gender);

                            if (item.patient.age.HasValue)
                                mwlItem.DateOfBirth = DateTime.Now.AddYears(item.patient.age.Value * -1);
                            else
                                mwlItem.DateOfBirth = DateTime.Now;
                        }
                        //mwlItem.PatientID = "10101";
                        //mwlItem.AccessionNumber = "26042022100448";
                        //mwlItem.Sex = "F";
                        mwlItem.Modality = "CR";
                        mwlItem.ExamDescription = item.service_request != null ? item.service_request.name ?? string.Empty : string.Empty;
                        mwlItem.HospitalName = item.facility != null ? item.facility.name ?? "CARE" : "CARE";
                        mwlItem.PerformingPhysician = string.Empty;
                        mwlItem.ProcedureID = "200001";// item.service_request != null ? item.service_request.id ?? string.Empty : string.Empty;
                        mwlItem.ProcedureStepID = "200002"; //item.service_request != null ? item.service_request.id ?? string.Empty : string.Empty;
                        mwlItem.StudyUID = "1.2.34.567890.1234567890.1";// string.Empty;
                        mwlItem.ScheduledAET = ConfigurationManager.AppSettings["careScheduledAET"]?.ToString() ?? "OEC9800";
                        mwlItem.ReferringPhysician = string.Empty;

                        if (item.service_request != null && item.service_request.date.HasValue)
                            mwlItem.ExamDateAndTime = item.service_request.date.Value.ToLocalTime();

                        objWorkListItems.Add(mwlItem);
                    }

                    objReadWriteLog.WriteToLog("CARE worklist data fetched and populated successfully.", true);
                }
                else
                {
                    if (errorString != string.Empty)
                    {
                        objReadWriteLog.WriteToLog("Error Getting CARE worklist Data with Exception : " + errorString, false);
                    }
                    else
                    {
                        objReadWriteLog.WriteToLog("No Record returned from CARE API", true);
                    }
                }
            }
            catch (Exception ex)
            {
                objReadWriteLog.WriteToLog("Error Getting / Populating CARE worklist data with exception " + ex.Message, false);
            }

            return objWorkListItems;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task<string> GetCareWorklistDetailsAsync()
        {
            string responseBody = string.Empty;
            ucls_ReadWriteLog objReadWriteLog = new ucls_ReadWriteLog();

            try
            {
                string baseUrl = ConfigurationManager.AppSettings["careBaseUrl"].ToString();
                string token = ConfigurationManager.AppSettings["careToken"].ToString();
                string modality = ConfigurationManager.AppSettings["careModality"].ToString();
                string fromDate = ConfigurationManager.AppSettings["careFromDate"].ToString();
                string toDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                string requestUrl = baseUrl +
                                    "/api/care_radiology/dicom/worklist/?modality=" + Uri.EscapeDataString(modality) +
                                    "&from=" + Uri.EscapeDataString(fromDate) +
                                    "&to=" + Uri.EscapeDataString(toDate);

                objReadWriteLog.WriteToLog("CARE Worklist URL: " + requestUrl, true);

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("Authorization", token);

                    HttpResponseMessage response = await client.GetAsync(requestUrl);
                    response.EnsureSuccessStatusCode();
                    responseBody = await response.Content.ReadAsStringAsync();
                }

                objReadWriteLog.WriteToLog("CARE Worklist API call successful. Returning the value", true);
            }
            catch (Exception ex)
            {
                objReadWriteLog.WriteToLog("Error calling CARE Worklist API with exception " + ex.Message, false);
                throw;
            }

            return responseBody;
        }
    

    public List<WorklistItem> GetAllCurrentWorklistItemsFromPellucidAsync()
        {
            List<WorklistItem> objWorkListItems = new List<WorklistItem>();
            ucls_ReadWriteLog objReadWriteLog = new ucls_ReadWriteLog();
            try
            {
                string errorString = string.Empty;


                
                Task<string> task = authAndGetDetailsAsync();
                string patientInfoResponseBody = task.Result;

                JArray patientInfoArray = JArray.Parse(patientInfoResponseBody);

                objReadWriteLog.WriteToLog("Retrieval for data Succcessfull ", true);

                if (patientInfoArray != null && patientInfoArray.Count > 0 && errorString == string.Empty)
                {


                    List<List<Appointment>> appointmentsList = JsonConvert.DeserializeObject<List<List<Appointment>>>(patientInfoArray.ToString());

                    foreach (var appointments in appointmentsList)
                    {

                        foreach (var appointment in appointments)
                        {
                            WorklistItem mwlItem = new WorklistItem();
                            mwlItem.AccessionNumber = string.Empty; 
                            
                            // new Random().Next().ToString();

                            if (appointment.Patient.Age.Year != null && appointment.Patient.Age.Year != string.Empty)
                            {
                                int age = Convert.ToInt32(appointment.Patient.Age.Year);
                                mwlItem.DateOfBirth = DateTime.Now.AddYears(age * -1);
                            }
                            else
                            {
                                mwlItem.DateOfBirth = DateTime.Now;
                            }

                            if (appointment.Patient.PatientMrn != null)
                            {
                                mwlItem.PatientID = appointment.Patient.PatientMrn;
                            }

                            if (appointment.Patient.FullName.FirstName != null)
                                mwlItem.Surname = appointment.Patient.FullName.FirstName;

                            if (appointment.Patient.FullName.LastName != null)
                                mwlItem.Forename = appointment.Patient.FullName.LastName;

                            if (appointment.Patient.Gender != null)
                                mwlItem.Sex = NormalizeSex(appointment.Patient.Gender);


                            if (appointment.Patient.Gender != null)
                                mwlItem.Sex = NormalizeSex(appointment.Patient.Gender);

                            mwlItem.Modality = "OT";
                            mwlItem.ExamDescription = string.Empty;
                            mwlItem.HospitalName = "SNC";
                            mwlItem.PerformingPhysician = string.Empty;
                            mwlItem.ProcedureID = "200003";
                            mwlItem.ProcedureStepID = "200004";
                            mwlItem.StudyUID = string.Empty;
                            mwlItem.ScheduledAET = "OEC9800";
                            mwlItem.ReferringPhysician = string.Empty;
                            if (appointment.AppointmentDate != null && appointment.AppointmentDate != string.Empty)
                                mwlItem.ExamDateAndTime = Convert.ToDateTime(appointment.AppointmentDate);

                            objWorkListItems.Add(mwlItem);
                        }

                    }
                    objReadWriteLog.WriteToLog("Data Fetched from Database and populated to Dataset : ", true);

                }
                else
                {
                    if (errorString != string.Empty)
                    {
                        objReadWriteLog.WriteToLog("Error Getting worklist Data with Exception : " + errorString, false);
                    }
                    else
                    {
                        objReadWriteLog.WriteToLog("No Record returned from Database ", true);
                    }
                }
            }
            catch (Exception ex)
            {
                objReadWriteLog.WriteToLog("Error Getting /Populating data from Database with excception " + ex.Message, false);
            }
            return objWorkListItems;
        }




        private static string NormalizeSex(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "O";
            switch (value.ToLowerInvariant().Trim())
            {
                case "m": case "male":   return "M";
                case "f": case "female": return "F";
                default:                 return "O";
            }
        }

        private async Task<string> authAndGetDetailsAsync()
        {
            string patientInfoResponseBody = string.Empty;
            ucls_ReadWriteLog objReadWriteLog = new ucls_ReadWriteLog();

            string authUrl = ConfigurationManager.AppSettings["authURL"].ToString();
            string patienURL = ConfigurationManager.AppSettings["fetchPat"].ToString();
            string room = ConfigurationManager.AppSettings["room"].ToString();
            string fromDate = ConfigurationManager.AppSettings["fromDate"].ToString();

            objReadWriteLog.WriteToLog("Get Default values from Backend ", true);

            DateTime now = DateTime.Now.AddDays(2);
            string toDate = now.ToString("yyyy-MM-dd");

            var authContent = new StringContent(
                JsonConvert.SerializeObject(new { id = "snc.evaluator.a", password = "password" }),
                Encoding.UTF8,
                "application/json"
            );

            using (HttpClient client = new HttpClient())
            {
                {
                    // Authenticate
                    objReadWriteLog.WriteToLog("Authenticate with Pellucid Server A", true);
                    HttpResponseMessage authResponse = await client.PostAsync(authUrl, authContent);


                    authResponse.EnsureSuccessStatusCode();
                    string authResponseBody = await authResponse.Content.ReadAsStringAsync();
                    //JObject authJson = JObject.Parse(authResponseBody);
                    string authToken = authResponseBody; // Assuming the key is returned in a field called "key"
                    objReadWriteLog.WriteToLog("Authentication Succesfull", true);
                    // Fetch patient info
                    string patientInfoUrl = patienURL + "?MRN=&client_id=&appointmentfromdate=" + fromDate + "&appointmenttodate=" + toDate + "&currentdepartment=" + room + "&email";
                    objReadWriteLog.WriteToLog(patientInfoUrl, true);
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
                    HttpResponseMessage patientInfoResponse = await client.GetAsync(patientInfoUrl);
                    patientInfoResponse.EnsureSuccessStatusCode();
                    patientInfoResponseBody = await patientInfoResponse.Content.ReadAsStringAsync();
                }

            }

            objReadWriteLog.WriteToLog("Patient URL Call successfull. Returning the value", true);
            return patientInfoResponseBody;
        }
    }

    public class CareWorklistResponse
    {
        public string status { get; set; }
        public List<CareWorklistResult> results { get; set; }
    }

    public class CareWorklistResult
    {
        public CareServiceRequest service_request { get; set; }
        public CareFacility facility { get; set; }
        public CarePatient patient { get; set; }
    }

    public class CareServiceRequest
    {
        public string external_id { get; set; }
        public string id { get; set; }
        public string name { get; set; }
        public DateTime? date { get; set; }
    }

    public class CareFacility
    {
        
        public string id { get; set; }
        public string name { get; set; }
    }

    public class CarePatient
    {
        public string external_id { get; set; }
        public string id { get; set; }
        public string name { get; set; }
        public string address { get; set; }
        public string phone_number { get; set; }
        public string gender { get; set; }
        public int? age { get; set; }
    }
}
