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
using Plexus_MWL_Service.logs;

namespace Worklist_SCP
{
    public class WorklistService : DicomService, IDicomServiceProvider, IDicomCFindProvider ,IDicomCEchoProvider , IDicomNServiceProvider, IDicomCStoreProvider
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
                    _mppsSource = new MppsHandler(Logger, fileLogger);
                }

                return _mppsSource;
            }
        }


        // The connecting device's IP and port, known before the association is requested
        private readonly string _remoteHost;
        private readonly int _remotePort;

        // The connecting device, for error logs: AE title once the association is requested, IP and port always
        private string Device => RemoteDeviceInfo.DescribeConnection(Association, _remoteHost, _remotePort);


        public WorklistService(INetworkStream stream, Encoding fallbackEncoding, FellowOakDicom.Log.ILogger log, DicomServiceDependencies dependencies)
            : base(stream, fallbackEncoding, log, dependencies)
        {
            fileLogger = GetFileLogger();
            RemoteDeviceInfo.GetRemoteEndpoint(stream, out _remoteHost, out _remotePort);
            fileLogger.Information($"[CONN] Connection opened from IP={_remoteHost} Port={_remotePort}");
        }

        private Serilog.ILogger GetFileLogger()
        {
            return new LoggerConfiguration()
                .WriteTo.Sink(DailyFolderSink.For("ModalitySCP.txt"), Serilog.Events.LogEventLevel.Information)
                .CreateLogger();
        }

        public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
        {
            // The calling AE was checked against the Server List when the association was accepted.
            fileLogger?.Information($"[C-ECHO] Request from device: {RemoteDeviceInfo.Describe(Association)}");
            return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
        }


        public async IAsyncEnumerable<DicomCFindResponse> OnCFindRequestAsync(DicomCFindRequest request)
        {

            // The calling AE was checked against the Server List when the association was accepted.
            fileLogger.Information($"Received C-FIND request from AE {Association.CallingAE} with IP: {Association.RemoteHost}");
            fileLogger.Information($"[C-FIND] Worklist requested by device: {RemoteDeviceInfo.Describe(Association)}");
            fileLogger.Information($"[C-FIND] Query keys: {RemoteDeviceInfo.DescribeQuery(request.Dataset)}");
            List<string> accessionNos = new List<string>();

            // An exception here would make fo-dicom drop the association, so the device would only see
            // "connection closed": log it with the device and answer with a DICOM failure status instead
            List<DicomDataset> results;
            try
            {
                results = FindWorklistItems(request);
            }
            catch (Exception ex)
            {
                fileLogger.Error($"[C-FIND] Fetching the worklist for device {Device} failed with exception : {ex.Message} - returning a C-FIND failure");
                results = null;
            }
            if (results == null)
            {
                yield return new DicomCFindResponse(request, DicomStatus.ProcessingFailure);
                yield break;
            }

            int returnedItemsCount = 0;
            foreach (DicomDataset result in results)
            {
                // Insert Into Database
                if (result.GetString(DicomTag.AccessionNumber) != null)
                    accessionNos.Add(result.GetString(DicomTag.AccessionNumber));
                yield return new DicomCFindResponse(request, DicomStatus.Pending) { Dataset = result };
                returnedItemsCount++;
            }
            UpdateStatusinDB(accessionNos);
            if (returnedItemsCount == 0)
                // The query keys logged above show which of the device's filters matched nothing
                fileLogger.Warning($" C-FIND completed with no worklist items for AE {Association.CallingAE} with IP: {Association.RemoteHost} - check the order is scheduled in CARE and the query keys are correct");
            else
                fileLogger.Information($" C-FIND completed successfully: returned {returnedItemsCount} worklist items to AE {Association.CallingAE} with IP: {Association.RemoteHost}");
            yield return new DicomCFindResponse(request, DicomStatus.Success);
            //}
        }


        /// <summary>
        /// The worklist items that match a C-FIND query, from the backend set in App.config
        /// </summary>
        private List<DicomDataset> FindWorklistItems(DicomCFindRequest request)
        {
            List<DicomDataset> results = null;

            switch (Convert.ToInt32(ConfigurationManager.AppSettings["backend"] ?? "2"))
            {
                case 0:
                    fileLogger.Information($"Fetching Records from List");
                    var newWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItems();
                    WorklistServer.CurrentWorklistItems = newWorklistItems;
                    fileLogger.Information($" Successfully fetched {newWorklistItems?.Count ?? 0} worklist items from List");
                    break;
                case 1:
                    fileLogger.Information($"Fetching Records from Plexus Database");
                    var dbWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItemsFromDB();
                    WorklistServer.CurrentWorklistItems = dbWorklistItems;
                    fileLogger.Information($" Successfully fetched {dbWorklistItems?.Count ?? 0} worklist items from Plexus Database");
                    break;
                case 2:
                    string facilityId = getFacilityId(Association.CallingAE);
                    if (string.IsNullOrWhiteSpace(facilityId))
                    {
                        // Facility ID is mandatory: do not call the CARE worklist API without it.
                        // getFacilityId has already logged why it is missing.
                        fileLogger.Warning($"Skipping CARE worklist fetch for AE {Association.CallingAE} - no Facility ID set in the Configuration tab; returning no worklist items");
                        WorklistServer.CurrentWorklistItems = new List<WorklistItem>();
                        break;
                    }
                    // The worklist is served from care_worklist. The CARE API is called only when no
                    // row there matches this query, then care_worklist is read again.
                    var itemsSource = CreateItemsSourceService;
                    fileLogger.Information($"Fetching Records from care_worklist for Facility ID {facilityId}");
                    var careWorklistItems = itemsSource.GetCareWorklistItemsFromDB(facilityId);
                    results = WorklistHandler.FilterWorklistItems(request.Dataset, careWorklistItems).ToList();
                    if (results.Count == 0)
                    {
                        fileLogger.Information($"No care_worklist item matches the C-FIND from AE {Association.CallingAE} ({careWorklistItems.Count} scheduled) - refreshing care_worklist from the CARE worklist API");
                        if (itemsSource.RefreshCareWorklistFromApi(facilityId))
                        {
                            careWorklistItems = itemsSource.GetCareWorklistItemsFromDB(facilityId);
                            results = WorklistHandler.FilterWorklistItems(request.Dataset, careWorklistItems).ToList();
                        }
                        else
                        {
                            fileLogger.Warning($"Refreshing care_worklist from the CARE worklist API failed - answering from the existing care_worklist rows");
                        }
                    }
                    WorklistServer.CurrentWorklistItems = careWorklistItems;
                    fileLogger.Information($" Successfully fetched {careWorklistItems.Count} worklist items from care_worklist, {results.Count} matching the C-FIND");
                    break;

            }

            return results ?? WorklistHandler.FilterWorklistItems(request.Dataset, WorklistServer.CurrentWorklistItems).ToList();
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
                        fileLogger?.Warning($"[VALIDATE] AE={aeTitle} IP={hostAddress} not in server list");
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
        /// Reads the Facility ID from the Configuration tab so the CARE worklist request is scoped to
        /// this enabler's facility. The Facility ID is mandatory: when none is set this returns empty,
        /// and the caller then fetches nothing rather than querying every facility. The error naming
        /// the cause is written to the log.
        /// </summary>
        private string getFacilityId(string aeTitle)
        {
            string errorString = string.Empty;
            string resolvedFrom = string.Empty;
            string applicationPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            try
            {
                if (objDal == null)
                {
                    objDal = new ucls_DAL(applicationPath);
                }
                string facilityId = objDal.GetFacilityId(aeTitle, ref resolvedFrom, ref errorString);
                if (errorString != string.Empty)
                {
                    fileLogger?.Error($"[FACILITY] Lookup failed for AE={aeTitle}: {errorString}");
                    return string.Empty;
                }
                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    fileLogger?.Error($"[FACILITY] No Facility ID for AE={aeTitle} - {resolvedFrom}. The CARE worklist will not be queried; enter a Facility ID in the Configuration tab.");
                    return string.Empty;
                }
                fileLogger?.Information($"[FACILITY] AE={aeTitle} resolved to Facility ID={facilityId} from {resolvedFrom}");
                return facilityId;
            }
            catch (Exception ex)
            {
                fileLogger?.Error($"[FACILITY] Lookup failed for AE={aeTitle} with exception {ex.Message}");
                return string.Empty;
            }
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
                        fileLogger.Error($"Updating DB with MWL Status failed for Accession No {accessionNo} with exception" + errorString);
                    }
                }
            }
            catch (Exception ex)
            {
                fileLogger.Error($"Update Status in Database Failed for MWL with exception" + ex.Message);
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
                // A network error, a timeout or a malformed message from the device ended the connection
                fileLogger?.Error($"[CONN] Connection with device {Device} ended with an error: {exception.GetType().Name}: {exception.Message}" +
                    (exception.InnerException != null ? $" ({exception.InnerException.Message})" : string.Empty));
            }
            else
            {
                fileLogger?.Information($"[CONN] Connection closed with device {Device}");
            }
        }


        public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
        {
            //log the abort reason
            //Logger.Error($"Received abort from {source}, reason is {reason}");
            fileLogger.Error($"Received abort from {source}, reason is {reason} - device {Device}");
        }


        public Task OnReceiveAssociationReleaseRequestAsync()
        {
            Clean();
            fileLogger?.Information($"[ASSOC] Release request from device {Device}");
            return SendAssociationReleaseResponseAsync();
        }


        public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
        {
            fileLogger?.Information($"[ASSOC] Request from device: {RemoteDeviceInfo.Describe(association)}");

            if (WorklistServer.AETitle != association.CalledAE)
            {
                fileLogger?.Error($"[ASSOC] Rejected device {RemoteDeviceInfo.Describe(association)}: called AE={association.CalledAE} unknown (expected {WorklistServer.AETitle})");
                return SendAssociationRejectAsync(DicomRejectResult.Permanent, DicomRejectSource.ServiceUser, DicomRejectReason.CalledAENotRecognized);
            }

            // Checked once here so every service on the association - C-ECHO, C-FIND and the MPPS
            // N-CREATE / N-SET that update CARE - is limited to modalities in the Server List.
            if (!validateServer(association.CallingAE, association.RemoteHost))
            {
                fileLogger?.Error($"[ASSOC] Rejected device {RemoteDeviceInfo.Describe(association)}: calling AE={association.CallingAE} IP={association.RemoteHost} is not in the Server List");
                return SendAssociationRejectAsync(DicomRejectResult.Permanent, DicomRejectSource.ServiceUser, DicomRejectReason.CallingAENotRecognized);
            }

            foreach (var pc in association.PresentationContexts)
            {
                if (pc.AbstractSyntax == DicomUID.Verification
                    || pc.AbstractSyntax == DicomUID.ModalityWorklistInformationModelFind
                    || pc.AbstractSyntax == DicomUID.ModalityPerformedProcedureStep
                    || pc.AbstractSyntax == DicomUID.ModalityPerformedProcedureStepNotification)
                {
                    pc.AcceptTransferSyntaxes(_acceptedTransferSyntaxes);
                    if (pc.Result == DicomPresentationContextResult.Accept)
                        fileLogger?.Information($"[ASSOC] PC accepted: {pc.AbstractSyntax.Name} (ID={pc.ID})");
                    else
                        fileLogger?.Warning($"[ASSOC] PC not accepted ({pc.Result}): {pc.AbstractSyntax.Name} (ID={pc.ID}) - none of the transfer syntaxes " +
                            $"{string.Join(", ", pc.GetTransferSyntaxes().Select(ts => ts.UID.Name))} proposed by AE={association.CallingAE} IP={association.RemoteHost} is supported");
                }
                else if (pc.AbstractSyntax.StorageCategory != DicomStorageCategory.None)
                {
                    // A modality that has this port configured as its image destination is misconfigured
                    // - images belong on the Store SCP port. Accept anyway so the study is not lost: the
                    // instance is written to the SCP folder and CARE_SCU_Service uploads it from there.
                    // Whatever the modality proposes is accepted, because the file is written to disk
                    // exactly as received and its pixel data is never decoded here.
                    pc.AcceptTransferSyntaxes(pc.GetTransferSyntaxes().ToArray());
                    fileLogger?.Warning($"[ASSOC] Storage PC accepted on worklist port: {pc.AbstractSyntax.Name} (ID={pc.ID}) from AE={association.CallingAE} IP={association.RemoteHost}. Configure this modality to send images to the Store SCP port.");
                }
                else
                {
                    fileLogger?.Warning($"[ASSOC] PC rejected: {pc.AbstractSyntax} not supported - proposed by AE={association.CallingAE} IP={association.RemoteHost}");
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


        /// <summary>
        /// Handles an image sent to the worklist port by a modality that should have been pointed at the
        /// Store SCP port. This service has no upload path of its own, so it only drops the instance into
        /// the SCP folder using the same layout the Store SCP writes. CARE_SCU_Service already scans that
        /// folder on its timer and uploads to the CARE backend, so nothing further is needed here.
        /// </summary>
        public async Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
        {
            // GetSingleValue throws on a missing or empty tag, and an exception here makes fo-dicom drop the
            // association, so the sender sees "connection closed" instead of a DICOM error status.
            string studyUid = request.Dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty).Trim();
            string instUid = request.SOPInstanceUID.UID;

            fileLogger?.Warning($"[MWL][C-STORE] Image received on the worklist port from AE={Association.CallingAE} IP={Association.RemoteHost} StudyInstanceUID={studyUid} SOPInstanceUID={instUid}");
            fileLogger?.Information($"[MWL][C-STORE] Sending device: {RemoteDeviceInfo.Describe(Association)}");
            fileLogger?.Information($"[MWL][C-STORE] Device details in the image: {RemoteDeviceInfo.Describe(request.Dataset)}");

            if (!validateServer(Association.CallingAE, Association.RemoteHost))
            {
                fileLogger?.Error($"[MWL][C-STORE] Rejected device {Device}: not in the Server List");
                return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
            }

            if (studyUid == string.Empty)
            {
                fileLogger?.Error($"[MWL][C-STORE] Rejected SOPInstanceUID={instUid} from AE={Association.CallingAE}: StudyInstanceUID is missing or empty");
                return new DicomCStoreResponse(request, DicomStatus.StorageDataSetDoesNotMatchSOPClassError);
            }

            try
            {
                string storageFolder = Path.Combine(WorklistServer.GetScpFolder(), studyUid);
                if (!Directory.Exists(storageFolder))
                {
                    Directory.CreateDirectory(storageFolder);
                }

                string filePath = Path.Combine(storageFolder, instUid) + ".dcm";
                await request.File.SaveAsync(filePath);

                if (!File.Exists(filePath))
                {
                    fileLogger?.Error($"[MWL][C-STORE] File was not written to {filePath}; the SCU service will have nothing to upload.");
                    return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
                }

                fileLogger?.Information($"[MWL][C-STORE] Transferred to SCP folder, awaiting upload by the SCU service");
                fileLogger?.Information($"[MWL][C-STORE]   - Calling AE: {Association.CallingAE}");
                fileLogger?.Information($"[MWL][C-STORE]   - Patient ID: {request.Dataset.GetSingleValueOrDefault(DicomTag.PatientID, "N/A")}");
                fileLogger?.Information($"[MWL][C-STORE]   - Accession Number: {request.Dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, "N/A")}");
                fileLogger?.Information($"[MWL][C-STORE]   - Modality: {request.Dataset.GetSingleValueOrDefault(DicomTag.Modality, "N/A")}");
                fileLogger?.Information($"[MWL][C-STORE]   - Study UID: {studyUid}");
                fileLogger?.Information($"[MWL][C-STORE]   - Instance UID: {instUid}");
                fileLogger?.Information($"[MWL][C-STORE]   - File Path: {filePath}");
            }
            catch (Exception ex)
            {
                fileLogger?.Error($"[MWL][C-STORE] Writing StudyInstanceUID={studyUid} SOPInstanceUID={instUid} from device {Device} to the SCP folder failed with exception : " + ex.Message);
                return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
            }

            return new DicomCStoreResponse(request, DicomStatus.Success);
        }


        public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
        {
            // fo-dicom sends the error response; this only records which device's image failed
            fileLogger?.Error($"[MWL][C-STORE] Receiving an image from device {Device} failed with exception : {e?.Message} (temp file {tempFileName})");
            return Task.CompletedTask;
        }


        public async Task<DicomNCreateResponse> OnNCreateRequestAsync(DicomNCreateRequest request)
        {
            // An exception here would make fo-dicom drop the association, so the device would only see
            // "connection closed": log it with the device and answer with a DICOM failure status instead
            try
            {
                return ProcessNCreate(request);
            }
            catch (Exception ex)
            {
                fileLogger?.Error($"[MPPS][N-CREATE] Processing the N-CREATE from device {Device} failed with exception : {ex.Message} - returning a failure status");
                return new DicomNCreateResponse(request, DicomStatus.ProcessingFailure);
            }
        }


        private DicomNCreateResponse ProcessNCreate(DicomNCreateRequest request)
        {
            if (request.SOPClassUID != DicomUID.ModalityPerformedProcedureStep)
            {
                fileLogger?.Warning($"[MPPS][N-CREATE] Rejected SOP class {request.SOPClassUID} from device {Device} - only MPPS is supported");
                return new DicomNCreateResponse(request, DicomStatus.SOPClassNotSupported);
            }
            // on N-Create the UID is stored in AffectedSopInstanceUID, in N-Set the UID is stored in RequestedSopInstanceUID
            var affectedSopInstanceUID = request.Command.GetSingleValue<string>(DicomTag.AffectedSOPInstanceUID);
            fileLogger.Information($"[MPPS][N-CREATE] Received from AE={Association.CallingAE} SOPInstanceUID={affectedSopInstanceUID}");
            fileLogger.Information($"[MPPS][N-CREATE] Sending device: {RemoteDeviceInfo.Describe(Association)}");
            fileLogger.Information($"[MPPS][N-CREATE] Device details in the MPPS: {RemoteDeviceInfo.Describe(request.Dataset)}");

            // get the procedureStepIds from the request
            var scheduledStepItem = request.Dataset
                .GetSequence(DicomTag.ScheduledStepAttributesSequence)
                .First();
            var procedureStepId = scheduledStepItem.GetSingleValueOrDefault(DicomTag.ScheduledProcedureStepID, string.Empty);
            var accessionNumber = scheduledStepItem.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty);
            var requestedProcedureId = scheduledStepItem.GetSingleValueOrDefault(DicomTag.RequestedProcedureID, string.Empty);
            var studyInstanceUid = scheduledStepItem.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty);
            fileLogger.Information($"[MPPS][N-CREATE] ScheduledStepAttributesSequence: ProcedureStepID={procedureStepId} AccessionNumber={accessionNumber} RequestedProcedureID={requestedProcedureId} StudyInstanceUID={studyInstanceUid}");

            var matchCount = WorklistServer.CurrentWorklistItems.Count(w => w.ProcedureStepID == procedureStepId);
            if (matchCount > 1)
            {
                fileLogger.Warning($"[MPPS][N-CREATE] {matchCount} worklist items share ProcedureStepID={procedureStepId} - the FIRST match will be used, which may be the wrong patient/service request. Verify ProcedureStepID is populated uniquely per item.");
            }
            else if (matchCount == 0)
            {
                fileLogger.Warning($"[MPPS][N-CREATE] No worklist item found with ProcedureStepID={procedureStepId} among {WorklistServer.CurrentWorklistItems.Count} cached items. The worklist may have been refreshed since the C-FIND that returned this item, or AccessionNumber={accessionNumber} should be used instead.");
            }

            var ok = MppsSource.SetInProgress(affectedSopInstanceUID, procedureStepId);
            fileLogger.Information($"[MPPS][N-CREATE] SetInProgress result={ok} for SOPInstanceUID={affectedSopInstanceUID}");
            if (!ok)
                fileLogger.Error($"[MPPS][N-CREATE] Returning a failure status to device {Device} for SOPInstanceUID={affectedSopInstanceUID}");

            return new DicomNCreateResponse(request, ok ? DicomStatus.Success : DicomStatus.ProcessingFailure);
        }


        public async Task<DicomNSetResponse> OnNSetRequestAsync(DicomNSetRequest request)
        {
            // An exception here would make fo-dicom drop the association, so the device would only see
            // "connection closed": log it with the device and answer with a DICOM failure status instead
            try
            {
                return ProcessNSet(request);
            }
            catch (Exception ex)
            {
                fileLogger?.Error($"[MPPS][N-SET] Processing the N-SET from device {Device} failed with exception : {ex.Message} - returning a failure status");
                return new DicomNSetResponse(request, DicomStatus.ProcessingFailure);
            }
        }


        private DicomNSetResponse ProcessNSet(DicomNSetRequest request)
        {
            if (request.SOPClassUID != DicomUID.ModalityPerformedProcedureStep)
            {
                fileLogger?.Warning($"[MPPS][N-SET] Rejected SOP class {request.SOPClassUID} from device {Device} - only MPPS is supported");
                return new DicomNSetResponse(request, DicomStatus.SOPClassNotSupported);
            }
            // on N-Create the UID is stored in AffectedSopInstanceUID, in N-Set the UID is stored in RequestedSopInstanceUID
            var requestedSopInstanceUID = request.Command.GetSingleValue<string>(DicomTag.RequestedSOPInstanceUID);
            //Logger.Log(LogLevel.Info, $"receiving N-Set with SOPUID {requestedSopInstanceUID}");.I
            fileLogger.Information($"receiving N-Set with SOPUID {requestedSopInstanceUID}");
            fileLogger.Information($"[MPPS][N-SET] Sending device: {RemoteDeviceInfo.Describe(Association)}");

            var status = request.Dataset.GetSingleValueOrDefault(DicomTag.PerformedProcedureStepStatus, string.Empty);
            if (status == "COMPLETED")
            {
                // most vendors send some informations with the mpps-completed message. 
                // this information should be stored into the datbase
                var doseDescription = request.Dataset.GetSingleValueOrDefault(DicomTag.CommentsOnRadiationDose, string.Empty);
                var listOfInstanceUIDs = new List<string>();
                // PerformedSeriesSequence is optional in practice - some modalities omit it - so a missing
                // one must not throw and fail the N-SET.
                request.Dataset.TryGetSequence(DicomTag.PerformedSeriesSequence, out var performedSeries);
                foreach (var seriesDataset in performedSeries?.Items ?? new List<DicomDataset>())
                {
                    // you can read here some information about the series that the modalidy created
                    //seriesDataset.Get(DicomTag.SeriesDescription, string.Empty);
                    //seriesDataset.Get(DicomTag.PerformingPhysicianName, string.Empty);
                    //seriesDataset.Get(DicomTag.ProtocolName, string.Empty);
                    seriesDataset.TryGetSequence(DicomTag.ReferencedImageSequence, out var referencedImages);
                    foreach (var instanceDataset in referencedImages?.Items ?? new List<DicomDataset>())
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
                fileLogger.Information($"[MPPS][N-SET] COMPLETED result={ok} for SOPInstanceUID={requestedSopInstanceUID} ({listOfInstanceUIDs.Count} referenced instances)");
                if (!ok)
                    fileLogger.Error($"[MPPS][N-SET] Returning a failure status to device {Device} for COMPLETED SOPInstanceUID={requestedSopInstanceUID}");

                return new DicomNSetResponse(request, ok ? DicomStatus.Success : DicomStatus.ProcessingFailure);
            }
            else if (status == "DISCONTINUED")
            {
                // some vendors send a reason code or description with the mpps-discontinued message
                // var reason = request.Dataset.Get(DicomTag.PerformedProcedureStepDiscontinuationReasonCodeSequence);
                var ok = MppsSource.SetDiscontinued(requestedSopInstanceUID, string.Empty);
                fileLogger.Information($"[MPPS][N-SET] DISCONTINUED result={ok} for SOPInstanceUID={requestedSopInstanceUID}");
                if (!ok)
                    fileLogger.Error($"[MPPS][N-SET] Returning a failure status to device {Device} for DISCONTINUED SOPInstanceUID={requestedSopInstanceUID}");

                return new DicomNSetResponse(request, ok ? DicomStatus.Success : DicomStatus.ProcessingFailure);
            }
            else
            {
                fileLogger.Warning($"[MPPS][N-SET] Rejected status '{status}' for SOPInstanceUID={requestedSopInstanceUID} from device {Device} - only COMPLETED or DISCONTINUED are accepted");
                return new DicomNSetResponse(request, DicomStatus.InvalidAttributeValue);
            }
        }


        #region not supported methods but that are required because of the interface

        public async Task<DicomNDeleteResponse> OnNDeleteRequestAsync(DicomNDeleteRequest request)
        {
            //Logger.Log(LogLevel.Info, "receiving N-Delete, not supported");
            fileLogger.Warning($"receiving N-Delete, not supported - from device {Device}");
            return new DicomNDeleteResponse(request, DicomStatus.UnrecognizedOperation);
        }

        public async Task<DicomNEventReportResponse> OnNEventReportRequestAsync(DicomNEventReportRequest request)
        {
            //Logger.Log(LogLevel.Info, "receiving N-Event, not supported");
            fileLogger.Warning($"receiving N-Event, not supported - from device {Device}");
            return new DicomNEventReportResponse(request, DicomStatus.UnrecognizedOperation);
        }

        public async Task<DicomNGetResponse> OnNGetRequestAsync(DicomNGetRequest request)
        {
            //Logger.Log(LogLevel.Info, "receiving N-Get, not supported");
            fileLogger.Warning($"receiving N-Get, not supported - from device {Device}");
            return new DicomNGetResponse(request, DicomStatus.UnrecognizedOperation);
        }

        public async Task<DicomNActionResponse> OnNActionRequestAsync(DicomNActionRequest request)
        {
            //Logger.Log(LogLevel.Info, "receiving N-Action, not supported");
            fileLogger.Warning($"receiving N-Action, not supported - from device {Device}");
            return new DicomNActionResponse(request, DicomStatus.UnrecognizedOperation);
        }

        #endregion

    }
}
