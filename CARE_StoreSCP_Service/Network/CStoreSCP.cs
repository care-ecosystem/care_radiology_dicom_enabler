using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FellowOakDicom;
using FellowOakDicom.Log;
using FellowOakDicom.Network;
using Plexus.Common.config;
using Plexus.Common.Database;
using Plexus_MWL_Service.logs;
using Serilog;


namespace Plexus_StoreSCP_Service.Network
{
    /// <summary>
    /// Store SCP
    /// </summary>
    class CStoreSCP : DicomService, IDicomServiceProvider, IDicomCStoreProvider, IDicomCEchoProvider

    {
        public static Serilog.ILogger _fileLogger = null;
        EventLog _eventLog = new EventLog();
        public static string _calledAETitle = string.Empty;
        public static ucls_DAL objDAL = null;
        // Accepted Transfer Sysntx
        private static readonly DicomTransferSyntax[] _acceptedTransferSyntaxes = new DicomTransferSyntax[]
           {
               DicomTransferSyntax.ExplicitVRLittleEndian,
               DicomTransferSyntax.ExplicitVRBigEndian,
               DicomTransferSyntax.ImplicitVRLittleEndian
           };

        /// <summary>
        /// 
        /// </summary>
        private static readonly DicomTransferSyntax[] _acceptedImageTransferSyntaxes = new DicomTransferSyntax[]
            {
               // Lossless
               DicomTransferSyntax.JPEGLSLossless,
               DicomTransferSyntax.JPEG2000Lossless,
               DicomTransferSyntax.JPEGProcess14SV1,
               DicomTransferSyntax.JPEGProcess14,
               DicomTransferSyntax.RLELossless,
               // Lossy
               DicomTransferSyntax.JPEGLSNearLossless,
               DicomTransferSyntax.JPEG2000Lossy,
               DicomTransferSyntax.JPEGProcess1,
               DicomTransferSyntax.JPEGProcess2_4,
               // Uncompressed
               DicomTransferSyntax.ExplicitVRLittleEndian,
               DicomTransferSyntax.ExplicitVRBigEndian,
               DicomTransferSyntax.ImplicitVRLittleEndian
            };

