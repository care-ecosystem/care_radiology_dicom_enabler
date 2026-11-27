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
using Serilog.Events;

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
                ScheduledAET = string.Empty,
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
                ScheduledAET = string.Empty,
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
                ScheduledAET = string.Empty,
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
        /// facility, limited to the given modality (the one the device queried with); with none given,
        /// every modality is returned (C-FIND does not call this without one, the periodic refresh
        /// does to cache every scheduled item). Reads only the local table - the CARE API is called by
        /// RefreshCareWorklistFromApi.
        /// </summary>
        public List<WorklistItem> GetCareWorklistItemsFromDB(string facilityId, string modality = null)
        {
            List<WorklistItem> objWorkListItems = new List<WorklistItem>();
            ucls_ReadWriteLog objReadWriteLog = new ucls_ReadWriteLog();
            ucls_DAL objDal = null;
            try
            {
                string errorString = string.Empty;
                objDal = new ucls_DAL(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                modality = modality?.Trim() ?? string.Empty;
                List<CareWorklistRecord> records = objDal.GetScheduledCareWorklist(facilityId, modality, ref errorString);
                if (errorString != string.Empty)
                {
                    objReadWriteLog.WriteThrottled(LogEventLevel.Error, "Reading the worklist from care_worklist failed: " + errorString);
                    return objWorkListItems;
                }

                Dictionary<string, string> studyUidRoots = GetStudyUidRootsByAET(objDal, objReadWriteLog);

                foreach (CareWorklistRecord record in records)
                {
                    WorklistItem mwlItem = ToWorklistItem(record, modality);
                    mwlItem.StudyUIDsByAET = BuildStudyUidsByAET(studyUidRoots, record);
                    objWorkListItems.Add(mwlItem);
                    // An AE title is at most 16 characters: a longer one is never sent as Scheduled Station AE Title
                    var tooLong = (record.DeviceAeTitles ?? new List<string>()).Select(aet => aet.Trim()).Where(aet => aet.Length > 16).Distinct().ToList();
                    if (tooLong.Count > 0)
                        objReadWriteLog.WriteThrottled(LogEventLevel.Warning, $"care_worklist: AccessionNumber={record.AccessionNumber} (service request {record.ServiceRequestId}): device AE title(s) {string.Join(", ", tooLong.Select(aet => $"\"{aet}\" ({aet.Length} characters)"))} " +
                            "not sent as Scheduled Station AE Title (0040,0001), which allows 16 characters. Shorten the ae_title of the DICOM device in CARE.");
                }

                // Without a device AE title the item matches no C-FIND that asks for a Scheduled Station AE Title
                var withoutAeTitle = objWorkListItems.Where(item => item.ScheduledAETs.Count == 0).Select(item => item.AccessionNumber).ToList();
                if (withoutAeTitle.Count > 0)
                    objReadWriteLog.WriteThrottled(LogEventLevel.Warning, $"care_worklist: {withoutAeTitle.Count} item(s) are scheduled on no DICOM device with an AE title in CARE, so they have no Scheduled Station AE Title: {string.Join(", ", withoutAeTitle)}. Set the ae_title of the DICOM devices in CARE.");

                objReadWriteLog.Debug($"care_worklist: {objWorkListItems.Count} scheduled item(s) for Facility ID {facilityId}, Modality {(string.IsNullOrWhiteSpace(modality) ? "(all)" : modality)}");
            }
            catch (Exception ex)
            {
                objReadWriteLog.WriteThrottled(LogEventLevel.Error, "Reading the worklist from care_worklist failed with exception " + ex.Message);
            }
            finally
            {
                objDal?.Dispose();
            }
            return objWorkListItems;
        }

        /// <summary>
        /// Calls the CARE worklist API for a facility and saves the response to care_worklist with
        /// SyncCareWorklist. Every modality is fetched: the modality a device queries with is matched
        /// when it sends C-FIND (GetCareWorklistItemsFromDB). Returns false
        /// when no Facility ID is given, the call fails, CARE does not report success, or saving
        /// fails; failure is then the reason, for the caller to log (in its C-FIND summary line, or once
        /// for the periodic refresh). The CARE API's own lines are Debug detail, except an order CARE has
        /// that cannot be added to the worklist, which is a warning (at most once an hour).
        /// </summary>
        public bool RefreshCareWorklistFromApi(string facilityId, out string failure)
        {
            ucls_ReadWriteLog objReadWriteLog = new ucls_ReadWriteLog();
            failure = null;

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                failure = "no facility is selected in the Configuration tab";
                return false;
            }

            string lastError = null;
            Action<string, bool> writeToLog = (message, isInfo) =>
            {
                if (isInfo)
                {
                    objReadWriteLog.WriteToLog("[REFRESH] " + message, true);
                }
                else
                {
                    lastError = message;
                    objReadWriteLog.Debug("[REFRESH] " + message);
                }
            };

            // The DICOM Enabler selected in the Configuration tab: CARE returns only its worklist (the
            // requests scheduled in the rooms of its DICOM devices). Without one nothing is fetched,
            // unless worklist_all_devices is on: then the whole facility's worklist is fetched.
            string dicomEnablerId = WorklistServer.GetConfigSetting("dicom_enabler_id", null).Trim();
            bool allDevices = ucls_CareWorklist.IsAllDevices(WorklistServer.GetConfigSetting(ucls_CareWorklist.AllDevicesConfigKey, null));

            ucls_DAL objDal = null;
            try
            {
                objDal = new ucls_DAL(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                if (!ucls_ServiceToken.TryGetAuthorizationHeader(objDal, out string authorization, out string tokenError))
                {
                    failure = tokenError;
                    return false;
                }
                bool ok = ucls_CareWorklist.RefreshCareWorklist(
                    objDal,
                    ConfigurationManager.AppSettings["careBaseUrl"] ?? string.Empty,
                    authorization,
                    WorklistServer.GetConfigSetting("care_from_date", "careFromDate"),
                    facilityId,
                    dicomEnablerId,
                    allDevices,
                    writeToLog);
                if (!ok)
                    failure = lastError ?? "refreshing care_worklist from the CARE worklist API failed";
                else if (lastError != null)
                    objReadWriteLog.WriteThrottled(LogEventLevel.Error, "[REFRESH] " + lastError);
                return ok;
            }
            catch (Exception ex)
            {
                failure = "refreshing care_worklist from the CARE worklist API failed with exception " + ex.Message;
                return false;
            }
            finally
            {
                objDal?.Dispose();
            }
        }


        /// <summary>
        /// Maps one care_worklist row (with its patient and service request) to the worklist item
        /// returned in C-FIND responses. requestedModality is the modality the device queried with:
        /// the row was read because it is scheduled on a DICOM device of that modality, or is linked to no
        /// device (no modality from CARE, offered for any modality), so the item carries it. Without one,
        /// the request's own modality is used.
        /// </summary>
        private static WorklistItem ToWorklistItem(CareWorklistRecord record, string requestedModality)
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

            // The modality of the DICOM device it is scheduled on. Modality (0008,0060) is a CS value,
            // which allows only uppercase letters, digits, space and underscore.
            mwlItem.Modality = (string.IsNullOrWhiteSpace(requestedModality) ? record.ServiceRequestModality ?? string.Empty : requestedModality)
                               .Trim().ToUpperInvariant();
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
            // No Study Instance UID of its own: a device with a Study UID root in CARE gets the one built under
            // it (StudyUIDsByAET); a device without one generates its own.
            mwlItem.StudyUID = string.Empty;
            // Scheduled Station AE Title (0040,0001): the ae_title of each DICOM device the request is
            // scheduled on, from CARE - there is no fixed AE title for the DICOM Enabler. An AE title is
            // at most 16 characters, so a longer one is left out. With none the item has no AE title.
            mwlItem.ScheduledAETs = (record.DeviceAeTitles ?? new List<string>())
                .Select(aet => aet.Trim())
                .Where(aet => aet.Length > 0 && aet.Length <= 16)
                .Distinct()
                .ToList();
            mwlItem.ScheduledAET = mwlItem.ScheduledAETs.FirstOrDefault() ?? string.Empty;
            mwlItem.HasScheduledDevices = record.HasDevices;
            // Who raised the service request in CARE
            mwlItem.ReferringPhysician = record.ServiceRequestReferringPhysician ?? string.Empty;
            mwlItem.TechnicianInstruction = record.ServiceRequestTechnicianInstruction ?? string.Empty;
            mwlItem.PatientInstruction = record.ServiceRequestPatientInstruction ?? string.Empty;
            mwlItem.Priority = NormalizePriority(record.ServiceRequestPriority);
            mwlItem.ProcedureCode = record.ServiceRequestProcedureId ?? string.Empty;
            mwlItem.BodySite = record.ServiceRequestBodySite ?? string.Empty;
            mwlItem.BodySiteId = record.ServiceRequestBodySiteId ?? string.Empty;

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

                objReadWriteLog.Debug("Retrieval for data Succcessfull ");

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
                            mwlItem.ScheduledAET = string.Empty;
                            mwlItem.ReferringPhysician = string.Empty;
                            if (appointment.AppointmentDate != null && appointment.AppointmentDate != string.Empty)
                                mwlItem.ExamDateAndTime = Convert.ToDateTime(appointment.AppointmentDate);

                            objWorkListItems.Add(mwlItem);
                        }

                    }
                    objReadWriteLog.Debug("Data Fetched from Database and populated to Dataset : ");

                }
                else
                {
                    if (errorString != string.Empty)
                    {
                        objReadWriteLog.WriteToLog("Error Getting worklist Data with Exception : " + errorString, false);
                    }
                    else
                    {
                        objReadWriteLog.Debug("No Record returned from Database ");
                    }
                }
            }
            catch (Exception ex)
            {
                objReadWriteLog.WriteToLog("Error Getting /Populating data from Database with excception " + ex.Message, false);
            }
            return objWorkListItems;
        }




        /// <summary>
        /// Caps a logged response body so an HTML error page cannot flood the log file.
        /// </summary>
        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "(empty)";
            value = value.Trim();
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "... (truncated)";
        }

        /// <summary>
        /// Describes the configured token without writing it to the log: its length plus a short
        /// SHA-256 fingerprint, which is enough to compare against the server's own secret.
        /// </summary>
        private static string DescribeToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return "(not configured)";

            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
                string hex = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
                return $"{token.Length} chars, sha256:{hex.Substring(0, 16)}";
            }
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
        /// Maps CARE's service_request.priority (stat / urgent / asap / routine) to a Requested Procedure
        /// Priority (0040,1003) defined term: STAT -> STAT, urgent -> MEDIUM, asap -> HIGH, routine -> ROUTINE.
        /// A missing or unknown priority is sent as ROUTINE.
        /// </summary>
        private static string NormalizePriority(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "ROUTINE";
            switch (value.ToLowerInvariant().Trim())
            {
                case "stat":    return "STAT";
                case "urgent":  return "MEDIUM";
                case "asap":    return "HIGH";
                case "routine": return "ROUTINE";
                default:        return "ROUTINE";
            }
        }

        /// <summary>
        /// Builds a fallback accession number from the last two groups of the service request UUID.
        /// Returns empty rather than throwing when the ID is missing or not hyphenated.
        /// </summary>
        private static string DeriveAccessionFromServiceRequestId(string serviceRequestId)
        {
            if (string.IsNullOrWhiteSpace(serviceRequestId))
            {
                return string.Empty;
            }

            string[] parts = serviceRequestId.Split('-');
            return parts.Length >= 2 ? parts[parts.Length - 2] + parts[parts.Length - 1] : serviceRequestId;
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

        /// <summary>
        /// A UID root as CARE validates study_uid_root: dot-separated numbers without leading zeros, at most
        /// 40 characters, which leaves at least 23 digits of the 64-character UID for the suffix.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex StudyUidRootPattern =
            new System.Text.RegularExpressions.Regex(@"^(0|[1-9][0-9]*)(\.(0|[1-9][0-9]*))*$");

        private const int MaxStudyUidRootLength = 40;

        /// <summary>A UI value (and so a Study Instance UID) is at most 64 characters.</summary>
        private const int MaxUidLength = 64;

        /// <summary>
        /// The Study UID root of each DICOM device of the selected DICOM Enabler (care_devices, saved by Sync),
        /// by AE title. Devices with no root are left out, as is one whose root is not a valid UID root (logged):
        /// no Study Instance UID is sent to them, so they generate their own.
        /// </summary>
        private static Dictionary<string, string> GetStudyUidRootsByAET(ucls_DAL objDal, ucls_ReadWriteLog objReadWriteLog)
        {
            var roots = new Dictionary<string, string>(StringComparer.Ordinal);
            string dicomEnablerId = WorklistServer.GetConfigSetting("dicom_enabler_id", null)?.Trim() ?? string.Empty;
            if (dicomEnablerId == string.Empty)
                return roots;

            string errorString = string.Empty;
            List<CareDeviceRecord> devices = objDal.GetCareDevices(dicomEnablerId, ref errorString);
            if (errorString != string.Empty)
            {
                objReadWriteLog.WriteThrottled(LogEventLevel.Error, "Study UID roots not read from care_devices, so no Study Instance UID is sent and the devices generate their own: " + errorString);
                return roots;
            }

            foreach (CareDeviceRecord device in devices.Where(d => d.DeviceType == ucls_CareModalities.CareTypeDicom && !string.IsNullOrWhiteSpace(d.AeTitle)))
            {
                string aet = device.AeTitle.Trim();
                string root = device.StudyUidRoot?.Trim() ?? string.Empty;
                // No root in CARE: the device generates its own Study Instance UID
                if (root.Length == 0)
                    continue;
                if (root.Length > MaxStudyUidRootLength || !StudyUidRootPattern.IsMatch(root))
                {
                    objReadWriteLog.WriteThrottled(LogEventLevel.Warning, $"care_devices: Study UID root \"{root}\" of device {device.DeviceName} (AE {aet}) is not a UID root of at most {MaxStudyUidRootLength} characters " +
                        "(numbers without leading zeros, separated by dots), so no Study Instance UID is sent and the device generates its own. Correct its study_uid_root in CARE and Sync.");
                    continue;
                }
                if (roots.TryGetValue(aet, out string other) && other != root)
                {
                    objReadWriteLog.WriteThrottled(LogEventLevel.Warning, $"care_devices: more than one device has AE title {aet} with different Study UID roots; {other} is used.");
                    continue;
                }
                roots[aet] = root;
            }
            return roots;
        }

        /// <summary>
        /// The Study Instance UID of a worklist row for each device AE title that has a Study UID root. Empty
        /// when the row has no accession number or creation time.
        /// </summary>
        private static Dictionary<string, string> BuildStudyUidsByAET(Dictionary<string, string> studyUidRoots, CareWorklistRecord record)
        {
            var uids = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(record.AccessionNumber) || !record.WorklistCreatedTime.HasValue)
                return uids;
            foreach (var entry in studyUidRoots)
                uids[entry.Key] = BuildStudyUidUnderRoot(entry.Value, record.FacilityId, record.AccessionNumber, record.WorklistCreatedTime.Value);
            return uids;
        }

        /// <summary>
        /// A Study Instance UID under a device's root: &lt;root&gt;.&lt;suffix&gt;, where the suffix is the decimal
        /// SHA-256 of facility ID, accession number and timestamp, cut to fill the UID to 64 characters. The
        /// timestamp is when the care_worklist row was first saved, so every C-FIND gets the same UID for the
        /// order, while an accession number reused later gets a new one.
        /// </summary>
        internal static string BuildStudyUidUnderRoot(string root, string facilityId, string accessionNumber, DateTime timestamp)
        {
            byte[] hash;
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                string seed = $"{(facilityId ?? string.Empty).Trim()}|{accessionNumber.Trim()}|{timestamp.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture)}";
                hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(seed));
            }

            // BigInteger reads little-endian two's complement: reverse, and add a zero byte to keep it positive.
            byte[] littleEndian = new byte[hash.Length + 1];
            for (int i = 0; i < hash.Length; i++)
                littleEndian[i] = hash[hash.Length - 1 - i];

            // A positive number's decimal form never starts with 0, as a UID component must not (unless it is 0)
            string digits = new System.Numerics.BigInteger(littleEndian).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (digits == "0")
                digits = "1";
            int suffixLength = Math.Min(digits.Length, MaxUidLength - root.Length - 1);
            return root + "." + digits.Substring(0, suffixLength);
        }

        private async Task<string> authAndGetDetailsAsync()
        {
            string patientInfoResponseBody = string.Empty;
            ucls_ReadWriteLog objReadWriteLog = new ucls_ReadWriteLog();

            string authUrl = ConfigurationManager.AppSettings["authURL"].ToString();
            string patienURL = ConfigurationManager.AppSettings["fetchPat"].ToString();
            string room = ConfigurationManager.AppSettings["room"].ToString();
            string fromDate = ConfigurationManager.AppSettings["fromDate"].ToString();

            objReadWriteLog.Debug("Get Default values from Backend ");

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
                    objReadWriteLog.Debug("Authenticate with Pellucid Server A");
                    HttpResponseMessage authResponse = await client.PostAsync(authUrl, authContent);


                    authResponse.EnsureSuccessStatusCode();
                    string authResponseBody = await authResponse.Content.ReadAsStringAsync();
                    //JObject authJson = JObject.Parse(authResponseBody);
                    string authToken = authResponseBody; // Assuming the key is returned in a field called "key"
                    objReadWriteLog.Debug("Authentication Succesfull");
                    // Fetch patient info
                    string patientInfoUrl = patienURL + "?MRN=&client_id=&appointmentfromdate=" + fromDate + "&appointmenttodate=" + toDate + "&currentdepartment=" + room + "&email";
                    objReadWriteLog.Debug(patientInfoUrl);
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
                    HttpResponseMessage patientInfoResponse = await client.GetAsync(patientInfoUrl);
                    patientInfoResponse.EnsureSuccessStatusCode();
                    patientInfoResponseBody = await patientInfoResponse.Content.ReadAsStringAsync();
                }

            }

            objReadWriteLog.Debug("Patient URL Call successfull. Returning the value");
            return patientInfoResponseBody;
        }
    }
}
