// Copyright (c) 2012-2022 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FellowOakDicom;
using FellowOakDicom.Log;
using FellowOakDicom.Network;
using Worklist_SCP.Model;
using Serilog;
using System.IO;
using System.Reflection;
using Plexus.Common.Database;
using Plexus.Common.config;

namespace Worklist_SCP
{
    public class WorklistService : DicomService, IDicomServiceProvider, IDicomCFindProvider ,IDicomCEchoProvider , IDicomNServiceProvider
    {
        public static IWorklistItemsSource CreateItemsSourceService => new WorklistItemsProvider();
        public static Serilog.ILogger fileLogger = null;
        public static ucls_DAL objDal = null;

        private static readonly DicomTransferSyntax[] _acceptedTransferSyntaxes = new DicomTransferSyntax[]
           {
                DicomTransferSyntax.ExplicitVRLittleEndian,
                DicomTransferSyntax.ExplicitVRBigEndian,
                DicomTransferSyntax.ImplicitVRLittleEndian
           };

        private IMppsSource _mppsSource;
        private IMppsSource MppsSource
        {
            get
            {
                if (_mppsSource == null)
                {
                    _mppsSource = new MppsHandler(Logger);
                }

                return _mppsSource;
            }
        }


        public WorklistService(INetworkStream stream, Encoding fallbackEncoding, FellowOakDicom.Log.ILogger log, DicomServiceDependencies dependencies)
            : base(stream, fallbackEncoding, log, dependencies)
        {
            fileLogger = GetFileLogger();
        }

        private Serilog.ILogger GetFileLogger()
        {
            string logFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "logs/ModalitySCP.txt");
            return new LoggerConfiguration().
                WriteTo.File(logFilePath,
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
                shared: true,
                retainedFileCountLimit: 3,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 5120)
                .CreateLogger();
        }