        public CStoreSCP(INetworkStream stream, Encoding fallbackEncoding, FellowOakDicom.Log.ILogger log, DicomServiceDependencies dependencies)
                : base(stream, fallbackEncoding, log, dependencies)
        {
            _fileLogger = GetFileLogger();
            if (objDAL == null )
            {
                objDAL = new ucls_DAL(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
            }
            RemoteDeviceInfo.GetRemoteEndpoint(stream, out _remoteHost, out _remotePort);
            _fileLogger.Debug($"Connection opened from IP: {_remoteHost} and port {_remotePort}");
        }

        // The connecting device's IP and port, known before the association is requested
        private readonly string _remoteHost;
        private readonly int _remotePort;

        // The connecting device, for error logs: AE title once the association is requested, IP and port always
        private string Device => RemoteDeviceInfo.DescribeConnection(Association, _remoteHost, _remotePort);

        private readonly Dictionary<string, int> _receivedByStudy = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly object _receivedLock = new object();

        /// <summary>Writes one line per device and order received on this association.</summary>
        private void WriteReceivedStudies()
        {
            lock (_receivedLock)
            {
                foreach (var study in _receivedByStudy)
                    _fileLogger?.Information($"{study.Key} - Received {study.Value} image(s)");
                _receivedByStudy.Clear();
            }
        }

        private string _deviceModality;

        /// <summary>
        /// "AE=CT01 Modality=CT": the device as the Release log names it. The modality is the image's when
        /// it has one, otherwise the one saved in care_devices for the AE title, or "unknown".
        /// </summary>
        private string DeviceSummary(string aeTitle, string datasetModality = null)
        {
            string modality = string.IsNullOrWhiteSpace(datasetModality) ? GetDeviceModality(aeTitle) : datasetModality.Trim();
            return $"AE={aeTitle} Modality={(string.IsNullOrEmpty(modality) ? "unknown" : modality)}";
        }

        private string GetDeviceModality(string aeTitle)
        {
            if (_deviceModality != null)
                return _deviceModality;
            string errorString = string.Empty;
            try
            {
                _deviceModality = objDAL.GetDeviceModality(aeTitle, ref errorString);
                if (!string.IsNullOrEmpty(errorString))
                    _fileLogger.Debug($"Modality of AE {aeTitle} not read from care_devices: {errorString}");
            }
            catch (Exception ex)
            {
                _fileLogger.Debug($"Modality of AE {aeTitle} not read from care_devices: {ex.Message}");
                _deviceModality = string.Empty;
            }
            return _deviceModality;
        }

        /// <summary>
        /// The operation an association is for, named in a reject line: C-STORE when it proposes a storage
        /// SOP class, otherwise C-ECHO (a device checking the connection).
        /// </summary>
        private static string OperationName(DicomAssociation association)
        {
            if (association.PresentationContexts.Any(pc => pc.AbstractSyntax.StorageCategory != DicomStorageCategory.None))
                return "C-STORE";
            if (association.PresentationContexts.Any(pc => pc.AbstractSyntax == DicomUID.Verification))
                return "C-ECHO";
            return "ASSOC";
        }


        
        /// <summary>
        /// Get FIle Loger to Write to file
        /// </summary>
        /// <returns></returns>
        private Serilog.ILogger GetFileLogger()
        {
            //WriteToLog(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),true);
            return DailyFolderSink.CreateLogger("StoreSCP.txt");
        }
       
        /// <summary>
        /// On Recieve Associat Request
        /// </summary>
        /// <param name="association"></param>
        /// <returns></returns>
        public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
        {
            _fileLogger.Debug($"Received Association request from AE {association.CallingAE} with IP: {association.RemoteHost}");
            _fileLogger.Debug($"Sending device: {RemoteDeviceInfo.Describe(association)}");
            if (Global._aeTitle != association.CalledAE)
            {
                _fileLogger.Error($"[{OperationName(association)}] {DeviceSummary(association.CallingAE)} - Failed: association rejected, the device calls AE title " +
                    $"\"{association.CalledAE}\" but this Store SCP is \"{Global._aeTitle}\". Set the Store SCP AE title on the device.");
                return SendAssociationRejectAsync(
                    DicomRejectResult.Permanent,
                    DicomRejectSource.ServiceUser,
                    DicomRejectReason.CalledAENotRecognized);
            }

            if (!validateServer(association.CallingAE, association.RemoteHost, out string rejectReason))
            {
                _fileLogger.Error($"[{OperationName(association)}] {DeviceSummary(association.CallingAE)} - Failed: association rejected, {rejectReason}");
                return SendAssociationRejectAsync(
                    DicomRejectResult.Permanent,
                    DicomRejectSource.ServiceUser,
                    DicomRejectReason.CallingAENotRecognized);
            }

            foreach (var pc in association.PresentationContexts)
            {
                if (pc.AbstractSyntax == DicomUID.Verification)
                {
                    pc.AcceptTransferSyntaxes(_acceptedTransferSyntaxes);
                }
                else if (pc.AbstractSyntax.StorageCategory != DicomStorageCategory.None)
                {
                    pc.AcceptTransferSyntaxes(_acceptedImageTransferSyntaxes);
                }
                if (pc.Result == DicomPresentationContextResult.Accept)
                    continue;
                if (pc.AbstractSyntax == DicomUID.Verification || pc.AbstractSyntax.StorageCategory != DicomStorageCategory.None)
                {
                    _fileLogger.Error($"[{OperationName(association)}] {DeviceSummary(association.CallingAE)} - Failed: {pc.AbstractSyntax.Name} not accepted, none of the transfer syntaxes " +
                        $"the device proposed ({string.Join(", ", pc.GetTransferSyntaxes().Select(ts => ts.UID.Name))}) is supported. Enable an uncompressed transfer syntax (Explicit or Implicit VR Little Endian) on the device");
                }
                else
                {
                    // Devices often propose services they do not need (storage commitment, MPPS...); one they do need fails on the device
                    _fileLogger.Debug($"Presentation context rejected: {pc.AbstractSyntax.Name} not supported - proposed by AE {association.CallingAE} with IP: {association.RemoteHost}");
                }
            }

            _fileLogger.Debug($"Sending Association Accept for AE {association.CallingAE} with IP: {association.RemoteHost} and port {association.RemotePort}");
            return SendAssociationAcceptAsync(association);
        }

        /// <summary>
        /// On Receive Association Release Request
        /// </summary>
        /// <returns></returns>

        public Task OnReceiveAssociationReleaseRequestAsync()
        {
            _fileLogger.Debug($"Association Release Request from AE {Association.CallingAE} with IP: {Association.RemoteHost}");
            return SendAssociationReleaseResponseAsync();
        }


        /// <summary>
        /// Checks the device is in the Server List (when checkserver is on). The caller logs the one line
        /// of the rejected operation with failureReason, so nothing is logged here but detail.
        /// </summary>
        private bool validateServer(string aeTitle, string hostAddress, out string failureReason)
        {
            failureReason = null;
            string errorString = string.Empty;
            string applicationPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string retVal = cls_PlexusConfig.ReadDetailsFromXML(applicationPath, @"/configurations/checkserver");
            if (retVal != string.Empty && (Convert.ToBoolean(retVal) == true))
            {
                // Uses the parameters, not Association: this also runs while the association is being negotiated.
                if (!objDAL.validateAETitle(aeTitle, hostAddress, ref errorString))
                {
                    failureReason = errorString == string.Empty
                        ? $"AE {aeTitle} with IP {hostAddress} is not in the Server List"
                        : $"the Server List could not be checked for AE {aeTitle} with IP {hostAddress}: {errorString}";
                    return false;
                }
            }
            else
            {
                _fileLogger.Debug($"Server List check disabled (checkserver) - accepting AE {aeTitle} with IP: {hostAddress}");
            }
            return true;
        }


        /// <summary>
        /// On Recieve Abort
        /// </summary>
        /// <param name="source"></param>
        /// <param name="reason"></param>
        public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
        {
            WriteReceivedStudies();
            _fileLogger?.Error($"[CONN] {DeviceSummary(Association?.CallingAE ?? "unknown")} - Failed: the association was aborted by the {source} (reason: {reason}). " +
                "Check the device's log for why it aborted; it often means a DICOM setting (AE title, port, timeout) does not match");
            _fileLogger?.Debug($"[CONN] Abort from device {Device}");
        }


        public void OnConnectionClosed(Exception exception)
        {
            // The images received before the connection ended, whether it ended normally or not
            WriteReceivedStudies();
            if (exception != null)
            {
                // A network error, a timeout or a malformed message from the device ended the connection
                _fileLogger?.Error($"[CONN] {DeviceSummary(Association?.CallingAE ?? "unknown")} - Failed: the connection ended with an error: {exception.Message}" +
                    (exception.InnerException != null ? $" ({exception.InnerException.Message})" : string.Empty) +
                    ". Check the network between the device and this PC; images received before it are kept");
                _fileLogger?.Debug($"[CONN] Connection error with device {Device}: {exception}");
            }
            else
            {
                _fileLogger?.Debug($"Connection closed with device {Device}");
            }
        }


        /// <summary>
        /// On Store Request 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
        {
            // GetSingleValue throws on a missing or empty tag, and an exception here makes fo-dicom drop the
            // association, so the sender sees "connection closed" instead of a DICOM error status.
            var studyUid = request.Dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty).Trim();
            var instUid = request.SOPInstanceUID.UID;
            string accessionNo = request.Dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty).Trim();

