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
        }


        
        /// <summary>
        /// Get FIle Loger to Write to file
        /// </summary>
        /// <returns></returns>
        private Serilog.ILogger GetFileLogger()
        {
            //WriteToLog(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),true);
            string logFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "logs/StoreSCP.txt");
            return new LoggerConfiguration().
                WriteTo.File(logFilePath,
                shared: true,
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
                rollOnFileSizeLimit: false,
                fileSizeLimitBytes: 10240000)
                .CreateLogger();
        }
       
        /// <summary>
        /// On Recieve Associat Request
        /// </summary>
        /// <param name="association"></param>
        /// <returns></returns>
        public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
        {
            _fileLogger.Information($"Received Association request from AE {association.CallingAE} with IP: {association.RemoteHost}");
            if (!validateServer(association.CallingAE, association.RemoteHost))
            {
                _fileLogger.Error($"Association Rejected as CalledAE not recognized {Global._aeTitle} with IP: {association.RemoteHost}");
                return SendAssociationRejectAsync(
                    DicomRejectResult.Permanent,
                    DicomRejectSource.ServiceUser,
                    DicomRejectReason.CalledAENotRecognized);
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
            }

            _fileLogger.Information($"Sending Association Accept for AE {association.CallingAE} with IP: {association.RemoteHost} and port {association.RemotePort}");
            return SendAssociationAcceptAsync(association);
        }

        /// <summary>
        /// On Receive Association Release Request
        /// </summary>
        /// <returns></returns>

        public Task OnReceiveAssociationReleaseRequestAsync()
        {
            _fileLogger.Information($"Association Release Request from AE {Association.CallingAE} with IP: {Association.RemoteHost}");
            return SendAssociationReleaseResponseAsync();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="aeTitle"></param>
        /// <param name="hostAddress"></param>
        /// <returns></returns>
        private bool validateServer(string aeTitle, string hostAddress)
        {
            string errorString = string.Empty;
            string applicationPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            _fileLogger.Information($"Application Path :  " + applicationPath);
            string retVal = cls_PlexusConfig.ReadDetailsFromXML(applicationPath, @"/configurations/checkserver");
            if (retVal != string.Empty && (Convert.ToBoolean(retVal) == true))
            {
                if (!objDAL.validateAETitle(Association.CallingAE, Association.RemoteHost, ref errorString))
                {
                    if (errorString == string.Empty)
                    {
                        _fileLogger.Error($"Unable to validate AETitle {Association.CallingAE} with IP: {Association.RemoteHost}. AETitle not configured as part of the Server List");
                    }
                    else
                    {
                        _fileLogger.Error($"validating AETitle {Association.CallingAE} with IP: {Association.RemoteHost}. failed with exception : " + errorString);
                    }
                    return false;
                }
            }
            else
            {
                _fileLogger.Error($"Configuraion Value to check for server in valid. Please check the Configuration from Server List Tab ");
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
            /* nothing to do here */
        }


        public void OnConnectionClosed(Exception exception)
        {
            /* nothing to do here */
        }

       
        /// <summary>
        /// On Store Request 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
        {
            try
            {
                _fileLogger.Information($"C-Store Request received for Study Instance Id : ");
                //var studyUid = request.Dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID).Trim();
                var studyUid = request.Dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID)?.Trim() ?? string.Empty;
                var instUid = request.SOPInstanceUID.UID;
                var sopClassUid = request.SOPClassUID.UID;
                _fileLogger.Information($"C-Store Request received for Study Instance Id : "+studyUid+" and Image Instance ID : " + instUid);


                if (!validateServer(Association.CallingAE, Association.RemoteHost))
                {
                    return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
                }

            var path = Path.GetFullPath(Global._storagePath);
            path = Path.Combine(path, studyUid);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            path = Path.Combine(path, instUid) + ".dcm";

                await request.File.SaveAsync(path);

                if (File.Exists(path))
                {
                    ReadDICOMPushDB(path, studyUid, instUid, sopClassUid);
                }

                _fileLogger.Information($"File Saved Successfully and C-Store Response Sent for ImageInstance ID : " + instUid);
                return new DicomCStoreResponse(request, DicomStatus.Success);
            }
            catch(Exception ex)
            {
                _fileLogger.Error($"C-Store processing failed: {ex.Message}\n{ex.StackTrace}");
                return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
            }
        }

        private void ReadDICOMPushDB(string filePath, string studyinstanceID, string imageInstanceId, string sopClassUid)
        {
            try
            {
                string errorString = string.Empty;
                string patient_id = string.Empty, accession_no = string.Empty, studyinstanceid = string.Empty, seriesinstanceid = string.Empty,
                    seriesno = string.Empty, modality = string.Empty,
                    bodypart = string.Empty, series_desc = string.Empty, institution = string.Empty,
                    stationname = string.Empty, department = string.Empty;


                // Read DICOM FIle
                DicomDataset dicomDataSet = DicomFile.Open(filePath).Dataset;

                
                if (dicomDataSet != null)
                {
                    patient_id = dicomDataSet.Contains(DicomTag.PatientID) ? dicomDataSet.GetString(DicomTag.PatientID) : string.Empty;
                    accession_no = dicomDataSet.Contains(DicomTag.AccessionNumber) ? dicomDataSet.GetString(DicomTag.AccessionNumber) : string.Empty;
                    studyinstanceid = dicomDataSet.Contains(DicomTag.StudyInstanceUID) ? dicomDataSet.GetString(DicomTag.StudyInstanceUID) : string.Empty;
                    seriesinstanceid = dicomDataSet.Contains(DicomTag.SeriesInstanceUID) ? dicomDataSet.GetString(DicomTag.SeriesInstanceUID) : string.Empty;
                    seriesno = dicomDataSet.Contains(DicomTag.SeriesNumber) ? dicomDataSet.GetString(DicomTag.SeriesNumber) : string.Empty;
                    modality = dicomDataSet.Contains(DicomTag.Modality) ? dicomDataSet.GetString(DicomTag.Modality) : string.Empty;
                    bodypart = dicomDataSet.Contains(DicomTag.BodyPartExamined) ? dicomDataSet.GetString(DicomTag.BodyPartExamined) : string.Empty;
                    series_desc = dicomDataSet.Contains(DicomTag.SeriesDescription) ? dicomDataSet.GetString(DicomTag.SeriesDescription) : string.Empty;
                    institution = dicomDataSet.Contains(DicomTag.InstitutionName) ? dicomDataSet.GetString(DicomTag.InstitutionName) : string.Empty;
                    stationname = dicomDataSet.Contains(DicomTag.StationName) ? dicomDataSet.GetString(DicomTag.StationName) : string.Empty;
                    department = dicomDataSet.Contains(DicomTag.InstitutionalDepartmentName) ? dicomDataSet.GetString(DicomTag.InstitutionalDepartmentName) : string.Empty;
                }
                else
                {
                    _fileLogger.Error($"Error Reading DICOM FIle for {studyinstanceID} and ImageInstanceId {imageInstanceId}");
                }

                objDAL.InsertOrUpdateStudyInfo(patient_id, accession_no, studyinstanceid, seriesinstanceid, seriesno, modality, bodypart, series_desc, institution,
                    stationname, department, imageInstanceId, 2, sopClassUid,ref errorString);

                if (errorString != string.Empty)
                {
                    _fileLogger.Error($"Populate DB Failed for StudyInstanceid {studyinstanceID} and ImageInstanceId {imageInstanceId} with exception : " + errorString);
                }
                else
                {
                    _fileLogger.Information($"Populate DB Successfull for StudyInstanceid {studyinstanceID} and ImageInstanceId {imageInstanceId}");
                }
            }
            catch (Exception ex)
            {
                _fileLogger.Error($"Read/Populate DB Failed for StudyInstanceid {studyinstanceID} and ImageInstanceId {imageInstanceId} with exception : " + ex.Message);
            }
        }


        public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
        {
            // let library handle logging and error response
            return Task.CompletedTask;
        }


        public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
        {
            _fileLogger.Information($"Received verification request from AE {Association.CallingAE} with IP: {Association.RemoteHost}");
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
        public async Task<DicomNCreateResponse> OnNCreateRequestAsync(DicomNCreateRequest request)
        {
            if (request.SOPClassUID != DicomUID.ModalityPerformedProcedureStep)
            {
                return new DicomNCreateResponse(request, DicomStatus.SOPClassNotSupported);
            }

            var affectedSopInstanceUID = request.Command.GetSingleValue<string>(DicomTag.AffectedSOPInstanceUID);
            _fileLogger.Information($"[MPPS] N-CREATE received for SOP Instance UID: {affectedSopInstanceUID}");

            return new DicomNCreateResponse(request, DicomStatus.Success);
        }

        public async Task<DicomNSetResponse> OnNSetRequestAsync(DicomNSetRequest request)
        {
            if (request.SOPClassUID != DicomUID.ModalityPerformedProcedureStep)
            {
                return new DicomNSetResponse(request, DicomStatus.SOPClassNotSupported);
            }

            var requestedSopInstanceUID = request.Command.GetSingleValue<string>(DicomTag.RequestedSOPInstanceUID);
            _fileLogger.Information($"[MPPS] N-SET received for SOP Instance UID: {requestedSopInstanceUID}");

            return new DicomNSetResponse(request, DicomStatus.Success);
        }

        public async Task<DicomNDeleteResponse> OnNDeleteRequestAsync(DicomNDeleteRequest request)
        {
            return new DicomNDeleteResponse(request, DicomStatus.UnrecognizedOperation);
        }

        public async Task<DicomNEventReportResponse> OnNEventReportRequestAsync(DicomNEventReportRequest request)
        {
            return new DicomNEventReportResponse(request, DicomStatus.UnrecognizedOperation);
        }

        public async Task<DicomNGetResponse> OnNGetRequestAsync(DicomNGetRequest request)
        {
            return new DicomNGetResponse(request, DicomStatus.UnrecognizedOperation);
        }

        public async Task<DicomNActionResponse> OnNActionRequestAsync(DicomNActionRequest request)
        {
            return new DicomNActionResponse(request, DicomStatus.UnrecognizedOperation);
        }
    }
}

