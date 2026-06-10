using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Timers;
using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using Plexus.Common.config;
using Plexus.Common.Database;
using Serilog;

namespace Plexus_SCU_Service
{
    public partial class Plexus_SCU_Service : ServiceBase
    {
        public static Serilog.ILogger fileLogger = null;
        Timer timer = new Timer(TimeSpan.FromHours(24).TotalMilliseconds);
        public ucls_DAL objDAL = null;
        public Plexus_SCU_Service()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                if (fileLogger == null)
                {
                    fileLogger = GetFileLogger();
                }
                if (objDAL == null )
                {

                    string applicationPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                    WriteToLog($"Application Path :  {applicationPath}", true);
                    objDAL = new ucls_DAL(applicationPath);
                }
                WriteToLog("Store SCU Service Started Successfully !!!", true);
                timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
                timer.Interval = 5000; //
                timer.Enabled = true;
            }
            catch (Exception ex)
            {
                WriteToLog("Store SCU failed with Exception :" + ex.Message, false);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            //WriteToFile("Service is recall at " + DateTime.Now);
            try
            {
                timer.Enabled = false;
                string aeTitle = string.Empty, callingaeTitle = string.Empty, hostAddress = string.Empty;
                int port = 0;
                WriteToLog("Try create StoreSCU client", true);
                string dcmPushPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "SCP");
                string[] dcmfiles = Directory.GetFiles(dcmPushPath, "*.*", SearchOption.AllDirectories);

                GetDetailsFromConfig(ref aeTitle, ref callingaeTitle, ref hostAddress, ref port);

                if (dcmfiles.Length <= 0)
                {
                    WriteToLog($"Folder {dcmPushPath} does not contain any files to push. !!",true);
                }
                else
                {
                    WriteToLog($"Try pushing {dcmfiles.Length}  files from {dcmPushPath}", true);
                    WriteToLog($"Creating  Association to AE {aeTitle} with IP: {hostAddress}",true);
                    var client = DicomClientFactory.Create(hostAddress, port, false, callingaeTitle, aeTitle);
                    //fileLogger.Information($" Association Successfull to AE {aeTitle} with IP: {hostAddress}");

                    // Send DIcom FIles
                    foreach (string dcmfile in dcmfiles)
                    {
                        if (string.IsNullOrWhiteSpace(dcmfile))
                        {
                            continue;
                        }
                        DicomSCUFile(client, dcmfile,aeTitle,hostAddress);
                    }
                }
            }
            catch(Exception ex)
            {
                WriteToLog("Push Images to Server failed with error : " + ex.Message, false);
            }
            finally
            {
                timer.Enabled = true;
            }
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        /// <param name="dcmfile"></param>
        private async void DicomSCUFile(IDicomClient client, string dcmfile,string aetitle,string hostAddress)
        {
            string studyInstanceId = string.Empty;
            try
            {
                WriteToLog($" Create C-Store Request to AE {aetitle} with IP: {hostAddress} for file : {dcmfile}",true);
                var request = new DicomCStoreRequest(dcmfile);
                request.OnResponseReceived += (req, response) => ResponseReceived(dcmfile, response.Status);
                await client.AddRequestAsync(request);
                GetStudyDetails(ref studyInstanceId, dcmfile);
                WriteToLog($"C-Store Request Added  for file {dcmfile}",true);
                await client.SendAsync();
                // Read Study Details from DICOM file and uPDate the Status to Database
                UpdateStudyStatusDB(3,studyInstanceId, dcmfile);
                WriteToLog($"C-Store send initiated for file {dcmfile}",true);
            }
            catch (Exception ex)
            {
                //fileLogger.Information($"Sending file {dcmfile} failed with exception : {ex.Message}" );
                WriteToLog($"Sending file {dcmfile} failed with exception : {ex.InnerException.Message}",false);
                UpdateStudyStatusDB(-10, studyInstanceId, dcmfile);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="studyinstanceId"></param>
        /// <param name="dcmfile"></param>
        private void GetStudyDetails(ref string studyinstanceId,string dcmfile)
        {
            try
            {
                DicomDataset dicomDataSet = DicomFile.Open(dcmfile).Dataset;

                if (dicomDataSet != null)
                {
                    studyinstanceId = dicomDataSet.GetString(DicomTag.StudyInstanceUID);
                }
                else
                {
                    WriteToLog($"Getting study details failed for the file {dcmfile}", false);
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"Getting Study details for the file {dcmfile} failed with exception : {ex.InnerException.Message}", false);
            }
        }

        /// <summary>
        /// Read Study Details from DICOM file and uPDate the Status to Database
        /// </summary>
        /// <param name="dcmfile"></param>
        private void UpdateStudyStatusDB(int studySatus,string studyInstanceId,string dcmfile)
        {
            string errorString = string.Empty;
            try
            {
                objDAL.UpdateStudyStatus(studyInstanceId, studySatus, ref errorString);
                if (errorString != string.Empty)
                {
                    WriteToLog($"Updating to Database failed for the file {dcmfile} with exception  : {errorString}",false);
                }
                else
                {
                    WriteToLog($"Updating to Database successfull for the file {dcmfile}", true);
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"Sending file {dcmfile} failed with exception : {ex.InnerException.Message}",false);
            }
        }

        private void ResponseReceived(string dcmFile, DicomStatus status)
        {
            try
            {
                WriteToLog("C-Store Response Received, Status: " + status + "for file : " + dcmFile,true);
                if ( status == DicomStatus.Success)
                {
                    WriteToLog("Success Status returned and delete intiation of the file " + dcmFile, true);
                    File.Delete(dcmFile);
                }
            }
            catch(Exception ex)
            {
                WriteToLog($"Error executing actions on Response Recieved fo file {dcmFile} failed with exception : {ex.InnerException.Message}", false);
            }
        }

        private void GetDetailsFromConfig(ref string aeTitle, ref string callingaeTitle, ref string hostAddress, ref int port)
        {
            try
            {
                string applicationPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                aeTitle = cls_PlexusConfig.ReadDetailsFromXML(applicationPath,@"/configurations/sscuaetitle");
                hostAddress = cls_PlexusConfig.ReadDetailsFromXML(applicationPath, @"/configurations/sscuhost");
                callingaeTitle = cls_PlexusConfig.ReadDetailsFromXML(applicationPath, @"/configurations/callingaetitle");
                port = Convert.ToInt32(cls_PlexusConfig.ReadDetailsFromXML(applicationPath, @"/configurations/sscuport"));
            }
            catch(Exception ex)
            {
                WriteToLog("Error getting details from config with error : " + ex.Message, false);
            }
        }


        /// <summary>
        /// Get FIle Loger to Write to file
        /// </summary>
        /// <returns></returns>
        private Serilog.ILogger GetFileLogger()
        {
            //WriteToLog(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),true);
            string logFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "logs/StoreSCU.txt");
            return new LoggerConfiguration().
                WriteTo.File(logFilePath,
                shared: true,
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: false,
                fileSizeLimitBytes: 10240000)
                .CreateLogger();
        }

        /// <summary>
        /// 
        /// </summary>

        protected override void OnStop()
        {
        }


        // <summary>
        /// Writelog in File and Event Log based on the configuration
        /// </summary>
        /// <param name="logString"></param>
        /// <param name="bInfo"></param>
        public void WriteToLog(string logString, bool bInfo)
        {
            bool writeEventLog = Convert.ToBoolean(ConfigurationManager.AppSettings["eventlog"].ToString());

            if (writeEventLog)
            {
                EventLog.WriteEntry(logString, bInfo ? EventLogEntryType.Information : EventLogEntryType.Error);
            }
            if (bInfo)
            {
                fileLogger.Information(logString);
            }
            else
            {
                fileLogger.Error(logString);
            }
        }
    }
}
