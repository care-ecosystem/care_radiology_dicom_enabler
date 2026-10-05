// Copyright (c) 2012-2022 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FellowOakDicom.Log;
using Newtonsoft.Json;

namespace Worklist_SCP.Model
{

    /// <summary>
    /// An implementation of IMppsSource, that does only logging but does not store the MPPS messages
    /// </summary>
    class MppsHandler : IMppsSource
    {

        public static Dictionary<string, WorklistItem> PendingProcedures { get; } = new Dictionary<string, WorklistItem>();

        private readonly ILogger _logger;

        // fo-dicom's logger only reaches the console, which a Windows service does not have, so every
        // MPPS line is also written to logs/ModalitySCP.txt where it can be read back.
        private readonly Serilog.ILogger _fileLogger;


        public MppsHandler(ILogger logger, Serilog.ILogger fileLogger = null)
        {
            _logger = logger;
            _fileLogger = fileLogger;
        }


        /// <summary>
        /// study_status values sent to the CARE status webhook. CARE adds the facility tag config whose
        /// display equals the value exactly, so they are configurable to match how a facility named
        /// its tags. Defaults are the values the enabler has always sent.
        /// </summary>
        private static string StatusStarted => ReadStatus("mppsStatusStarted", "Scan Started");
        private static string StatusCompleted => ReadStatus("mppsStatusCompleted", "Scan Completed");
        private static string StatusDiscontinued => ReadStatus("mppsStatusDiscontinued", "Scan Cancelled");

        private static string ReadStatus(string key, string defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        }

        private void LogInfo(string message)
        {
            _logger.Info(message);
            _fileLogger?.Information(message);
        }

        private void LogWarn(string message)
        {
            _logger.Warn(message);
            _fileLogger?.Warning(message);
        }

        private void LogError(string message)
        {
            _logger.Error(message);
            _fileLogger?.Error(message);
        }


        /// <summary>
        /// Sends MPPS status update to CARE server webhook
        /// </summary>
        private async Task SendStatusToCareServerAsync(string serviceRequestId, string facilityId, string studyStatus)
        {
            try
            {
                // Only send webhook if backend is set to CARE Server (mode 2)
                int backend = Convert.ToInt32(ConfigurationManager.AppSettings["backend"] ?? "2");
                if (backend != 2)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(serviceRequestId))
                {
                    LogWarn($"[MPPS] MPPS webhook skipped: {studyStatus} - worklist item has no service_request id");
                    return;
                }

                string baseUrl = ConfigurationManager.AppSettings["careBaseUrl"]?.ToString();
                string token = ConfigurationManager.AppSettings["careToken"]?.ToString();

                if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
                {
                    LogWarn("[MPPS] MPPS webhook skipped: CARE server URL or token not configured");
                    return;
                }

                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    LogWarn($"[MPPS] facility_id missing for service_request {serviceRequestId} - sending webhook without it");
                }

                string webhookUrl = $"{baseUrl}/api/care_radiology/webhooks/status/";

                var payload = new
                {
                    service_request_id = serviceRequestId,
                    facility_id = facilityId,
                    study_status = studyStatus
                };

                string jsonPayload = JsonConvert.SerializeObject(payload);

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("Authorization", token);

                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await client.PostAsync(webhookUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        LogInfo($"[MPPS] MPPS webhook sent to CARE: {studyStatus} for service_request {serviceRequestId} facility {facilityId}");
                    }
                    else
                    {
                        // Include the server's own explanation - the status alone cannot tell a
                        // rejected token apart from a payload CARE would not accept.
                        string errorBody = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(errorBody) && errorBody.Length > 1000)
                        {
                            errorBody = errorBody.Substring(0, 1000) + "... (truncated)";
                        }

                        LogWarn($"[MPPS] MPPS webhook failed: {studyStatus} - {(int)response.StatusCode} ({response.ReasonPhrase}) for service_request {serviceRequestId} facility {facilityId}. Response body: {(string.IsNullOrWhiteSpace(errorBody) ? "(empty)" : errorBody.Trim())}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"[MPPS] MPPS webhook failed: {studyStatus} for service_request {serviceRequestId} with exception {ex.Message}");
            }
        }