            string summary = $"[C-STORE] {DeviceSummary(Association.CallingAE, request.Dataset.GetSingleValueOrDefault(DicomTag.Modality, string.Empty))} " +
                $"AccessionNumber={(accessionNo == string.Empty ? "(none)" : accessionNo)}";

            _fileLogger.Debug($"C-Store Request received for Study Instance Id : "+studyUid+" and Image Instance ID : " + instUid);
            _fileLogger.Debug($"  - Sending device: {RemoteDeviceInfo.Describe(Association)}");
            _fileLogger.Debug($"  - Device details in the image: {RemoteDeviceInfo.Describe(request.Dataset)}");


            if (!validateServer(Association.CallingAE, Association.RemoteHost, out string rejectReason))
            {
                _fileLogger.Error($"{summary} - Failed: {rejectReason}");
                return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
            }

            if (studyUid == string.Empty)
            {
                _fileLogger.Error($"{summary} - Failed: the image (SOPInstanceUID={instUid}) has no Study Instance UID");
                return new DicomCStoreResponse(request, DicomStatus.StorageDataSetDoesNotMatchSOPClassError);
            }

            var path = Global._storagePath;

            try
            {
                // Inside the try: an invalid SCP folder setting throws here, and must fail only this image
                path = Path.Combine(Path.GetFullPath(path), studyUid);
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                path = Path.Combine(path, instUid) + ".dcm";

                await request.File.SaveAsync(path);
            }
            catch (Exception ex)
            {
                _fileLogger.Error($"{summary} - Failed: saving the image to {path} failed: {ex.Message}");
                return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
            }

            string patientId = request.Dataset.GetSingleValueOrDefault(DicomTag.PatientID, "N/A");
            string dbError = "the saved file was not found";
            bool dbUpdated = File.Exists(path) && ReadDICOMPushDB(path, studyUid, instUid, out dbError);

            if (dbUpdated)
            {
                _fileLogger.Debug($"{summary} - Received SOPInstanceUID={instUid}");
                lock (_receivedLock)
                {
                    _receivedByStudy.TryGetValue(summary, out int received);
                    _receivedByStudy[summary] = received + 1;
                }
            }
            else
                _fileLogger.Warning($"{summary} - Received and saved for upload, but not recorded in the database: {dbError}");
            _fileLogger.Debug($"  - Study UID: {studyUid}");
            _fileLogger.Debug($"  - Instance UID: {instUid}");
            _fileLogger.Debug($"  - Patient ID: {patientId}");
            _fileLogger.Debug($"  - File Path: {path}");
            return new DicomCStoreResponse(request, DicomStatus.Success);
        }


