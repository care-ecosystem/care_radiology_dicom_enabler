using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FellowOakDicom;
using FellowOakDicom.Media;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.Log;
using FellowOakDicom.Network;
using Serilog;
using Plexus.Common.Database;
using System.Windows.Forms;
using Plexus.Common.config;

namespace Sample_Store_SCP.Network
{
    /// <summary>
    /// Store SCP
    /// </summary>
    class CStoreSCP : DicomService, IDicomServiceProvider, IDicomCStoreProvider, IDicomCEchoProvider

    {
        public static Serilog.ILogger fileLogger = null;
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

        public CStoreSCP(INetworkStream stream, Encoding fallbackEncoding, FellowOakDicom.Log.ILogger log, ILogManager logManager, INetworkManager network, ITranscoderManager transcoder)
                : base(stream, fallbackEncoding, log, logManager, network, transcoder)
        {
            fileLogger = GetFileLogger();
            objDAL = new ucls_DAL(Path.GetDirectoryName(Application.ExecutablePath));
        }

        private Serilog.ILogger GetFileLogger()
        {
            return new LoggerConfiguration().
                 WriteTo.File("logs/StoreSCP.txt",
                 restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
                 rollingInterval: RollingInterval.Day,
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
            fileLogger.Information($"Received Association request from AE {association.CallingAE} with IP: {association.RemoteHost}");
            if (!validateServer(association.CallingAE, association.RemoteHost))
            {
                fileLogger.Error($"Association Rejected as CalledAE not recognized {association.CallingAE} with IP: {association.RemoteHost}");
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

            fileLogger.Information($"Sending Association Accept for AE {association.CallingAE} with IP: {association.RemoteHost}");
            return SendAssociationAcceptAsync(association);
        }

        /// <summary>
        /// On Receive Association Release Request
        /// </summary>
        /// <returns></returns>

        public Task OnReceiveAssociationReleaseRequestAsync()
        {
            fileLogger.Information($"Association Release Request from AE {Association.CallingAE} with IP: {Association.RemoteHost}");
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
            string applicationPath = Path.GetDirectoryName(Application.ExecutablePath);
            string retVal = cls_PlexusConfig.ReadDetailsFromXML(applicationPath, @"/configurations/checkserver");
            if (retVal != string.Empty && (Convert.ToBoolean(retVal) == true))
            {
                if (!objDAL.validateAETitle(Association.CallingAE, Association.RemoteHost, ref errorString))
                {
                    if (errorString == string.Empty)
                    {
                        fileLogger.Error($"Unable to validate AETitle {Association.CallingAE} with IP: {Association.RemoteHost}. AETitle not configured as part of the Server List");
                    }
                    else
                    {
                        fileLogger.Error($"validating AETitle {Association.CallingAE} with IP: {Association.RemoteHost}. failed with exception : " + errorString);
                    }
                    return false;
                }
            }
            else
            {
                fileLogger.Error($"Configuraion Value to check for server in valid. Please check the Configuration from Server List Tab ");
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
          
            var studyUid = request.Dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID).Trim();
            fileLogger.Information($"C-Store Request received for Study Instance Id : {studyUid}");

            if (!validateServer(Association.CallingAE, Association.RemoteHost))
            {
                return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
            }

            var instUid = request.SOPInstanceUID.UID;

            fileLogger.Information($"C-Store Request received for Study Instance Id : "+studyUid+" and Image Instance ID : " + instUid);

            var path = Path.GetFullPath(Global._storagePath);
            path = Path.Combine(path, studyUid);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            path = Path.Combine(path, instUid) + ".dcm";

            await request.File.SaveAsync(path);

            if ( File.Exists(path))
            {
                ReadDICOMPushDB(path, studyUid, instUid);
            }

            fileLogger.Information($"File Saved Successfully and C-Store Response Sent for ImageInstance ID : " + instUid);
            return new DicomCStoreResponse(request, DicomStatus.Success);
        }


        private void ReadDICOMPushDB(string filePath,string studyinstanceID,string imageInstanceId)
        {
            try
            {
                string errorString = string.Empty;
                string patient_id = string.Empty, accession_no = string.Empty, studyinstanceid = string.Empty, seriesinstanceid = string.Empty, 
                    seriesno = string.Empty, modality = string.Empty,
                    bodypart = string.Empty, series_desc = string.Empty, institution = string.Empty, 
                    stationname = string.Empty,department = string.Empty;


                // Read DICOM FIle
                DicomDataset dicomDataSet =  DicomFile.Open(filePath).Dataset;

                if (dicomDataSet != null)
                {
                    patient_id = dicomDataSet.GetString(DicomTag.PatientID);
                    accession_no = dicomDataSet.GetString(DicomTag.AccessionNumber);
                    studyinstanceid = dicomDataSet.GetString(DicomTag.StudyInstanceUID);
                    seriesinstanceid = dicomDataSet.GetString(DicomTag.SeriesInstanceUID);
                    seriesno = dicomDataSet.GetString(DicomTag.SeriesNumber);
                    modality = dicomDataSet.GetString(DicomTag.Modality);
                    bodypart = dicomDataSet.GetString(DicomTag.BodyPartExamined);
                    series_desc = dicomDataSet.GetString(DicomTag.SeriesDescription);
                    institution = dicomDataSet.GetString(DicomTag.InstitutionName);
                    stationname = dicomDataSet.GetString(DicomTag.StationName);
                    department = dicomDataSet.GetString(DicomTag.InstitutionalDepartmentName);
                }
                else
                {
                    fileLogger.Error($"Error Reading DICOM FIle for {studyinstanceID} and ImageInstanceId {imageInstanceId}");
                }


                objDAL.InsertOrUpdateStudyInfo(patient_id, accession_no, studyinstanceid, seriesinstanceid, seriesno, modality, bodypart, series_desc, institution,
                    stationname, department, imageInstanceId, 2 , ref errorString);

                if (errorString != string.Empty)
                {
                    fileLogger.Error($"Populate DB Failed for StudyInstanceid {studyinstanceID} and ImageInstanceId {imageInstanceId} with exception : " + errorString);
                }


            }
            catch(Exception ex)
            {
                fileLogger.Error($"Read/Populate DB Failed for StudyInstanceid {studyinstanceID} and ImageInstanceId {imageInstanceId} with exception : " + ex.Message);
            }
        }


        public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
        {
            // let library handle logging and error response
            return Task.CompletedTask;
        }


        public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
        {
            fileLogger.Information($"Received verification request from AE {Association.CallingAE} with IP: {Association.RemoteHost}");
            return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
        }
    }
}
