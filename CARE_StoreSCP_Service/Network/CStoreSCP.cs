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
        }


        
        /// <summary>
        /// Get FIle Loger to Write to file
        /// </summary>
        /// <returns></returns>
        private Serilog.ILogger GetFileLogger()
        {
            //WriteToLog(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),true);
            return new LoggerConfiguration()
                .WriteTo.Sink(DailyFolderSink.For("StoreSCP.txt"), Serilog.Events.LogEventLevel.Information)
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
                _fileLogger.Error($"Association Rejected: calling AE {association.CallingAE} with IP: {association.RemoteHost} is not in the Server List");
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
                // Uses the parameters, not Association: this also runs while the association is being negotiated.
                if (!objDAL.validateAETitle(aeTitle, hostAddress, ref errorString))
                {
                    if (errorString == string.Empty)
                    {
                        _fileLogger.Error($"Unable to validate AETitle {aeTitle} with IP: {hostAddress}. AETitle not configured as part of the Server List");
                    }
                    else
                    {
                        _fileLogger.Error($"validating AETitle {aeTitle} with IP: {hostAddress}. failed with exception : " + errorString);
                    }
                    return false;
                }
            }
            else
            {
                _fileLogger.Information($"Server List check disabled (checkserver) - accepting AE {aeTitle} with IP: {hostAddress}");
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
            _fileLogger.Information($"C-Store Request received for Study Instance Id : ");
            var studyUid = request.Dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID).Trim();
            var instUid = request.SOPInstanceUID.UID;

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

            string accessionNo = "N/A";
            string patientId = "N/A";
            bool dbUpdated = false;
            if (File.Exists(path))
            {
                accessionNo = request.Dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, "N/A");
                patientId = request.Dataset.GetSingleValueOrDefault(DicomTag.PatientID, "N/A");
                dbUpdated = ReadDICOMPushDB(path, studyUid, instUid);
            }

            _fileLogger.Information(dbUpdated
                ? $" DICOM Upload Successful: File saved and database updated"
                : $" DICOM file saved, but the database was not updated - see the error above");
            _fileLogger.Information($"  - Study UID: {studyUid}");
            _fileLogger.Information($"  - Instance UID: {instUid}");
            _fileLogger.Information($"  - Accession Number: {accessionNo}");
            _fileLogger.Information($"  - Patient ID: {patientId}");
            _fileLogger.Information($"  - File Path: {path}");
            return new DicomCStoreResponse(request, DicomStatus.Success);
        }


        /// <returns>True when the study was recorded in the database.</returns>
        private bool ReadDICOMPushDB(string filePath, string studyinstanceID, string imageInstanceId)
        {
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
                    _fileLogger.Error($"Error Reading DICOM FIle for {studyinstanceID} and ImageInstanceId {imageInstanceId}");
                }

                objDAL.InsertOrUpdateStudyInfo(patient_id, accession_no, studyinstanceid, seriesinstanceid, seriesno, modality, bodypart, series_desc, institution,
                    stationname, department, imageInstanceId, 2, sopclassuid, ref errorString);

                if (errorString != string.Empty)
                {
                    _fileLogger.Error($"Populate DB Failed for StudyInstanceid {studyinstanceID} and ImageInstanceId {imageInstanceId} with exception : " + errorString);
                    return false;
                }

                _fileLogger.Information($" Database Update Successful");
                _fileLogger.Information($"  - Patient ID: {patient_id}");
                _fileLogger.Information($"  - Accession No: {accession_no}");
                _fileLogger.Information($"  - Modality: {modality}");
                _fileLogger.Information($"  - Series: {seriesinstanceid}");
                return true;
            }
            catch (Exception ex)
            {
                _fileLogger.Error($"Read/Populate DB Failed for StudyInstanceid {studyinstanceID} and ImageInstanceId {imageInstanceId} with exception : " + ex.Message);
                return false;
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
    }
}