        /// <returns>True when the study was recorded in the database.</returns>
        /// <param name="error">Why it was not, for the C-STORE line; null when it was.</param>
        private bool ReadDICOMPushDB(string filePath, string studyinstanceID, string imageInstanceId, out string error)
        {
            error = null;
            try
            {
                string errorString = string.Empty;
                string patient_id = string.Empty, accession_no = string.Empty, studyinstanceid = string.Empty, seriesinstanceid = string.Empty,
                    seriesno = string.Empty, modality = string.Empty,
                    bodypart = string.Empty, series_desc = string.Empty, institution = string.Empty,
                    stationname = string.Empty, department = string.Empty, sopclassuid = string.Empty;


                // Read DICOM FIle
                DicomDataset dicomDataSet = DicomFile.Open(filePath).Dataset;

                if (dicomDataSet != null)
                {
                    // GetString throws when a tag is absent, and modalities routinely omit the optional ones
                    // (body part, series description, institution, station, department), so read them all
                    // with a default rather than losing the whole database record.
                    patient_id = dicomDataSet.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty);
                    accession_no = dicomDataSet.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty);
                    studyinstanceid = dicomDataSet.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty);
                    seriesinstanceid = dicomDataSet.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, string.Empty);
                    seriesno = dicomDataSet.GetSingleValueOrDefault(DicomTag.SeriesNumber, string.Empty);
                    modality = dicomDataSet.GetSingleValueOrDefault(DicomTag.Modality, string.Empty);
                    bodypart = dicomDataSet.GetSingleValueOrDefault(DicomTag.BodyPartExamined, string.Empty);
                    series_desc = dicomDataSet.GetSingleValueOrDefault(DicomTag.SeriesDescription, string.Empty);
                    institution = dicomDataSet.GetSingleValueOrDefault(DicomTag.InstitutionName, string.Empty);
                    stationname = dicomDataSet.GetSingleValueOrDefault(DicomTag.StationName, string.Empty);
                    department = dicomDataSet.GetSingleValueOrDefault(DicomTag.InstitutionalDepartmentName, string.Empty);
                    sopclassuid = dicomDataSet.GetSingleValueOrDefault(DicomTag.SOPClassUID, string.Empty);
                }
                else
                {
                    _fileLogger.Warning($"[C-STORE] StudyInstanceUID={studyinstanceID} SOPInstanceUID={imageInstanceId} - the saved image could not be read back, so its details are not recorded in the database (the file stays in the SCP folder)");
                }

                objDAL.InsertOrUpdateStudyInfo(patient_id, accession_no, studyinstanceid, seriesinstanceid, seriesno, modality, bodypart, series_desc, institution,
                    stationname, department, imageInstanceId, 2, sopclassuid, ref errorString);

                if (errorString != string.Empty)
                {
                    error = errorString;
                    return false;
                }

                _fileLogger.Debug($" Database Update Successful");
                _fileLogger.Debug($"  - Patient ID: {patient_id}");
                _fileLogger.Debug($"  - Accession No: {accession_no}");
                _fileLogger.Debug($"  - Modality: {modality}");
                _fileLogger.Debug($"  - Series: {seriesinstanceid}");
                return true;
            }
            catch (Exception ex)
            {
                error = "reading the file failed with exception " + ex.Message;
                return false;
            }
        }


        public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
        {
            // fo-dicom sends the error response; this only records which device's image failed
            _fileLogger?.Error($"[C-STORE] {DeviceSummary(Association?.CallingAE ?? "unknown")} - Failed: receiving the image failed: {e?.Message}");
            _fileLogger?.Debug($"Receiving an image from device {Device} failed (temp file {tempFileName})");
            return Task.CompletedTask;
        }


        public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
        {
            _fileLogger.Debug($"Received verification request from device: {RemoteDeviceInfo.Describe(Association)}");
            _fileLogger.Information($"[C-ECHO] {DeviceSummary(Association.CallingAE)} - Success");
            return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
        }


        /// <summary>
        /// Writelog in File and Event Log based on the configuration
        /// </summary>
        /// <param name="logString"></param>
        /// <param name="bInfo"></param>
        public void WriteToLog(string logString, bool bInfo)
        {
            bool writeEventLog = Convert.ToBoolean(ConfigurationManager.AppSettings["eventlog"].ToString());

            if (writeEventLog)
            {
                _eventLog.WriteEntry(logString, bInfo ? EventLogEntryType.Information : EventLogEntryType.Error);
            }
            if (bInfo)
            {
                _fileLogger.Information(logString);
            }
            else
            {
                _fileLogger.Error(logString);
            }
        }
    }
}