        public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
        {
            fileLogger?.Information($"[C-ECHO] Request from AE={Association.CallingAE} IP={Association.RemoteHost}");
            if (!validateServer(Association.CallingAE, Association.RemoteHost))
            {
                fileLogger?.Warning($"[C-ECHO] Rejected AE={Association.CallingAE}");
                return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.ProcessingFailure));
            }
            return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
        }


        public async IAsyncEnumerable<DicomCFindResponse> OnCFindRequestAsync(DicomCFindRequest request)
        {

            fileLogger.Information($"Received C-FIND request from AE {Association.CallingAE} with IP: {Association.RemoteHost}");
            string errorString = string.Empty;
            fileLogger.Information($"CFIND : Validating Server with AETitle {Association.CallingAE} with IP: {Association.RemoteHost}");
            if (!validateServer(Association.CallingAE, Association.RemoteHost))
            {
                yield return new DicomCFindResponse(request, DicomStatus.QueryRetrieveUnableToProcess);
            }
            List<string> accessionNos = new List<string>();

            switch (Convert.ToInt32(ConfigurationManager.AppSettings["backend"] ?? "2"))
            {
                case 0:
                    fileLogger.Information($"Fetching Records from Mock/Emulator (List)");
                    var newWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItems();
                    WorklistServer.CurrentWorklistItems = newWorklistItems;
                    fileLogger.Information($"✓ Successfully fetched {newWorklistItems?.Count ?? 0} worklist items from Mock/Emulator");
                    break;
                case 1:
                    fileLogger.Information($"Fetching Records from Plexus Database");
                    var dbWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItemsFromDB();
                    WorklistServer.CurrentWorklistItems = dbWorklistItems;
                    fileLogger.Information($"✓ Successfully fetched {dbWorklistItems?.Count ?? 0} worklist items from Plexus Database");
                    break;
                case 2:
                    fileLogger.Information($"Fetching Records from CARE Server API");
                    //var pellucidWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItemsFromPellucidAsync();
                    var pellucidWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItemsFromCareAsync();
                    WorklistServer.CurrentWorklistItems = pellucidWorklistItems;
                    fileLogger.Information($"✓ Successfully fetched {pellucidWorklistItems?.Count ?? 0} worklist items from CARE Server");
                    break;

            }

            int returnedItemsCount = 0;
            foreach (DicomDataset result in WorklistHandler.FilterWorklistItems(request.Dataset, WorklistServer.CurrentWorklistItems))
            {
                // Insert Into Database
                if (result.GetString(DicomTag.AccessionNumber) != null)
                    accessionNos.Add(result.GetString(DicomTag.AccessionNumber));
                yield return new DicomCFindResponse(request, DicomStatus.Pending) { Dataset = result };
                returnedItemsCount++;
            }
            UpdateStatusinDB(accessionNos);
            fileLogger.Information($"✓ C-FIND completed successfully: returned {returnedItemsCount} worklist items (Accession Numbers: {string.Join(", ", accessionNos)}) to AE {Association.CallingAE} with IP: {Association.RemoteHost}");
            yield return new DicomCFindResponse(request, DicomStatus.Success);
            //}
        }


        private bool validateServer(string aeTitle, string hostAddress)
        {
            string errorString = string.Empty;
            string applicationPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string retVal = cls_PlexusConfig.ReadDetailsFromXML(applicationPath, @"/configurations/checkserver");
            fileLogger?.Information($"[VALIDATE] checkserver={retVal} AE={aeTitle} IP={hostAddress}");
            if (retVal != string.Empty && Convert.ToBoolean(retVal) == true)
            {
                if (objDal == null)
                {
                    objDal = new ucls_DAL(applicationPath);
                }
                if (!objDal.validateAETitle(aeTitle, hostAddress, ref errorString))
                {
                    if (errorString == string.Empty)
                        fileLogger?.Information($"[VALIDATE] AE={aeTitle} IP={hostAddress} not in server list");
                    else
                        fileLogger?.Error($"[VALIDATE] AE={aeTitle} validation failed: {errorString}");
                    return false;
                }
                fileLogger?.Information($"[VALIDATE] AE={aeTitle} validated OK");
            }
            else
            {
                fileLogger?.Information($"[VALIDATE] checkserver disabled, skipping AE validation");
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="accessionNos"></param>
        private void UpdateStatusinDB(List<string> accessionNos)
        {
            string errorString = string.Empty;

            string applicationPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            fileLogger.Information($"Application path : {applicationPath}");
            ucls_DAL objDal = new ucls_DAL(applicationPath);
            try
            {
                foreach (string accessionNo in accessionNos)
                {
                    objDal.UpdateStudyStatusByAscNo(accessionNo, 1, ref errorString);

                    if (errorString != string.Empty)
                    {
                        fileLogger.Information($"Updating DB with MWL Status failed for Accession No {accessionNo} with exception" + errorString);
                    }
                }
            }
            catch (Exception ex)
            {
                fileLogger.Information($"Update Status in Database Failed for MWL with exception" + ex.Message);
            }
            finally
            {
                objDal.Dispose();
            }
        }

  
        /// <summary>
        /// On Connection Closed after C-FIND
        /// </summary>
        /// <param name="exception"></param>
        public void OnConnectionClosed(Exception exception)
        {
            Clean();
            if (exception != null)
            {
                fileLogger.Information($"Error Generating data for C-Find Response with Exception " + exception.Message);
            }
        }


        public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
        {
            //log the abort reason
            //Logger.Error($"Received abort from {source}, reason is {reason}");
            fileLogger.Error($"Received abort from {source}, reason is {reason}");
        }


        public Task OnReceiveAssociationReleaseRequestAsync()
        {
            Clean();
            return SendAssociationReleaseResponseAsync();
        }


        public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
        {
            fileLogger?.Information($"[ASSOC] Request from AE={association.CallingAE} IP={association.RemoteHost} CalledAE={association.CalledAE}");

            if (WorklistServer.AETitle != association.CalledAE)
            {
                fileLogger?.Error($"[ASSOC] Rejected: called AE={association.CalledAE} unknown (expected {WorklistServer.AETitle})");
                return SendAssociationRejectAsync(DicomRejectResult.Permanent, DicomRejectSource.ServiceUser, DicomRejectReason.CalledAENotRecognized);
            }

            foreach (var pc in association.PresentationContexts)
            {
                if (pc.AbstractSyntax == DicomUID.Verification
                    || pc.AbstractSyntax == DicomUID.ModalityWorklistInformationModelFind
                    || pc.AbstractSyntax == DicomUID.ModalityPerformedProcedureStep
                    || pc.AbstractSyntax == DicomUID.ModalityPerformedProcedureStepNotification)
                {
                    pc.AcceptTransferSyntaxes(_acceptedTransferSyntaxes);
                    fileLogger?.Information($"[ASSOC] PC accepted: {pc.AbstractSyntax.Name} (ID={pc.ID})");
                }
                else
                {
                    fileLogger?.Warning($"[ASSOC] PC rejected: {pc.AbstractSyntax} not supported");
                    pc.SetResult(DicomPresentationContextResult.RejectAbstractSyntaxNotSupported);
                }
            }

            fileLogger?.Information($"[ASSOC] Accepted association from AE={association.CallingAE}");
            return SendAssociationAcceptAsync(association);
        }


        public void Clean()
        {
            // cleanup, like cancel outstanding move- or get-jobs
        }


        public async Task<DicomNCreateResponse> OnNCreateRequestAsync(DicomNCreateRequest request)
        {
            if (request.SOPClassUID != DicomUID.ModalityPerformedProcedureStep)
            {
                return new DicomNCreateResponse(request, DicomStatus.SOPClassNotSupported);
            }
            // on N-Create the UID is stored in AffectedSopInstanceUID, in N-Set the UID is stored in RequestedSopInstanceUID
            var affectedSopInstanceUID = request.Command.GetSingleValue<string>(DicomTag.AffectedSOPInstanceUID);
            //Logger.Log(LogLevel.Info, $"reeiving N-Create with SOPUID {affectedSopInstanceUID}");
            fileLogger.Information($"reeiving N-Create with SOPUID {affectedSopInstanceUID}");
            // get the procedureStepIds from the request
            var procedureStepId = request.Dataset
                .GetSequence(DicomTag.ScheduledStepAttributesSequence)
                .First()
                .GetSingleValue<string>(DicomTag.ScheduledProcedureStepID);
            var ok = MppsSource.SetInProgress(affectedSopInstanceUID, procedureStepId);

            return new DicomNCreateResponse(request, ok ? DicomStatus.Success : DicomStatus.ProcessingFailure);
        }


        public async Task<DicomNSetResponse> OnNSetRequestAsync(DicomNSetRequest request)
        {
            if (request.SOPClassUID != DicomUID.ModalityPerformedProcedureStep)
            {
                return new DicomNSetResponse(request, DicomStatus.SOPClassNotSupported);
            }
            // on N-Create the UID is stored in AffectedSopInstanceUID, in N-Set the UID is stored in RequestedSopInstanceUID
            var requestedSopInstanceUID = request.Command.GetSingleValue<string>(DicomTag.RequestedSOPInstanceUID);
            //Logger.Log(LogLevel.Info, $"receiving N-Set with SOPUID {requestedSopInstanceUID}");.I
            fileLogger.Information($"receiving N-Set with SOPUID {requestedSopInstanceUID}");

            var status = request.Dataset.GetSingleValue<string>(DicomTag.PerformedProcedureStepStatus);
            if (status == "COMPLETED")
            {
                // most vendors send some informations with the mpps-completed message. 
                // this information should be stored into the datbase
                var doseDescription = request.Dataset.GetSingleValueOrDefault(DicomTag.CommentsOnRadiationDose, string.Empty);
                var listOfInstanceUIDs = new List<string>();
                foreach (var seriesDataset in request.Dataset.GetSequence(DicomTag.PerformedSeriesSequence))
                {
                    // you can read here some information about the series that the modalidy created
                    //seriesDataset.Get(DicomTag.SeriesDescription, string.Empty);
                    //seriesDataset.Get(DicomTag.PerformingPhysicianName, string.Empty);
                    //seriesDataset.Get(DicomTag.ProtocolName, string.Empty);
                    foreach (var instanceDataset in seriesDataset.GetSequence(DicomTag.ReferencedImageSequence))
                    {
                        // here you can read the SOPClassUID and SOPInstanceUID
                        var instanceUID = instanceDataset.GetSingleValueOrDefault(DicomTag.ReferencedSOPInstanceUID, string.Empty);
                        if (!string.IsNullOrEmpty(instanceUID))
                        {
                            listOfInstanceUIDs.Add(instanceUID);
                        }
                    }
                }
                var ok = MppsSource.SetCompleted(requestedSopInstanceUID, doseDescription, listOfInstanceUIDs);

                return new DicomNSetResponse(request, ok ? DicomStatus.Success : DicomStatus.ProcessingFailure);
            }
            else if (status == "DISCONTINUED")
            {
                // some vendors send a reason code or description with the mpps-discontinued message
                // var reason = request.Dataset.Get(DicomTag.PerformedProcedureStepDiscontinuationReasonCodeSequence);
                var ok = MppsSource.SetDiscontinued(requestedSopInstanceUID, string.Empty);

                return new DicomNSetResponse(request, ok ? DicomStatus.Success : DicomStatus.ProcessingFailure);
            }
            else
            {
                return new DicomNSetResponse(request, DicomStatus.InvalidAttributeValue);
            }
        }


        #region not supported methods but that are required because of the interface

        public async Task<DicomNDeleteResponse> OnNDeleteRequestAsync(DicomNDeleteRequest request)
        {
            //Logger.Log(LogLevel.Info, "receiving N-Delete, not supported");
            fileLogger.Information("receiving N-Delete, not supported");
            return new DicomNDeleteResponse(request, DicomStatus.UnrecognizedOperation);
        }

        public async Task<DicomNEventReportResponse> OnNEventReportRequestAsync(DicomNEventReportRequest request)
        {
            //Logger.Log(LogLevel.Info, "receiving N-Event, not supported");
            fileLogger.Information("receiving N-Event, not supported");
            return new DicomNEventReportResponse(request, DicomStatus.UnrecognizedOperation);
        }

        public async Task<DicomNGetResponse> OnNGetRequestAsync(DicomNGetRequest request)
        {
            //Logger.Log(LogLevel.Info, "receiving N-Get, not supported");
            fileLogger.Information("receiving N-Get, not supported");
            return new DicomNGetResponse(request, DicomStatus.UnrecognizedOperation);
        }

        public async Task<DicomNActionResponse> OnNActionRequestAsync(DicomNActionRequest request)
        {
            //Logger.Log(LogLevel.Info, "receiving N-Action, not supported");
            fileLogger.Information("receiving N-Action, not supported");
            return new DicomNActionResponse(request, DicomStatus.UnrecognizedOperation);
        }

        #endregion

    }
}
