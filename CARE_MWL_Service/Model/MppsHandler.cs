// Copyright (c) 2012-2022 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using System.Collections.Generic;
using System.Linq;
using FellowOakDicom.Log;
using System;
using Newtonsoft.Json;

namespace Worklist_SCP.Model
{

    /// <summary>
    /// An implementation of IMppsSource, that does only logging but does not store the MPPS messages
    /// </summary>
    public class MppsHandler : IMppsSource
    {

        public static Dictionary<string, WorklistItem> PendingProcedures { get; } = new Dictionary<string, WorklistItem>();

        private readonly ILogger _logger;


        public MppsHandler(ILogger logger)
        {
            _logger = logger;
        }

        public bool SetInProgress(string sopInstanceUID, string procedureStepId, string accessionNumber)
        {
            var workItem = WorklistServer.CurrentWorklistItems
                .FirstOrDefault(w => w.AccessionNumber == accessionNumber);

            if (workItem != null)
            {
                System.IO.File.AppendAllText(@"C:\temp\mpps_debug.txt",
                    $"\n[{DateTime.Now:HH:mm:ss}] MATCH SUCCESS - accessionNumber='{accessionNumber}' | ServiceRequestId='{workItem.ServiceRequestId}' | StudyUID='{workItem.StudyUID}'\n");

                // Extract webhook fields
                string service_request_id = workItem.ServiceRequestId;
                string study_id = workItem.StudyUID;
                string mpps_status = "STARTED";

                // Create JSON payload
                var payload = new
                {
                    service_request_id = service_request_id,
                    study_id = study_id,
                    mpps_status = mpps_status
                };

                // Send webhook to Radiology Plugin
                SendWebhookToRadiology(payload);

                return true;
            }

            System.IO.File.AppendAllText(@"C:\temp\mpps_debug.txt",
                $"\n[{DateTime.Now:HH:mm:ss}] MATCH FAILED - accessionNumber='{accessionNumber}' not found in worklist\n");
            return false;
        }


        private void SendWebhookToRadiology(object payload)
        {
            try
            {
                System.IO.File.AppendAllText(@"C:\temp\mpps_debug.txt",
                    $"\n[{DateTime.Now:HH:mm:ss}] SENDING WEBHOOK...\n");

                using (var client = new System.Net.Http.HttpClient())
                {
                    var jsonContent = JsonConvert.SerializeObject(payload);
                    System.IO.File.AppendAllText(@"C:\temp\mpps_debug.txt",
                        $"[{DateTime.Now:HH:mm:ss}] Payload: {jsonContent}\n");

                    var content = new System.Net.Http.StringContent(
                        jsonContent,
                        System.Text.Encoding.UTF8,
                        "application/json");

                    client.DefaultRequestHeaders.Add("Authorization", "RADOMSECRET");

                    var response = client.PostAsync(
                        "https://staging.carehmis.dpdns.org/api/care_radiology/webhooks/mpps/",
                        content).Result;

                    System.IO.File.AppendAllText(@"C:\temp\mpps_debug.txt",
                        $"[{DateTime.Now:HH:mm:ss}] WEBHOOK RESPONSE: {response.StatusCode}\n");

                    _logger.Info($"Webhook sent to Radiology: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(@"C:\temp\mpps_debug.txt",
                    $"\n[{DateTime.Now:HH:mm:ss}] WEBHOOK ERROR: {ex.Message}\n");

                _logger.Error($"Error sending webhook to Radiology: {ex.Message}");
            }
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

            // the MPPS completed message contains some additional informations about the performed procedure.
            // this informations are very vendor depending, so read the DICOM Conformance Statement or read
            // the DICOM logfiles to see which informations the vendor sends

            // since the procedure was completed, we remove it from the list of pending procedures
            PendingProcedures.Remove(sopInstanceUID);
            return true;
        }

        
    }
}
