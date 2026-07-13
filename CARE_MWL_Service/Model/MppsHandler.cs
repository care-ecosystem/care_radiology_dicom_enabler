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


        public MppsHandler(ILogger logger)
        {
            _logger = logger;
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
                if (backend != 2 || string.IsNullOrWhiteSpace(serviceRequestId))
                {
                    return;
                }

                string baseUrl = ConfigurationManager.AppSettings["careBaseUrl"]?.ToString();
                string token = ConfigurationManager.AppSettings["careToken"]?.ToString();

                if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
                {
                    _logger.Warn("CARE server URL or token not configured, skipping webhook");
                    return;
                }

                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    _logger.Warn($"[MPPS] facility_id missing for service_request {serviceRequestId} - sending webhook without it");
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
                        _logger.Info($" MPPS webhook sent to CARE: {studyStatus} for service_request {serviceRequestId} facility {facilityId}");
                    }
                    else
                    {
                        _logger.Warn($"MPPS webhook failed: {response.StatusCode} for service_request {serviceRequestId} facility {facilityId}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error sending MPPS webhook to CARE server: {ex.Message}");
            }
        }


        public bool SetInProgress(string sopInstanceUID, string procedureStepId)
        {
            _logger.Info($"[MPPS] SetInProgress: looking up ProcedureStepID={procedureStepId} among {WorklistServer.CurrentWorklistItems.Count} cached worklist items");

            var workItem = WorklistServer.CurrentWorklistItems
                .FirstOrDefault(w => w.ProcedureStepID == procedureStepId);
            if (workItem == null)
            {
                // the procedureStepId provided cannot be found any more, so the data is invalid or the
                // modality tries to start a procedure that has been deleted/changed on the ris side...
                _logger.Warn($"[MPPS] SetInProgress: no worklist item matched ProcedureStepID={procedureStepId}");
                return false;
            }

            // now here change the sate of the procedure in the database or do similar stuff...
            _logger.Info($"Procedure with id {workItem.ProcedureStepID} of Patient {workItem.Surname} {workItem.Forename} is started");
            _logger.Info($"[MPPS] SetInProgress: matched ServiceRequestId={workItem.ServiceRequestId} AccessionNumber={workItem.AccessionNumber} PatientID={workItem.PatientID} for SOPInstanceUID={sopInstanceUID}");

            // remember the sopInstanceUID and store the worklistitem to which the sopInstanceUID belongs.
            // You should do this more permanent like in database or in file
            PendingProcedures.Add(sopInstanceUID, workItem);

            // Send status update to CARE server
            Task.Run(() => SendStatusToCareServerAsync(workItem.ServiceRequestId, workItem.FacilityId, "Scan Started"));

            return true;
        }


        public bool SetDiscontinued(string sopInstanceUID, string reason)
        {
            if (!PendingProcedures.ContainsKey(sopInstanceUID))
            {
                // there is no pending procedure with this sopInstanceUID!
                return false;
            }
            var workItem = PendingProcedures[sopInstanceUID];

            // now here change the sate of the procedure in the database or do similar stuff...
            _logger.Info($"Procedure with id {workItem.ProcedureStepID} of Patient {workItem.Surname} {workItem.Forename} is discontinued for reason {reason}");
            _logger.Info($"[MPPS] SetDiscontinued: ServiceRequestId={workItem.ServiceRequestId} AccessionNumber={workItem.AccessionNumber} for SOPInstanceUID={sopInstanceUID}");

            // Send status update to CARE server
            Task.Run(() => SendStatusToCareServerAsync(workItem.ServiceRequestId, workItem.FacilityId, "Scan Cancelled"));

            // since the procedure was stopped, we remove it from the list of pending procedures
            PendingProcedures.Remove(sopInstanceUID);
            return true;
        }


        public bool SetCompleted(string sopInstanceUID, string doseDescription, List<string> affectedInstanceUIDs)
        {
            if (!PendingProcedures.ContainsKey(sopInstanceUID))
            {
                // there is no pending procedure with this sopInstanceUID!
                return false;
            }
            var workItem = PendingProcedures[sopInstanceUID];

            // now here change the sate of the procedure in the database or do similar stuff...
            _logger.Info($"Procedure with id {workItem.ProcedureStepID} of Patient {workItem.Surname} {workItem.Forename} is completed");
            _logger.Info($"[MPPS] SetCompleted: ServiceRequestId={workItem.ServiceRequestId} AccessionNumber={workItem.AccessionNumber} for SOPInstanceUID={sopInstanceUID}");

            // the MPPS completed message contains some additional informations about the performed procedure.
            // this informations are very vendor depending, so read the DICOM Conformance Statement or read
            // the DICOM logfiles to see which informations the vendor sends

            // Send status update to CARE server
            Task.Run(() => SendStatusToCareServerAsync(workItem.ServiceRequestId, workItem.FacilityId, "Scan Completed"));

            // since the procedure was completed, we remove it from the list of pending procedures
            PendingProcedures.Remove(sopInstanceUID);
            return true;
        }


    }
}
