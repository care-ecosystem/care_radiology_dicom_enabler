// Copyright (c) 2012-2022 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using FellowOakDicom.Log;
using Plexus.Common.Database;

namespace Worklist_SCP.Model
{

    /// <summary>
    /// An implementation of IMppsSource. For the CARE backend it sends IN_PROGRESS to CARE when the
    /// modality starts a procedure and CANCELLED when it discontinues it; COMPLETED is sent by the SCU
    /// service once the study's images are uploaded. It does not store the MPPS messages
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


        private void LogInfo(string message)
        {
            _logger.Info("{message}", message);
            _fileLogger?.Information("{Message:l}", message);
        }

        private void LogWarn(string message)
        {
            _logger.Warn("{message}", message);
            _fileLogger?.Warning("{Message:l}", message);
        }


        // Only the CARE backend (backend 2) has care_worklist and the CARE scan status API
        private static bool IsCareBackend => (ConfigurationManager.AppSettings["backend"] ?? "2").Trim() == "2";

        // Opens its own DAL each call because C-FIND, C-STORE, MPPS and the refresh timer run
        // concurrently and a ucls_DAL holds a single connection.
        private static ucls_DAL OpenDal() => new ucls_DAL(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));


        /// <summary>
        /// Sends IN_PROGRESS for the work item's service request when the modality starts the procedure.
        /// Once sent, care_worklist.scan_status is STARTED and the SCU service does not send it again
        /// before the upload. When it cannot be sent scan_status is set to START_PENDING, so the SCU
        /// service sends it like one that could not be sent before an upload. Not sent for a procedure
        /// already started, completed or cancelled.
        /// </summary>
        private void SendScanStarted(WorklistItem workItem)
        {
            SendStudyStatus(workItem, ucls_CareScanStatus.StatusInProgress, "STARTED", "START_PENDING",
                new[] { "STARTED", "COMPLETED", "CANCEL_PENDING", "CANCELLED" });
        }

        /// <summary>
        /// Sends CANCELLED for the work item's service request when the modality discontinues the procedure.
        /// Once sent, care_worklist.scan_status is CANCELLED and the SCU service never sends COMPLETED for it.
        /// When it cannot be sent scan_status is set to CANCEL_PENDING and the SCU service retries it every
        /// cycle. Not sent once COMPLETED was sent, as CARE leaves a completed imaging study unchanged.
        /// </summary>
        private void SendScanCancelled(WorklistItem workItem)
        {
            SendStudyStatus(workItem, ucls_CareScanStatus.StatusCancelled, "CANCELLED", "CANCEL_PENDING",
                new[] { "COMPLETED", "CANCELLED" });
        }

        /// <summary>
        /// Sends studyStatus for the work item's service request and sets care_worklist.scan_status to
        /// sentScanStatus, or to pendingScanStatus when it cannot be sent so the SCU service retries it.
        /// Nothing is sent while scan_status is one of skipScanStatuses.
        /// </summary>
        private void SendStudyStatus(WorklistItem workItem, string studyStatus, string sentScanStatus, string pendingScanStatus, string[] skipScanStatuses)
        {
            if (!IsCareBackend)
                return;
            string accessionNumber = workItem.AccessionNumber;
            ucls_DAL dal = null;
            try
            {
                dal = OpenDal();
                string errorString = string.Empty;
                string serviceRequestId = null;
                string scanStatus = null;
                bool found = dal.GetScanState(accessionNumber, ref serviceRequestId, ref scanStatus, ref errorString);
                if (!string.IsNullOrEmpty(errorString) || !found)
                {
                    LogWarn($"[MPPS] Not sending {studyStatus} for AccessionNumber={accessionNumber}: {(found ? errorString : "not in care_worklist")}");
                    return;
                }
                if (skipScanStatuses.Contains(scanStatus))
                {
                    LogInfo($"[MPPS] Not sending {studyStatus} for AccessionNumber={accessionNumber}: scan_status is already {scanStatus}");
                    return;
                }
                if (string.IsNullOrWhiteSpace(serviceRequestId))
                {
                    LogWarn($"[MPPS] Not sending {studyStatus} for AccessionNumber={accessionNumber}: no service request id in care_worklist");
                    return;
                }

                string resolvedFrom = string.Empty;
                string facilityId = dal.GetFacilityId(string.Empty, ref resolvedFrom, ref errorString);
                string baseUrl = ConfigurationManager.AppSettings["careBaseUrl"];
                string token = ConfigurationManager.AppSettings["careToken"];
                string statusPath = ConfigurationManager.AppSettings["statusURL"];

                bool sent = false;
                if (!string.IsNullOrEmpty(errorString) || string.IsNullOrWhiteSpace(facilityId))
                    LogWarn($"[MPPS] Not sending {studyStatus} for AccessionNumber={accessionNumber}: no Facility ID - {(string.IsNullOrEmpty(errorString) ? resolvedFrom : errorString)}. The SCU service will send it.");
                else if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(statusPath))
                    LogWarn($"[MPPS] Not sending {studyStatus} for AccessionNumber={accessionNumber}: careBaseUrl, careToken or statusURL is not set in App.config. The SCU service will send it.");
                else
                    sent = ucls_CareScanStatus.Send(ucls_CareScanStatus.BuildStatusURL(baseUrl, statusPath, facilityId), token, serviceRequestId, accessionNumber, studyStatus,
                        (message, isInfo) => { if (isInfo) LogInfo("[MPPS] " + message); else LogWarn("[MPPS] " + message + ". The SCU service will send it."); });

                string newScanStatus = sent ? sentScanStatus : pendingScanStatus;
                if (newScanStatus != scanStatus)
                {
                    errorString = string.Empty;
                    dal.SetScanStatus(accessionNumber, newScanStatus, ref errorString);
                    if (!string.IsNullOrEmpty(errorString))
                        LogWarn($"[MPPS] care_worklist scan_status update failed for AccessionNumber={accessionNumber}: {errorString}");
                }
            }
            catch (Exception ex)
            {
                LogWarn($"[MPPS] Sending {studyStatus} for AccessionNumber={accessionNumber} failed with exception {ex.Message}");
            }
            finally
            {
                dal?.Dispose();
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

            SendScanStarted(workItem);

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

            // since the procedure was stopped, we remove it from the list of pending procedures
            PendingProcedures.Remove(sopInstanceUID);

            SendScanCancelled(workItem);

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

            // since the procedure was completed, we remove it from the list of pending procedures
            PendingProcedures.Remove(sopInstanceUID);
            return true;
        }


    }
}
