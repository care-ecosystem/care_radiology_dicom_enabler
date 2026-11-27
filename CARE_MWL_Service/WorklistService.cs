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

        private string _cFindQueryKeys = string.Empty;
        private string QueryKeys => $" Query keys: {_cFindQueryKeys}";

        // A device that goes silent while it is its turn (e.g. it never got a reply) would keep the
        // association open until the service stops: it is aborted after DeviceIdleTimeout instead
        private static readonly TimeSpan DeviceIdleTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan IdleCheckInterval = TimeSpan.FromSeconds(5);
        private readonly object _idleLock = new object();
        private System.Threading.Timer _idleTimer;
        private bool _waitingForDevice;
        private DateTime _waitingSinceUtc;
        private string _waitingAfter;
        private int _requestsInProgress;
        private bool _connectionClosed;
        // Set when a C-ECHO is answered, cleared once the reply has been written to the network
        private volatile bool _echoReplyPending;

        private string _echoModality;

        private string _cFindFailure;
        private string _cFindNoItems;
        private readonly List<string> _itemsNotSent = new List<string>();

        private static readonly ucls_ReadWriteLog ThrottledLog = new ucls_ReadWriteLog();


        public WorklistService(INetworkStream stream, Encoding fallbackEncoding, FellowOakDicom.Log.ILogger log, DicomServiceDependencies dependencies)
            : base(stream, fallbackEncoding, log, dependencies)
        {
            fileLogger = GetFileLogger();
            RemoteDeviceInfo.GetRemoteEndpoint(stream, out _remoteHost, out _remotePort);
            fileLogger.Debug($"[CONN] Connection opened from IP={_remoteHost} Port={_remotePort}");
            WaitForDevice("the connection was opened (no association request yet)");
            _idleTimer = new System.Threading.Timer(state => CheckDeviceIdle(), null, IdleCheckInterval, IdleCheckInterval);
        }

        /// <summary>It is now the device's turn: it should send its next request or release the association.</summary>
        private void WaitForDevice(string after)
        {
            lock (_idleLock)
            {
                _waitingForDevice = true;
                _waitingSinceUtc = DateTime.UtcNow;
                _waitingAfter = after;
            }
        }

        /// <summary>A request arrived from the device; the service is busy with it until it is answered.</summary>
        private void RequestStarted()
        {
            lock (_idleLock)
            {
                _waitingForDevice = false;
                _requestsInProgress++;
            }
        }

        private void RequestFinished()
        {
            lock (_idleLock)
            {
                if (_requestsInProgress > 0)
                    _requestsInProgress--;
            }
        }

        private void StopIdleTimer()
        {
            System.Threading.Timer timer;
            lock (_idleLock)
            {
                _connectionClosed = true;
                timer = _idleTimer;
                _idleTimer = null;
            }
            timer?.Dispose();
        }

        /// <summary>
        /// Aborts the association when the device has sent nothing for DeviceIdleTimeout while it was its
        /// turn, logging what it was last sent, then closes the connection.
        /// </summary>
        private void CheckDeviceIdle()
        {
            TimeSpan idle;
            string after;
            lock (_idleLock)
            {
                if (_connectionClosed || !_waitingForDevice || _requestsInProgress > 0)
                    return;
                idle = DateTime.UtcNow - _waitingSinceUtc;
                if (idle < DeviceIdleTimeout)
                    return;
                after = _waitingAfter;
            }
            StopIdleTimer();
            fileLogger?.Warning($"[CONN] Device {Device} sent nothing for {idle.TotalSeconds:0}s after {after} - aborting the association and closing the connection. " +
                "If the device reported a failure, it did not receive or did not accept that reply.");
            try
            {
                if (IsConnected)
                    SendAbortAsync(DicomAbortSource.ServiceProvider, DicomAbortReason.NotSpecified).Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                fileLogger?.Warning($"[CONN] Sending the abort to device {Device} failed: {ex.GetBaseException().Message}");
            }
            try
            {
                Dispose();
            }
            catch (Exception ex)
            {
                fileLogger?.Warning($"[CONN] Closing the connection with device {Device} failed: {ex.GetBaseException().Message}");
            }
        }

        /// <summary>Called by fo-dicom once every queued reply has been written to the network.</summary>
        protected override Task OnSendQueueEmptyAsync()
        {
            if (_echoReplyPending)
            {
                _echoReplyPending = false;
                fileLogger?.Information($"[C-ECHO] {Who(Association?.CallingAE, _echoModality)} - Success");
                fileLogger?.Debug($"[C-ECHO] Success reply sent to device {Device} - waiting for it to release the association");
                RequestFinished();
                WaitForDevice("the C-ECHO reply");
            }
            return base.OnSendQueueEmptyAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                StopIdleTimer();
            base.Dispose(disposing);
        }

        private Serilog.ILogger GetFileLogger()
        {
            return DailyFolderSink.CreateLogger("ModalitySCP.txt");
        }

        /// <summary>
        /// "AE=CT01 Modality=CT": the device as the summary lines show it. A C-ECHO carries no modality,
        /// so it is the one CARE has for the device's AE title (care_devices).
        /// </summary>
        private static string Who(string aeTitle, string modality)
        {
            return $"AE={(string.IsNullOrWhiteSpace(aeTitle) ? "unknown" : aeTitle.Trim())} " +
                   $"Modality={(string.IsNullOrWhiteSpace(modality) ? "unknown" : modality.Trim())}";
        }

        /// <summary>
        /// The modality CARE has for the DICOM device with this AE title (care_devices), or empty when no
        /// device has it. Only for the logs, so a failure to read it is Debug detail.
        /// </summary>
        private string DeviceModality(string aeTitle)
        {
            ucls_DAL dal = null;
            try
            {
                string errorString = string.Empty;
                dal = new ucls_DAL(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                string modality = dal.GetDeviceModality(aeTitle, ref errorString);
                if (errorString != string.Empty)
                    fileLogger?.Debug($"[DEVICE] Modality of AE={aeTitle} not read: {errorString}");
                return modality;
            }
            catch (Exception ex)
            {
                fileLogger?.Debug($"[DEVICE] Modality of AE={aeTitle} not read: {ex.Message}");
                return string.Empty;
            }
            finally
            {
                dal?.Dispose();
            }
        }

        /// <summary>
        /// What the device opened the association for, from the services it proposed, so a rejected
        /// association is logged as the C-ECHO or C-FIND that failed.
        /// </summary>
        private static string RequestedOperation(DicomAssociation association)
        {
            var syntaxes = association.PresentationContexts.Select(pc => pc.AbstractSyntax).ToList();
            if (syntaxes.Any(uid => uid == DicomUID.ModalityWorklistInformationModelFind))
                return "C-FIND";
            if (syntaxes.Any(uid => uid == DicomUID.ModalityPerformedProcedureStep))
                return "MPPS";
            if (syntaxes.Any(uid => uid.StorageCategory != DicomStorageCategory.None))
                return "C-STORE";
            if (syntaxes.Any(uid => uid == DicomUID.Verification))
                return "C-ECHO";
            return "ASSOC";
        }

        public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
        {
            fileLogger?.Debug($"[C-ECHO] Request from device: {RemoteDeviceInfo.Describe(Association)}");
            _echoModality = DeviceModality(Association?.CallingAE);
            RequestStarted();
            // OnSendQueueEmptyAsync logs once the reply is written to the network
            _echoReplyPending = true;
            return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
        }


        public async IAsyncEnumerable<DicomCFindResponse> OnCFindRequestAsync(DicomCFindRequest request)
        {

            // The calling AE was checked against the Server List when the association was accepted.
            fileLogger.Debug($"[C-FIND] Worklist requested by device: {RemoteDeviceInfo.Describe(Association)}");
            _cFindQueryKeys = RemoteDeviceInfo.DescribeQuery(request.Dataset);
            fileLogger.Debug($"[C-FIND] Query keys: {_cFindQueryKeys}");
            RequestStarted();
            List<string> accessionNos = new List<string>();
            _cFindFailure = null;
            _cFindNoItems = null;
            _itemsNotSent.Clear();
            string aeTitle = Association.CallingAE;
            string modality = GetRequestedModality(request.Dataset) ?? DeviceModality(aeTitle);

            // An exception here would make fo-dicom drop the association, so the device would only see
            // "connection closed": log it with the device and answer with a DICOM failure status instead
            List<DicomDataset> results;
            try
            {
                results = FindWorklistItems(request);
            }
            catch (Exception ex)
            {
                _cFindFailure = $"fetching the worklist failed with exception {ex.Message} - a C-FIND failure status was returned";
                fileLogger.Debug($"[C-FIND] Fetching the worklist for device {Device} failed: {ex}.{QueryKeys}");
                results = null;
            }
            if (results == null)
            {
                LogCFindSummary(aeTitle, modality, 0);
                yield return new DicomCFindResponse(request, DicomStatus.ProcessingFailure);
                RequestFinished();
                WaitForDevice("the C-FIND failure reply");
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
            LogCFindSummary(aeTitle, modality, returnedItemsCount);
            yield return new DicomCFindResponse(request, DicomStatus.Success);
            RequestFinished();
            WaitForDevice($"the C-FIND replies ({returnedItemsCount} worklist item(s))");
        }



        private void LogCFindSummary(string aeTitle, string modality, int sentCount)
        {
            string who = "[C-FIND] " + Who(aeTitle, modality);
            if (_cFindFailure != null)
            {
                fileLogger.Error($"{who} - Failed: {_cFindFailure}");
            }
            else if (_itemsNotSent.Count > 0)
            {
                fileLogger.Error($"{who} - {sentCount} worklist item(s) sent, {_itemsNotSent.Count} not sent: {_itemsNotSent[0]}" +
                    (_itemsNotSent.Count > 1 ? $" (and {_itemsNotSent.Count - 1} more)" : string.Empty) + ". Correct the value in CARE.");
            }
            else if (sentCount == 0)
            {
                fileLogger.Warning($"{who} - No worklist items: {_cFindNoItems ?? "nothing matches the device's query"}");
            }
            else
            {
                fileLogger.Information($"{who} - Success: {sentCount} worklist item(s) sent");
            }
        }


        private List<DicomDataset> FindWorklistItems(DicomCFindRequest request)
        {
            List<DicomDataset> results = null;

            switch (Convert.ToInt32(ConfigurationManager.AppSettings["backend"] ?? "2"))
            {
                case 0:
                    fileLogger.Debug($"Fetching Records from List");
                    var newWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItems();
                    WorklistServer.CurrentWorklistItems = newWorklistItems;
                    fileLogger.Debug($" Successfully fetched {newWorklistItems?.Count ?? 0} worklist items from List");
                    break;
                case 1:
                    fileLogger.Debug($"Fetching Records from Plexus Database");
                    var dbWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItemsFromDB();
                    WorklistServer.CurrentWorklistItems = dbWorklistItems;
                    fileLogger.Debug($" Successfully fetched {dbWorklistItems?.Count ?? 0} worklist items from Plexus Database");
                    break;
                case 2:
                    string facilityId = getFacilityId(Association.CallingAE, out string facilityProblem);
                    if (string.IsNullOrWhiteSpace(facilityId))
                    {
                        // Facility ID is mandatory: do not call the CARE worklist API without it.
                        _cFindFailure = facilityProblem;
                        WorklistServer.CurrentWorklistItems = new List<WorklistItem>();
                        break;
                    }
                    // The worklist is served from care_worklist. The CARE API is called only when no
                    // row there matches this query, then care_worklist is read again.
                    // The care_worklist read is limited to the modality in the query (0008,0060); a
                    // C-FIND without one gets no worklist. The CARE API refresh fetches every modality.
                    string requestedModality = GetRequestedModality(request.Dataset);
                    if (requestedModality == null)
                    {
                        _cFindFailure = "the device sent no Modality (0008,0060) in its worklist query. Set the modality on the device so it is sent in the worklist query";
                        results = new List<DicomDataset>();
                        break;
                    }
                    string requestedStationAET = GetRequestedScheduledStationAET(request.Dataset);
                    if (requestedStationAET == null)
                    {
                        _cFindFailure = "the device sent no Scheduled Station AE Title (0040,0001) in its worklist query. It is mandatory: set the device to send its AE title as Scheduled Station AE Title";
                        results = new List<DicomDataset>();
                        break;
                    }
                    var itemsSource = CreateItemsSourceService;
                    fileLogger.Debug($"Fetching Records from care_worklist for Facility ID {facilityId}, Modality {requestedModality} (from the C-FIND).{QueryKeys}");
                    var careWorklistItems = itemsSource.GetCareWorklistItemsFromDB(facilityId, requestedModality);
                    bool includeUnmapped = ucls_CareWorklist.IsAllDevices(WorklistServer.GetConfigSetting(ucls_CareWorklist.AllDevicesConfigKey, null));
                    results = WorklistHandler.FilterWorklistItems(request.Dataset, careWorklistItems, LogWorklistItem, deviceRule: true, includeUnmapped: includeUnmapped).ToList();
                    if (results.Count == 0)
                    {
                        fileLogger.Debug($"No care_worklist item matches the C-FIND from AE {Association.CallingAE} ({careWorklistItems.Count} scheduled) - refreshing care_worklist from the CARE worklist API.{QueryKeys}");
                        if (itemsSource.RefreshCareWorklistFromApi(facilityId, out string refreshFailure))
                        {
                            careWorklistItems = itemsSource.GetCareWorklistItemsFromDB(facilityId, requestedModality);
                            _itemsNotSent.Clear();
                            results = WorklistHandler.FilterWorklistItems(request.Dataset, careWorklistItems, LogWorklistItem, deviceRule: true, includeUnmapped: includeUnmapped).ToList();
                        }
                        else
                        {
                            _cFindFailure = "the worklist could not be fetched from CARE: " + refreshFailure;
                        }
                    }
                    WorklistServer.CurrentWorklistItems = careWorklistItems;
                    if (results.Count == 0 && _cFindFailure == null)
                        _cFindNoItems = careWorklistItems.Count == 0
                            ? $"no {requestedModality} order is scheduled in CARE for this DICOM Enabler's devices"
                            : $"{careWorklistItems.Count} {requestedModality} order(s) scheduled in CARE, none for Scheduled Station AE Title {requestedStationAET} or matching the device's query";
                    fileLogger.Debug($" Successfully fetched {careWorklistItems.Count} worklist items from care_worklist, {results.Count} matching the C-FIND.{QueryKeys}");
                    break;

            }

            return results ?? WorklistHandler.FilterWorklistItems(request.Dataset, WorklistServer.CurrentWorklistItems, LogWorklistItem).ToList();
        }


        /// <summary>
        /// What WorklistHandler did not send to the device for this C-FIND: items that did not match, an item
        /// left out because a CARE value is not valid DICOM, values sent empty or replaced, unsupported keys.
        /// </summary>
        private void LogWorklistItem(Serilog.Events.LogEventLevel level, string message)
        {
            if (level == Serilog.Events.LogEventLevel.Error)
                _itemsNotSent.Add(message);
            else if (level == Serilog.Events.LogEventLevel.Warning)
                ThrottledLog.WriteThrottled(level, $"[C-FIND] AE={Association?.CallingAE}: {message}");
            string text = $"[C-FIND] {message} - device {Device}";
            text += (text.EndsWith(".") ? string.Empty : ".") + QueryKeys;
            fileLogger?.Debug(text);
        }


        /// <summary>
        /// The Modality (0008,0060) the device asked for in the Scheduled Procedure Step Sequence, or
        /// null when it sent none, an empty value or a wildcard.
        /// </summary>
        private static string GetRequestedModality(DicomDataset query)
        {
            if (query == null || !query.Contains(DicomTag.ScheduledProcedureStepSequence))
                return null;

            var procedureStep = query.GetSequence(DicomTag.ScheduledProcedureStepSequence).FirstOrDefault();
            if (procedureStep == null || !procedureStep.TryGetString(DicomTag.Modality, out string modality))
                return null;

            modality = modality?.Trim();
            if (string.IsNullOrEmpty(modality) || modality.Contains("*") || modality.Contains("?"))
                return null;
            return modality;
        }

        /// <summary>
        /// The Scheduled Station AE Title (0040,0001) the device asked for in the Scheduled Procedure Step
        /// Sequence, or null when it sent none, an empty value or a wildcard.
        /// </summary>
        private static string GetRequestedScheduledStationAET(DicomDataset query)
        {
            if (query == null || !query.Contains(DicomTag.ScheduledProcedureStepSequence))
                return null;

            var procedureStep = query.GetSequence(DicomTag.ScheduledProcedureStepSequence).FirstOrDefault();
            if (procedureStep == null || !procedureStep.TryGetString(DicomTag.ScheduledStationAETitle, out string aeTitle))
                return null;

            aeTitle = aeTitle?.Trim();
            if (string.IsNullOrEmpty(aeTitle) || aeTitle.Contains("*") || aeTitle.Contains("?"))
                return null;
            return aeTitle;
        }


        private bool validateServer(string aeTitle, string hostAddress, out string reason)
        {
            reason = null;
            string errorString = string.Empty;
            string applicationPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string retVal = cls_PlexusConfig.ReadDetailsFromXML(applicationPath, @"/configurations/checkserver");
            fileLogger?.Debug($"[VALIDATE] checkserver={retVal} AE={aeTitle} IP={hostAddress}");
            if (retVal != string.Empty && Convert.ToBoolean(retVal) == true)
            {
                if (objDal == null)
                {
                    objDal = new ucls_DAL(applicationPath);
                }
                if (!objDal.validateAETitle(aeTitle, hostAddress, ref errorString))
                {
                    reason = errorString == string.Empty
                        ? $"AE title {aeTitle} with IP {hostAddress} is not in the Server List - add the device in the Server List tab"
                        : $"the Server List could not be checked: {errorString}";
                    return false;
                }
                fileLogger?.Debug($"[VALIDATE] AE={aeTitle} validated OK");
            }
            else
            {
                fileLogger?.Debug($"[VALIDATE] checkserver disabled, skipping AE validation");
            }
            return true;
        }

        /// <summary>
        /// Reads the Facility ID from the Configuration tab so the CARE worklist request is scoped to
        /// this enabler's facility. The Facility ID is mandatory: when none is set this returns empty,
        /// and the caller then fetches nothing rather than querying every facility. The error naming
        /// the cause is returned in problem, for the C-FIND summary line.
        /// </summary>
        private string getFacilityId(string aeTitle, out string problem)
        {
            problem = null;
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
                    problem = $"the selected facility could not be read: {errorString}";
                    return string.Empty;
                }
                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    problem = $"{resolvedFrom} - select one and save";
                    return string.Empty;
                }
                fileLogger?.Debug($"[FACILITY] AE={aeTitle} resolved to Facility ID={facilityId} from {resolvedFrom}");
                return facilityId;
            }
            catch (Exception ex)
            {
                problem = $"the selected facility could not be read: {ex.Message}";
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
            fileLogger.Debug($"Application path : {applicationPath}");
            ucls_DAL objDal = new ucls_DAL(applicationPath);
            try
            {
                foreach (string accessionNo in accessionNos)
                {
                    objDal.UpdateStudyStatusByAscNo(accessionNo, 1, ref errorString);

                    if (errorString != string.Empty)
                    {
                        fileLogger.Error($"[C-FIND] AE={Association?.CallingAE} - Failed: marking AccessionNumber={accessionNo} as sent in the database failed: {errorString}");
                        fileLogger.Debug("[C-FIND] Query keys: " + QueryKeys);
                    }
                }
            }
            catch (Exception ex)
            {
                fileLogger.Error($"[C-FIND] AE={Association?.CallingAE} - Failed: marking the worklist items as sent in the database failed with exception {ex.Message}");
                fileLogger.Debug("[C-FIND] Query keys: " + QueryKeys);
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
            StopIdleTimer();
            Clean();
            if (exception != null)
            {
                // A network error, a timeout or a malformed message from the device ended the connection
                fileLogger?.Error($"[CONN] {Who(Association?.CallingAE, DeviceModality(Association?.CallingAE))} - Failed: the connection ended with an error: {exception.Message}" +
                    (exception.InnerException != null ? $" ({exception.InnerException.Message})" : string.Empty) +
                    ". Check the network between the device and this PC, and that no firewall closes the connection");
                fileLogger?.Debug($"[CONN] Connection error with device {Device}: {exception}");
            }
            else
            {
                fileLogger?.Debug($"[CONN] Connection closed with device {Device}");
            }
        }


        public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
        {
            StopIdleTimer();
            //log the abort reason
            //Logger.Error($"Received abort from {source}, reason is {reason}");
            fileLogger.Error($"[CONN] {Who(Association?.CallingAE, DeviceModality(Association?.CallingAE))} - Failed: the association was aborted by the {source} (reason: {reason}). " +
                "Check the device's log for why it aborted; it often means a DICOM setting (AE title, port, timeout) does not match");
            fileLogger.Debug($"[CONN] Abort from device {Device}");
        }


        public Task OnReceiveAssociationReleaseRequestAsync()
        {
            // Released: the device closes the connection next
            StopIdleTimer();
            Clean();
            fileLogger?.Debug($"[ASSOC] Release request from device {Device}");
            return SendAssociationReleaseResponseAsync();
        }


        public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
        {
            fileLogger?.Debug($"[ASSOC] Request from device: {RemoteDeviceInfo.Describe(association)}");
            // A rejected association is logged as the C-ECHO / C-FIND / MPPS it was opened for
            string operation = RequestedOperation(association);

            if (WorklistServer.AETitle != association.CalledAE)
            {
                fileLogger?.Error($"[{operation}] {Who(association.CallingAE, DeviceModality(association.CallingAE))} - Failed: association rejected, the device calls AE title " +
                    $"\"{association.CalledAE}\" but this worklist server's AE title is \"{WorklistServer.AETitle}\". Set the worklist server's AE title on the device");
                return SendAssociationRejectAsync(DicomRejectResult.Permanent, DicomRejectSource.ServiceUser, DicomRejectReason.CalledAENotRecognized);
            }

            // Checked once here so every service on the association - C-ECHO, C-FIND and the MPPS
            // N-CREATE / N-SET that update CARE - is limited to modalities in the Server List.
            if (!validateServer(association.CallingAE, association.RemoteHost, out string rejectReason))
            {
                fileLogger?.Error($"[{operation}] {Who(association.CallingAE, DeviceModality(association.CallingAE))} - Failed: association rejected, {rejectReason}");
                return SendAssociationRejectAsync(DicomRejectResult.Permanent, DicomRejectSource.ServiceUser, DicomRejectReason.CallingAENotRecognized);
            }

            int storageContexts = 0;

            foreach (var pc in association.PresentationContexts)
            {
                if (pc.AbstractSyntax == DicomUID.Verification
                    || pc.AbstractSyntax == DicomUID.ModalityWorklistInformationModelFind
                    || pc.AbstractSyntax == DicomUID.ModalityPerformedProcedureStep
                    || pc.AbstractSyntax == DicomUID.ModalityPerformedProcedureStepNotification)
                {
                    pc.AcceptTransferSyntaxes(_acceptedTransferSyntaxes);
                    if (pc.Result == DicomPresentationContextResult.Accept)
                        fileLogger?.Debug($"[ASSOC] PC accepted: {pc.AbstractSyntax.Name} (ID={pc.ID})");
                    else
                        // The device's request for this service will fail
                        fileLogger?.Error($"[{operation}] {Who(association.CallingAE, DeviceModality(association.CallingAE))} - Failed: {pc.AbstractSyntax.Name} not accepted, none of the transfer syntaxes " +
                            $"the device proposed ({string.Join(", ", pc.GetTransferSyntaxes().Select(ts => ts.UID.Name))}) is supported");
                }
                else if (pc.AbstractSyntax.StorageCategory != DicomStorageCategory.None)
                {
                    // A modality that has this port configured as its image destination is misconfigured
                    // - images belong on the Store SCP port. Accept anyway so the study is not lost: the
                    // instance is written to the SCP folder and CARE_SCU_Service uploads it from there.
                    // Whatever the modality proposes is accepted, because the file is written to disk
                    // exactly as received and its pixel data is never decoded here.
                    pc.AcceptTransferSyntaxes(pc.GetTransferSyntaxes().ToArray());
                    storageContexts++;
                    fileLogger?.Debug($"[ASSOC] Storage PC accepted on worklist port: {pc.AbstractSyntax.Name} (ID={pc.ID}) from AE={association.CallingAE} IP={association.RemoteHost}");
                }
                else
                {
                    // Devices often propose services they do not need; one they do need fails on the device
                    fileLogger?.Debug($"[ASSOC] PC rejected: {pc.AbstractSyntax} not supported - proposed by AE={association.CallingAE} IP={association.RemoteHost}");
                    pc.SetResult(DicomPresentationContextResult.RejectAbstractSyntaxNotSupported);
                }
            }
            if (storageContexts > 0)
                fileLogger?.Warning($"[C-STORE] {Who(association.CallingAE, DeviceModality(association.CallingAE))} - the device offers images on the worklist port; they are accepted, " +
                    "but configure this modality to send images to the Store SCP port");

            fileLogger?.Debug($"[ASSOC] Accepted association from AE={association.CallingAE}");
            WaitForDevice("the association was accepted");
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
            RequestStarted();
            try
            {
                return await ProcessCStoreAsync(request);
            }
            finally
            {
                RequestFinished();
                WaitForDevice("the C-STORE reply");
            }
        }


        private async Task<DicomCStoreResponse> ProcessCStoreAsync(DicomCStoreRequest request)
        {
            // GetSingleValue throws on a missing or empty tag, and an exception here makes fo-dicom drop the
            // association, so the sender sees "connection closed" instead of a DICOM error status.
            string studyUid = request.Dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty).Trim();
            string instUid = request.SOPInstanceUID.UID;

            string imageModality = request.Dataset.GetSingleValueOrDefault(DicomTag.Modality, string.Empty);
            string accessionNumber = request.Dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty);
            string who = $"[MWL][C-STORE] {Who(Association.CallingAE, imageModality)} AccessionNumber={(accessionNumber == string.Empty ? "(none)" : accessionNumber)}";
            fileLogger?.Debug($"[MWL][C-STORE] Image received on the worklist port from AE={Association.CallingAE} IP={Association.RemoteHost} StudyInstanceUID={studyUid} SOPInstanceUID={instUid}");
            fileLogger?.Debug($"[MWL][C-STORE] Sending device: {RemoteDeviceInfo.Describe(Association)}");
            fileLogger?.Debug($"[MWL][C-STORE] Device details in the image: {RemoteDeviceInfo.Describe(request.Dataset)}");

            if (!validateServer(Association.CallingAE, Association.RemoteHost, out string rejectReason))
            {
                fileLogger?.Error($"{who} - Failed: {rejectReason}");
                return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
            }

            if (studyUid == string.Empty)
            {
                fileLogger?.Error($"{who} - Failed: the image has no StudyInstanceUID (SOPInstanceUID={instUid})");
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
                    fileLogger?.Error($"{who} - Failed: the image was not written to {filePath}, so the SCU service has nothing to upload");
                    return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
                }

                fileLogger?.Debug($"{who} - Received on the worklist port; saved for upload by the SCU service");
                fileLogger?.Debug($"[MWL][C-STORE]   - Patient ID: {request.Dataset.GetSingleValueOrDefault(DicomTag.PatientID, "N/A")}");
                fileLogger?.Debug($"[MWL][C-STORE]   - Study UID: {studyUid}");
                fileLogger?.Debug($"[MWL][C-STORE]   - Instance UID: {instUid}");
                fileLogger?.Debug($"[MWL][C-STORE]   - File Path: {filePath}");
            }
            catch (Exception ex)
            {
                fileLogger?.Error($"{who} - Failed: saving the image (StudyInstanceUID={studyUid} SOPInstanceUID={instUid}) to the SCP folder failed with exception {ex.Message}");
                return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
            }

            return new DicomCStoreResponse(request, DicomStatus.Success);
        }


        public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
        {
            // fo-dicom sends the error response; this only records which device's image failed
            fileLogger?.Error($"[C-STORE] {Who(Association?.CallingAE, DeviceModality(Association?.CallingAE))} - Failed: receiving the image failed: {e?.Message}. Send the study again; images belong on the Store SCP port, not the worklist port");
            fileLogger?.Debug($"[C-STORE] Receive failure from device {Device} (temp file {tempFileName}): {e}");
            return Task.CompletedTask;
        }


        public async Task<DicomNCreateResponse> OnNCreateRequestAsync(DicomNCreateRequest request)
        {
            RequestStarted();
            // An exception here would make fo-dicom drop the association, so the device would only see
            // "connection closed": log it with the device and answer with a DICOM failure status instead
            try
            {
                return ProcessNCreate(request);
            }
            catch (Exception ex)
            {
                fileLogger?.Error($"[MPPS] {Who(Association?.CallingAE, DeviceModality(Association?.CallingAE))} - Failed: procedure start (N-CREATE) failed with exception {ex.Message}; a failure status was returned to the device");
                fileLogger?.Debug($"[MPPS] N-CREATE failure from device {Device}: {ex}");
                return new DicomNCreateResponse(request, DicomStatus.ProcessingFailure);
            }
            finally
            {
                RequestFinished();
                WaitForDevice("the N-CREATE reply");
            }
        }


        private DicomNCreateResponse ProcessNCreate(DicomNCreateRequest request)
        {
            if (request.SOPClassUID != DicomUID.ModalityPerformedProcedureStep)
            {
                fileLogger?.Error($"[MPPS] {Who(Association?.CallingAE, DeviceModality(Association?.CallingAE))} - Failed: N-CREATE for {request.SOPClassUID} is not supported (only MPPS)");
                return new DicomNCreateResponse(request, DicomStatus.SOPClassNotSupported);
            }
            // on N-Create the UID is stored in AffectedSopInstanceUID, in N-Set the UID is stored in RequestedSopInstanceUID
            var affectedSopInstanceUID = request.Command.GetSingleValue<string>(DicomTag.AffectedSOPInstanceUID);
            fileLogger.Debug($"[MPPS][N-CREATE] Received from AE={Association.CallingAE} SOPInstanceUID={affectedSopInstanceUID}");
            fileLogger.Debug($"[MPPS][N-CREATE] Sending device: {RemoteDeviceInfo.Describe(Association)}");
            fileLogger.Debug($"[MPPS][N-CREATE] Device details in the MPPS: {RemoteDeviceInfo.Describe(request.Dataset)}");

            // get the procedureStepIds from the request
            var scheduledStepItem = request.Dataset
                .GetSequence(DicomTag.ScheduledStepAttributesSequence)
                .First();
            var procedureStepId = scheduledStepItem.GetSingleValueOrDefault(DicomTag.ScheduledProcedureStepID, string.Empty);
            var accessionNumber = scheduledStepItem.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty);
            var requestedProcedureId = scheduledStepItem.GetSingleValueOrDefault(DicomTag.RequestedProcedureID, string.Empty);
            var studyInstanceUid = scheduledStepItem.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty);
            fileLogger.Debug($"[MPPS][N-CREATE] ScheduledStepAttributesSequence: ProcedureStepID={procedureStepId} AccessionNumber={accessionNumber} RequestedProcedureID={requestedProcedureId} StudyInstanceUID={studyInstanceUid}");
            string who = $"[MPPS] {Who(Association.CallingAE, request.Dataset.GetSingleValueOrDefault(DicomTag.Modality, string.Empty))} AccessionNumber={(accessionNumber == string.Empty ? "(none)" : accessionNumber)}";

            var matchCount = WorklistServer.CurrentWorklistItems.Count(w => w.ProcedureStepID == procedureStepId);
            if (matchCount > 1)
            {
                fileLogger.Warning($"[MPPS][N-CREATE] {matchCount} worklist items share ProcedureStepID={procedureStepId} - the FIRST match will be used, which may be the wrong patient/service request. Verify ProcedureStepID is populated uniquely per item.");
            }
            else if (matchCount == 0)
            {
                fileLogger.Debug($"[MPPS][N-CREATE] No worklist item found with ProcedureStepID={procedureStepId} among {WorklistServer.CurrentWorklistItems.Count} cached items. The worklist may have been refreshed since the C-FIND that returned this item, or AccessionNumber={accessionNumber} should be used instead.");
            }

            var ok = MppsSource.SetInProgress(affectedSopInstanceUID, procedureStepId);
            fileLogger.Debug($"[MPPS][N-CREATE] SetInProgress result={ok} for SOPInstanceUID={affectedSopInstanceUID}");
            if (ok)
                fileLogger.Information($"{who} - Procedure started (IN PROGRESS)");
            else
                fileLogger.Error($"{who} - Failed: procedure start (IN PROGRESS) not recorded - no worklist item has ProcedureStepID={procedureStepId}; a failure status was returned to the device");

            return new DicomNCreateResponse(request, ok ? DicomStatus.Success : DicomStatus.ProcessingFailure);
        }


        public async Task<DicomNSetResponse> OnNSetRequestAsync(DicomNSetRequest request)
        {
            RequestStarted();
            // An exception here would make fo-dicom drop the association, so the device would only see
            // "connection closed": log it with the device and answer with a DICOM failure status instead
            try
            {
                return ProcessNSet(request);
            }
            catch (Exception ex)
            {
                fileLogger?.Error($"[MPPS] {Who(Association?.CallingAE, DeviceModality(Association?.CallingAE))} - Failed: procedure update (N-SET) failed with exception {ex.Message}; a failure status was returned to the device");
                fileLogger?.Debug($"[MPPS] N-SET failure from device {Device}: {ex}");
                return new DicomNSetResponse(request, DicomStatus.ProcessingFailure);
            }
            finally
            {
                RequestFinished();
                WaitForDevice("the N-SET reply");
            }
        }


        private DicomNSetResponse ProcessNSet(DicomNSetRequest request)
        {
            if (request.SOPClassUID != DicomUID.ModalityPerformedProcedureStep)
            {
                fileLogger?.Error($"[MPPS] {Who(Association?.CallingAE, DeviceModality(Association?.CallingAE))} - Failed: N-SET for {request.SOPClassUID} is not supported (only MPPS)");
                return new DicomNSetResponse(request, DicomStatus.SOPClassNotSupported);
            }
            // on N-Create the UID is stored in AffectedSopInstanceUID, in N-Set the UID is stored in RequestedSopInstanceUID
            var requestedSopInstanceUID = request.Command.GetSingleValue<string>(DicomTag.RequestedSOPInstanceUID);
            //Logger.Log(LogLevel.Info, $"receiving N-Set with SOPUID {requestedSopInstanceUID}");.I
            fileLogger.Debug($"receiving N-Set with SOPUID {requestedSopInstanceUID}");
            fileLogger.Debug($"[MPPS][N-SET] Sending device: {RemoteDeviceInfo.Describe(Association)}");

            string who = $"[MPPS] {Who(Association.CallingAE, DeviceModality(Association.CallingAE))} SOPInstanceUID={requestedSopInstanceUID}";

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
                if (ok)
                    fileLogger.Information($"{who} - Procedure COMPLETED ({listOfInstanceUIDs.Count} image(s) referenced)");
                else
                    fileLogger.Error($"{who} - Failed: COMPLETED not recorded - no procedure in progress for this MPPS (its N-CREATE was not received or failed); a failure status was returned to the device");

                return new DicomNSetResponse(request, ok ? DicomStatus.Success : DicomStatus.ProcessingFailure);
            }
            else if (status == "DISCONTINUED")
            {
                // some vendors send a reason code or description with the mpps-discontinued message
                // var reason = request.Dataset.Get(DicomTag.PerformedProcedureStepDiscontinuationReasonCodeSequence);
                var ok = MppsSource.SetDiscontinued(requestedSopInstanceUID, string.Empty);
                if (ok)
                    fileLogger.Information($"{who} - Procedure DISCONTINUED");
                else
                    fileLogger.Error($"{who} - Failed: DISCONTINUED not recorded - no procedure in progress for this MPPS (its N-CREATE was not received or failed); a failure status was returned to the device");

                return new DicomNSetResponse(request, ok ? DicomStatus.Success : DicomStatus.ProcessingFailure);
            }
            else
            {
                fileLogger.Error($"{who} - Failed: status '{status}' is not accepted (only COMPLETED or DISCONTINUED)");
                return new DicomNSetResponse(request, DicomStatus.InvalidAttributeValue);
            }
        }


        #region not supported methods but that are required because of the interface

        public async Task<DicomNDeleteResponse> OnNDeleteRequestAsync(DicomNDeleteRequest request)
        {
            //Logger.Log(LogLevel.Info, "receiving N-Delete, not supported");
            fileLogger.Warning($"[N-DELETE] {Who(Association?.CallingAE, DeviceModality(Association?.CallingAE))} - not supported; the device was told so");
            return new DicomNDeleteResponse(request, DicomStatus.UnrecognizedOperation);
        }

        public async Task<DicomNEventReportResponse> OnNEventReportRequestAsync(DicomNEventReportRequest request)
        {
            //Logger.Log(LogLevel.Info, "receiving N-Event, not supported");
            fileLogger.Warning($"[N-EVENT] {Who(Association?.CallingAE, DeviceModality(Association?.CallingAE))} - not supported; the device was told so");
            return new DicomNEventReportResponse(request, DicomStatus.UnrecognizedOperation);
        }

        public async Task<DicomNGetResponse> OnNGetRequestAsync(DicomNGetRequest request)
        {
            //Logger.Log(LogLevel.Info, "receiving N-Get, not supported");
            fileLogger.Warning($"[N-GET] {Who(Association?.CallingAE, DeviceModality(Association?.CallingAE))} - not supported; the device was told so");
            return new DicomNGetResponse(request, DicomStatus.UnrecognizedOperation);
        }

        public async Task<DicomNActionResponse> OnNActionRequestAsync(DicomNActionRequest request)
        {
            //Logger.Log(LogLevel.Info, "receiving N-Action, not supported");
            fileLogger.Warning($"[N-ACTION] {Who(Association?.CallingAE, DeviceModality(Association?.CallingAE))} - not supported; the device was told so");
            return new DicomNActionResponse(request, DicomStatus.UnrecognizedOperation);
        }

        #endregion

    }
}
