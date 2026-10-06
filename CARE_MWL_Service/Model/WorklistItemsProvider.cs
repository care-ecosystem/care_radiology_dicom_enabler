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

        /// <summary>
        /// Builds the worklist shared with modalities from the SCHEDULED care_worklist rows for a
        /// facility, limited to the modality set in the Configuration tab (care_modality, or App.config
        /// careModality when blank; blank in both returns every modality). Both are read on every call,
        /// so a change in the Configuration tab applies to the next C-FIND. Reads only the local
        /// table - the CARE API is called by RefreshCareWorklistFromApi.
        /// </summary>
        public List<WorklistItem> GetCareWorklistItemsFromDB(string facilityId)
        {
            List<WorklistItem> objWorkListItems = new List<WorklistItem>();
            ucls_ReadWriteLog objReadWriteLog = new ucls_ReadWriteLog();
            ucls_DAL objDal = null;
            try
            {
                string errorString = string.Empty;
                objDal = new ucls_DAL(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                string modality = WorklistServer.GetConfigSetting("care_modality", "careModality");
                List<CareWorklistRecord> records = objDal.GetScheduledCareWorklist(facilityId, modality, ref errorString);
                if (errorString != string.Empty)
                {
                    objReadWriteLog.WriteToLog(errorString, false);
                    return objWorkListItems;
                }

                foreach (CareWorklistRecord record in records)
                    objWorkListItems.Add(ToWorklistItem(record));

                objReadWriteLog.WriteToLog($"care_worklist: {objWorkListItems.Count} scheduled item(s) for Facility ID {facilityId}, Modality {(string.IsNullOrWhiteSpace(modality) ? "(all)" : modality)}" +
                    (objWorkListItems.Count > 0 ? $" (Accession Numbers: {string.Join(", ", objWorkListItems.Select(x => x.AccessionNumber))})" : string.Empty), true);
            }
            catch (Exception ex)
            {
                objReadWriteLog.WriteToLog("Reading the worklist from care_worklist failed with exception " + ex.Message, false);
            }
            finally
            {
                objDal?.Dispose();
            }
            return objWorkListItems;
        }

        /// <summary>
        /// Calls the CARE worklist API for a facility and saves the response to care_worklist with
        /// SyncCareWorklist. Returns false when no Facility ID is given, the call fails, CARE does not
        /// report success, or saving fails; the reason is logged.
        /// </summary>
        public bool RefreshCareWorklistFromApi(string facilityId)
        {
            ucls_ReadWriteLog objReadWriteLog = new ucls_ReadWriteLog();

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                objReadWriteLog.WriteToLog("Not calling the CARE worklist API: no Facility ID resolved. Enter a Facility ID in the Configuration tab.", false);
                return false;
            }

            ucls_DAL objDal = null;
            try
            {
                objDal = new ucls_DAL(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                return ucls_CareWorklist.RefreshCareWorklist(
                    objDal,
                    ConfigurationManager.AppSettings["careBaseUrl"] ?? string.Empty,
                    ConfigurationManager.AppSettings["careToken"] ?? string.Empty,
                    WorklistServer.GetConfigSetting("care_modality", "careModality"),
                    WorklistServer.GetConfigSetting("care_from_date", "careFromDate"),
                    facilityId,
                    objReadWriteLog.WriteToLog);
            }
            catch (Exception ex)
            {
                objReadWriteLog.WriteToLog("Refreshing care_worklist from the CARE worklist API failed with exception " + ex.Message, false);
                return false;
            }
            finally
            {
                objDal?.Dispose();
            }
        }


        /// <summary>
        /// Maps one care_worklist row (with its patient and service request) to the worklist item
        /// returned in C-FIND responses.
        /// </summary>
        private static WorklistItem ToWorklistItem(CareWorklistRecord record)
        {
            WorklistItem mwlItem = new WorklistItem();
            mwlItem.AccessionNumber = record.AccessionNumber ?? string.Empty;

            mwlItem.PatientUHID = record.PatientUhid ?? string.Empty;
            mwlItem.PatientID = !string.IsNullOrWhiteSpace(record.PatientUhid)
                ? record.PatientUhid
                : (record.PatientId ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(record.PatientName))
            {
                string[] patNames = record.PatientName.Trim().Split(' ');

                if (patNames.Length > 1)
                {
                    mwlItem.Surname = patNames[0];
                    mwlItem.Forename = string.Join(" ", patNames.Skip(1));
                }
                else
                {
                    mwlItem.Surname = record.PatientName;
                    mwlItem.Forename = string.Empty;
                }
            }

            if (!string.IsNullOrWhiteSpace(record.PatientGender))
                mwlItem.Sex = NormalizeSex(record.PatientGender);

            if (record.PatientAge.HasValue)
                mwlItem.DateOfBirth = DateTime.Now.AddYears(record.PatientAge.Value * -1);
            else
                mwlItem.DateOfBirth = DateTime.Now;

            mwlItem.Modality = record.ServiceRequestModality ?? "CR";
            mwlItem.ExamDescription = record.ServiceRequestName ?? string.Empty;
            mwlItem.HospitalName = record.FacilityName ?? "CARE";
            mwlItem.FacilityId = record.FacilityId ?? string.Empty;
            mwlItem.PerformingPhysician = string.Empty;
            mwlItem.ServiceRequestId = record.ServiceRequestId ?? string.Empty;
            // Must be unique per item - MPPS N-CREATE correlation (MppsHandler.SetInProgress) matches
            // worklist items by this value, so every item sharing "200002" caused MPPS to always
            // resolve to the first CurrentWorklistItems entry regardless of which procedure was performed.
            mwlItem.ProcedureStepID = mwlItem.AccessionNumber;
            mwlItem.ProcedureID = DeriveProcedureIdFromAccessionNumber(mwlItem.AccessionNumber);
            mwlItem.StudyUID = string.Empty;
            mwlItem.ScheduledAET = ConfigurationManager.AppSettings["careScheduledAET"]?.ToString() ?? "OEC9800";
            mwlItem.ReferringPhysician = FormatReferringPhysician(record.CreatedByPrefix, record.CreatedByFirstName, record.CreatedByLastName);
            mwlItem.TechnicianInstruction = record.ServiceRequestTechnicianInstruction ?? string.Empty;
            mwlItem.PatientInstruction = record.ServiceRequestPatientInstruction ?? string.Empty;
            mwlItem.Priority = NormalizePriority(record.ServiceRequestPriority);
            mwlItem.ProcedureCode = record.ServiceRequestProcedureId ?? string.Empty;

            // care_service_request.date is saved already converted to local time.
            if (record.ServiceRequestDate.HasValue)
                mwlItem.ExamDateAndTime = record.ServiceRequestDate.Value;

            return mwlItem;
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

        /// <summary>
        /// Maps CARE's free-text service_request.priority to a DICOM CS-compliant Priority value
        /// (STAT/HIGH/MEDIUM/ROUTINE), mirroring the gender normalization done for PatientSex - CARE
        /// returns lower-case/varied wording, but the Priority attribute is a constrained CS value set.
        /// </summary>
        private static string NormalizePriority(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "ROUTINE";
            switch (value.ToLowerInvariant().Trim())
            {
                case "stat": case "emergency": case "urgent": return "STAT";
                case "high": case "asap":                     return "HIGH";
                case "medium":                                return "MEDIUM";
                case "routine": case "normal":                return "ROUTINE";
                default:                                      return "ROUTINE";
            }
        }

        /// <summary>
        /// Builds a DICOM PN-formatted (FamilyName^GivenName^MiddleName^Prefix^Suffix) referring physician
        /// name from the CARE service_request created_by values saved in care_service_request.
        /// </summary>
        private static string FormatReferringPhysician(string prefix, string firstName, string lastName)
        {
            if (prefix == null && firstName == null && lastName == null)
            {
                return string.Empty;
            }

            string familyName = lastName ?? string.Empty;
            string givenName = firstName ?? string.Empty;
            prefix = prefix ?? string.Empty;

            return $"{familyName}^{givenName}^^{prefix}".TrimEnd('^');
        }

        /// <summary>
        /// Derives a stable numeric Requested Procedure ID (DICOM SH, max 16 chars) from an accession
        /// number by hashing its character codes, so alphanumeric accession numbers like "ACJAY260005"
        /// still map to a unique, reproducible RequestedProcedureID.
        /// </summary>
        private static string DeriveProcedureIdFromAccessionNumber(string accessionNumber)
        {
            if (string.IsNullOrWhiteSpace(accessionNumber))
            {
                return string.Empty;
            }

            unchecked
            {
                long hash = 17;
                foreach (char c in accessionNumber)
                {
                    hash = hash * 31 + c;
                }

                long numeric = Math.Abs(hash % 1_000_000_000_000_000L);
                return numeric.ToString();
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
}
