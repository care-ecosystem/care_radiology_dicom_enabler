// Copyright (c) 2012-2022 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using System;
using System.Collections.Generic;
using System.Linq;
using FellowOakDicom.Log;

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