        public bool SetInProgress(string sopInstanceUID, string procedureStepId)
        {
            LogInfo($"[MPPS] SetInProgress: looking up ProcedureStepID={procedureStepId} among {WorklistServer.CurrentWorklistItems.Count} cached worklist items");

            var workItem = WorklistServer.CurrentWorklistItems
                .FirstOrDefault(w => w.ProcedureStepID == procedureStepId);
            if (workItem == null)
            {
                // the procedureStepId provided cannot be found any more, so the data is invalid or the
                // modality tries to start a procedure that has been deleted/changed on the ris side...
                LogWarn($"[MPPS] SetInProgress: no worklist item matched ProcedureStepID={procedureStepId}");
                return false;
            }

            // now here change the sate of the procedure in the database or do similar stuff...
            LogInfo($"Procedure with id {workItem.ProcedureStepID} is started");
            LogInfo($"[MPPS] SetInProgress: matched ServiceRequestId={workItem.ServiceRequestId} AccessionNumber={workItem.AccessionNumber} PatientID={workItem.PatientID} for SOPInstanceUID={sopInstanceUID}");

            // remember the sopInstanceUID and store the worklistitem to which the sopInstanceUID belongs.
            // You should do this more permanent like in database or in file
            PendingProcedures.Add(sopInstanceUID, workItem);

            // Send status update to CARE server
            Task.Run(() => SendStatusToCareServerAsync(workItem.ServiceRequestId, workItem.FacilityId, StatusStarted));

            return true;
        }


        public bool SetDiscontinued(string sopInstanceUID, string reason)
        {
            if (!PendingProcedures.ContainsKey(sopInstanceUID))
            {
                // there is no pending procedure with this sopInstanceUID!
                LogWarn($"[MPPS] SetDiscontinued: no procedure in progress for SOPInstanceUID={sopInstanceUID}");
                return false;
            }
            var workItem = PendingProcedures[sopInstanceUID];

            // now here change the sate of the procedure in the database or do similar stuff...
            LogInfo($"Procedure with id {workItem.ProcedureStepID} is discontinued for reason {reason}");
            LogInfo($"[MPPS] SetDiscontinued: ServiceRequestId={workItem.ServiceRequestId} AccessionNumber={workItem.AccessionNumber} for SOPInstanceUID={sopInstanceUID}");

            // Send status update to CARE server
            Task.Run(() => SendStatusToCareServerAsync(workItem.ServiceRequestId, workItem.FacilityId, StatusDiscontinued));

            // since the procedure was stopped, we remove it from the list of pending procedures
            PendingProcedures.Remove(sopInstanceUID);
            return true;
        }


        public bool SetCompleted(string sopInstanceUID, string doseDescription, List<string> affectedInstanceUIDs)
        {
            if (!PendingProcedures.ContainsKey(sopInstanceUID))
            {
                // there is no pending procedure with this sopInstanceUID!
                LogWarn($"[MPPS] SetCompleted: no procedure in progress for SOPInstanceUID={sopInstanceUID}");
                return false;
            }
            var workItem = PendingProcedures[sopInstanceUID];

            // now here change the sate of the procedure in the database or do similar stuff...
            LogInfo($"Procedure with id {workItem.ProcedureStepID} is completed");
            LogInfo($"[MPPS] SetCompleted: ServiceRequestId={workItem.ServiceRequestId} AccessionNumber={workItem.AccessionNumber} for SOPInstanceUID={sopInstanceUID}");

            // the MPPS completed message contains some additional informations about the performed procedure.
            // this informations are very vendor depending, so read the DICOM Conformance Statement or read
            // the DICOM logfiles to see which informations the vendor sends

            // Send status update to CARE server
            Task.Run(() => SendStatusToCareServerAsync(workItem.ServiceRequestId, workItem.FacilityId, StatusCompleted));

            // since the procedure was completed, we remove it from the list of pending procedures
            PendingProcedures.Remove(sopInstanceUID);
            return true;
        }


    }
}
