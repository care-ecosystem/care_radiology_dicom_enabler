using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Timers;
using FellowOakDicom;
using Plexus.Common.Database;
using Serilog;

namespace Plexus_SCU_Service
{
    public partial class Plexus_SCU_Service : ServiceBase
    {
        public static Serilog.ILogger fileLogger = null;
        private static readonly HttpClient httpClient = new HttpClient();
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
                if (objDAL == null)
                {
                    string applicationPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                    WriteToLog($"Application Path: {applicationPath}", true);
                    objDAL = new ucls_DAL(applicationPath);
                }
                WriteToLog("Store SCU Service Started Successfully !!!", true);
                timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
                timer.Interval = 5000;
                timer.Enabled = true;
            }
            catch (Exception ex)
            {
                WriteToLog("Store SCU failed with Exception: " + ex.Message, false);
            }
        }

        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            try
            {
                timer.Enabled = false;

                string careBackendURL = ConfigurationManager.AppSettings["careBackendURL"]?.TrimEnd('/') ?? string.Empty;
                string uploadPath = ConfigurationManager.AppSettings["uploadURL"] ?? string.Empty;
                string staticAPIKey = ConfigurationManager.AppSettings["staticAPIKey"] ?? string.Empty;

                if (string.IsNullOrWhiteSpace(careBackendURL))
                {
                    WriteToLog("careBackendURL is not configured in App.config", false);
                    return;
                }
                if (string.IsNullOrWhiteSpace(staticAPIKey))
                {
                    WriteToLog("staticAPIKey is not configured in App.config — cannot upload", false);
                    return;
                }

                //string dcmPushPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "SCP");
                string dcmPushPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "SCP");
                if (!Directory.Exists(dcmPushPath))
                {
                    WriteToLog($"SCP folder not found: {dcmPushPath}", false);
                    return;
                }

                string[] dcmfiles = Directory.GetFiles(dcmPushPath, "*.*", SearchOption.AllDirectories);

                if (dcmfiles.Length <= 0)
                {
                    WriteToLog($"Folder {dcmPushPath} has no files to upload.", true);
                    return;
                }

                WriteToLog($"Found {dcmfiles.Length} file(s) to upload from {dcmPushPath}", true);

                string uploadURL = careBackendURL + uploadPath;

                foreach (string dcmfile in dcmfiles)
                {
                    if (string.IsNullOrWhiteSpace(dcmfile)) continue;
                    UploadDicomFileViaHttp(dcmfile, uploadURL, staticAPIKey);
                }
            }
            catch (Exception ex)
            {
                WriteToLog("Upload cycle failed with error: " + ex.Message, false);
            }
            finally
            {
                timer.Enabled = true;
            }
        }

        private void UploadDicomFileViaHttp(string dcmfile, string uploadURL, string staticApiKey)
        {
            string studyInstanceId = string.Empty;
            try
            {
                WriteToLog($"Preparing upload for: {dcmfile}", true);

                DicomDataset dataset = DicomFile.Open(dcmfile).Dataset;
                studyInstanceId = dataset.GetString(DicomTag.StudyInstanceUID);
                string patientId = dataset.GetString(DicomTag.PatientID);
                string serviceRequestId = dataset.GetSingleValueOrDefault(DicomTag.RequestedProcedureID, string.Empty);

                string fileName = Path.GetFileName(dcmfile);
                using (var content = new MultipartFormDataContent())
                {
                    if (!string.IsNullOrWhiteSpace(patientId) && Guid.TryParse(patientId, out _))
                        content.Add(new StringContent(patientId), "patient_id");
                    else if (!string.IsNullOrWhiteSpace(patientId))
                        WriteToLog($"PatientID '{patientId}' is not a UUID — skipping patient_id field", false);

                    content.Add(new StringContent(fileName), "filename");

                    byte[] fileBytes = File.ReadAllBytes(dcmfile);
                    var fileContent = new ByteArrayContent(fileBytes);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/dicom");
                    content.Add(fileContent, "file", fileName);

                    var request = new HttpRequestMessage(HttpMethod.Post, uploadURL);
                    request.Headers.Add("Authorization", staticApiKey);
                    request.Content = content;

                    WriteToLog($"Uploading to {uploadURL} (PatientID={patientId}, StudyUID={studyInstanceId}, ServiceRequestID={serviceRequestId})", true);

                    var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                    string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    if (response.IsSuccessStatusCode)
                    {
                        WriteToLog($"Upload succeeded ({(int)response.StatusCode}) for {dcmfile}", true);
                        UpdateStudyStatusDB(3, studyInstanceId, dcmfile);

                        string studyUid = ParseStudyUidFromResponse(responseBody);
                        CallStudyWebhook(studyUid, serviceRequestId);

                        File.Delete(dcmfile);
                        WriteToLog($"Deleted local file: {dcmfile}", true);
                    }
                    else
                    {
                        WriteToLog($"Upload failed ({(int)response.StatusCode}) for {dcmfile}: {responseBody}", false);
                        UpdateStudyStatusDB(-10, studyInstanceId, dcmfile);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"Upload exception for {dcmfile}: {ex.Message}", false);
                UpdateStudyStatusDB(-10, studyInstanceId, dcmfile);
            }
        }

        private string ParseStudyUidFromResponse(string responseBody)
        {
            try
            {
                using (var doc = JsonDocument.Parse(responseBody))
                {
                    if (doc.RootElement.TryGetProperty("study_uid", out var prop))
                        return prop.GetString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"Failed to parse study_uid from upload response: {ex.Message}", false);
            }
            return string.Empty;
        }

        private void CallStudyWebhook(string studyUid, string serviceRequestId)
        {
            try
            {
                string careBackendURL = ConfigurationManager.AppSettings["careBackendURL"]?.TrimEnd('/') ?? string.Empty;
                string webhookPath = ConfigurationManager.AppSettings["webhookURL"] ?? string.Empty;
                string staticApiKey = ConfigurationManager.AppSettings["staticAPIKey"] ?? string.Empty;

                if (string.IsNullOrWhiteSpace(studyUid))
                {
                    WriteToLog("Skipping webhook — study_uid missing from upload response", false);
                    return;
                }
                if (!Guid.TryParse(serviceRequestId, out _))
                {
                    WriteToLog($"Skipping webhook — service_request_id '{serviceRequestId}' is not a valid UUID (file did not come from MWL flow)", false);
                    return;
                }

                string webhookUrl = careBackendURL + webhookPath;
                string payload = $"{{\"service_request_id\":\"{serviceRequestId}\",\"study_id\":\"{studyUid}\"}}";

                var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl);
                request.Headers.Add("Authorization", staticApiKey);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                WriteToLog($"Calling webhook: {webhookUrl} (study_id={studyUid}, service_request_id={serviceRequestId})", true);

                var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode)
                    WriteToLog($"Webhook succeeded ({(int)response.StatusCode}): {responseBody}", true);
                else
                    WriteToLog($"Webhook failed ({(int)response.StatusCode}): {responseBody}", false);
            }
            catch (Exception ex)
            {
                WriteToLog($"Webhook call exception: {ex.Message}", false);
            }
        }

        private void UpdateStudyStatusDB(int studyStatus, string studyInstanceId, string dcmfile)
        {
            string errorString = string.Empty;
            try
            {
                objDAL.UpdateStudyStatus(studyInstanceId, studyStatus, ref errorString);
                if (!string.IsNullOrEmpty(errorString))
                    WriteToLog($"DB update failed for {dcmfile}: {errorString}", false);
                else
                    WriteToLog($"DB update succeeded for {dcmfile}", true);
            }
            catch (Exception ex)
            {
                WriteToLog($"DB update exception for {dcmfile}: {ex.Message}", false);
            }
        }

        private Serilog.ILogger GetFileLogger()
        {
            string logFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "logs/StoreSCU.txt");
            return new LoggerConfiguration()
                .WriteTo.File(logFilePath,
                    shared: true,
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
                    rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: false,
                    fileSizeLimitBytes: 10240000)
                .CreateLogger();
        }

        protected override void OnStop()
        {
        }

        public void WriteToLog(string logString, bool bInfo)
        {
            bool writeEventLog = Convert.ToBoolean(ConfigurationManager.AppSettings["eventlog"]?.ToString() ?? "false");
            if (writeEventLog)
                EventLog.WriteEntry(logString, bInfo ? EventLogEntryType.Information : EventLogEntryType.Error);

            if (bInfo)
                fileLogger.Information(logString);
            else
                fileLogger.Error(logString);
        }
    }
}
