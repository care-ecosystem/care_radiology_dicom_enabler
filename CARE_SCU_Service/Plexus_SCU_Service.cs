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
using System.Threading.Tasks;
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
        // Used when maxUploadFailures / maxUploadRetries are missing or invalid in App.config.
        private const int DefaultMaxUploadFailures = 3;
        private const int DefaultMaxUploadRetries = 10;
        // The CARE worklist is fetched at most once per upload cycle, however many files in the
        // cycle have an accession number that is not in care_worklist.
        private bool worklistRefreshedThisCycle = false;

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
                worklistRefreshedThisCycle = false;

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
            string accessionNumber = string.Empty;
            try
            {
                WriteToLog($"Preparing upload for: {dcmfile}", true);

                DicomDataset dataset = DicomFile.Open(dcmfile).Dataset;
                studyInstanceId = dataset.GetString(DicomTag.StudyInstanceUID);
                string patientId = dataset.GetString(DicomTag.PatientID);
                accessionNumber = dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty);

                // patient_id is only ever the CARE patient id saved in care_patient, found through the
                // care_worklist row with the file's accession number. The DICOM PatientID is never sent.
                // Without a patient_id the file is not uploaded: it stays in SCP and is retried without
                // counting as a failure, so after maxUploadRetries it moves to FailedSCP\OtherFailure.
                string carePatientId = GetCarePatientId(accessionNumber);
                if (string.IsNullOrWhiteSpace(carePatientId))
                {
                    string notFoundLog = string.IsNullOrWhiteSpace(accessionNumber)
                        ? "No AccessionNumber in the DICOM file - not uploaded"
                        : $"AccessionNumber={accessionNumber} not found in care_worklist after refreshing it from the CARE worklist API - not uploaded";
                    WriteToLog($"{notFoundLog}: {dcmfile}", false);
                    UpdateStudyStatusDB(-10, studyInstanceId, dcmfile);
                    RecordUploadFailure(dcmfile, studyInstanceId, accessionNumber, notFoundLog, false);
                    return;
                }

                string fileName = Path.GetFileName(dcmfile);
                using (var content = new MultipartFormDataContent())
                {
                    content.Add(new StringContent(carePatientId), "patient_id");
                    WriteToLog($"Sending patient_id={carePatientId} from care_worklist for AccessionNumber={accessionNumber}", true);

                    content.Add(new StringContent(fileName), "filename");

                    byte[] fileBytes = File.ReadAllBytes(dcmfile);
                    var fileContent = new ByteArrayContent(fileBytes);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/dicom");
                    content.Add(fileContent, "file", fileName);

                    var request = new HttpRequestMessage(HttpMethod.Post, uploadURL);
                    request.Headers.Add("Authorization", staticApiKey);
                    request.Content = content;

                    WriteToLog($"Uploading to {uploadURL} (patient_id={carePatientId}, DICOM PatientID={patientId}, StudyUID={studyInstanceId}, AccessionNumber={accessionNumber})", true);

                    var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                    string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    if (response.IsSuccessStatusCode)
                    {
                        WriteToLog($"Upload succeeded ({(int)response.StatusCode}) for {dcmfile}", true);
                        UpdateStudyStatusDB(3, studyInstanceId, dcmfile);
                        SaveStudyUploadDB(studyInstanceId, accessionNumber, dcmfile, "SUCCESS", null, false);

                        string studyUid = ParseStudyUidFromResponse(responseBody);
                        WriteToLog($"Preparing to map SR — StudyInstanceUID={studyInstanceId}, PatientID={patientId}, AccessionNumber={accessionNumber}", true);
                        CallStudyWebhook(studyUid, accessionNumber);

                        File.Delete(dcmfile);
                        WriteToLog($"Deleted local file: {dcmfile}", true);
                    }
                    else
                    {
                        WriteToLog($"Upload failed ({(int)response.StatusCode}) for {dcmfile}: {responseBody}", false);
                        UpdateStudyStatusDB(-10, studyInstanceId, dcmfile);

                        // 429 (CARE rate limiting), 401, 403 and any 5xx (server-side) keep retrying without counting as a failure.
                        int statusCode = (int)response.StatusCode;
                        bool countAsFailure = statusCode != 429 && statusCode != 401 && statusCode != 403 && (statusCode < 500 || statusCode > 599);
                        string failureLog = $"HTTP {statusCode} ({response.ReasonPhrase}): {responseBody}";
                        RecordUploadFailure(dcmfile, studyInstanceId, accessionNumber, failureLog, countAsFailure);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"Upload exception for {dcmfile}: {ex.Message}", false);
                UpdateStudyStatusDB(-10, studyInstanceId, dcmfile);

                // A network failure leaves the file in SCP to retry without counting as a failure.
                bool countAsFailure = !IsNetworkError(ex);
                string failureLog = (countAsFailure ? "Exception: " : "Network error: ") + ex.Message;
                RecordUploadFailure(dcmfile, studyInstanceId, accessionNumber, failureLog, countAsFailure);
            }
        }

        // Saves the failed attempt to care_study_upload, then moves the file out of SCP once it
        // reaches maxUploadFailures counted failures or maxUploadRetries retries of any kind.
        private void RecordUploadFailure(string dcmfile, string studyInstanceId, string accessionNumber, string failureLog, bool countAsFailure)
        {
            int maxFailures = GetIntSetting("maxUploadFailures", DefaultMaxUploadFailures);
            int maxRetries = GetIntSetting("maxUploadRetries", DefaultMaxUploadRetries);

            SaveStudyUploadDB(studyInstanceId, accessionNumber, dcmfile, "FAILED", failureLog, countAsFailure, out int failureCount, out int retryCount);

            if (countAsFailure && failureCount >= maxFailures)
                MoveToFailedSCP(dcmfile, "MaxFailures", studyInstanceId, accessionNumber, failureCount, retryCount, failureLog);
            else if (retryCount >= maxRetries)
                MoveToFailedSCP(dcmfile, "MaxRetries", studyInstanceId, accessionNumber, failureCount, retryCount, failureLog);
        }

        private int GetIntSetting(string key, int defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            if (int.TryParse(value, out int parsed) && parsed > 0)
                return parsed;
            if (!string.IsNullOrWhiteSpace(value))
                WriteToLog($"{key}='{value}' in App.config is not a positive number — using {defaultValue}", false);
            return defaultValue;
        }

        // True when the exception (or one it wraps) comes from reaching the CARE server: connection
        // refused, DNS failure, dropped connection or HttpClient timeout.
        private static bool IsNetworkError(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                if (e is HttpRequestException || e is TaskCanceledException ||
                    e is System.Net.WebException || e is System.Net.Sockets.SocketException)
                    return true;
            }
            return false;
        }

        private void SaveStudyUploadDB(string studyInstanceId, string accessionNumber, string dcmfile, string status, string log, bool countAsFailure)
        {
            SaveStudyUploadDB(studyInstanceId, accessionNumber, dcmfile, status, log, countAsFailure, out _, out _);
        }

        private void SaveStudyUploadDB(string studyInstanceId, string accessionNumber, string dcmfile, string status, string log, bool countAsFailure, out int failureCount, out int retryCount)
        {
            string errorString = string.Empty;
            failureCount = 0;
            retryCount = 0;
            try
            {
                objDAL.SaveStudyUpload(studyInstanceId, accessionNumber, Path.GetFileName(dcmfile), status, log, countAsFailure, ref failureCount, ref retryCount, ref errorString);
                if (!string.IsNullOrEmpty(errorString))
                    WriteToLog($"care_study_upload update failed for {dcmfile}: {errorString}", false);
            }
            catch (Exception ex)
            {
                WriteToLog($"care_study_upload update exception for {dcmfile}: {ex.Message}", false);
            }
        }

        // Moves a file that hit an upload limit out of SCP so it is no longer picked up, and appends the
        // details to error.log in the destination folder. reason is "MaxFailures" (moved to
        // FailedSCP\<dd-MM-yyyy>\) or "MaxRetries" (moved to FailedSCP\OtherFailure\<dd-MM-yyyy>\, since
        // those retries come from server-side errors rather than a problem with the DICOM file).
        private void MoveToFailedSCP(string dcmfile, string reason, string studyInstanceId, string accessionNumber, int failureCount, int retryCount, string failureLog)
        {
            try
            {
                string failedRoot = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "FailedSCP");
                if (reason == "MaxRetries")
                    failedRoot = Path.Combine(failedRoot, "OtherFailure");
                string failedFolder = Path.Combine(failedRoot, DateTime.Now.ToString("dd-MM-yyyy"));
                if (!Directory.Exists(failedFolder))
                    Directory.CreateDirectory(failedFolder);

                string fileName = Path.GetFileName(dcmfile);
                string destination = Path.Combine(failedFolder, fileName);
                if (File.Exists(destination))
                    File.Delete(destination);
                File.Move(dcmfile, destination);

                string entry = $"{DateTime.Now:dd-MM-yyyy HH:mm:ss} | Reason: {reason} | File: {fileName} | AccessionNumber: {accessionNumber} | StudyUID: {studyInstanceId} | " +
                               $"Failures: {failureCount} | Retries: {retryCount} | Response: {failureLog}{Environment.NewLine}";
                File.AppendAllText(Path.Combine(failedFolder, "error.log"), entry);

                WriteToLog($"Moved {dcmfile} to {destination} ({reason}: {failureCount} failures, {retryCount} retries)", false);
            }
            catch (Exception ex)
            {
                WriteToLog($"Moving {dcmfile} to FailedSCP failed: {ex.Message}", false);
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

        private void CallStudyWebhook(string studyUid, string accessionNumber)
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
                if (string.IsNullOrWhiteSpace(accessionNumber))
                {
                    WriteToLog("Skipping webhook — accession_number missing from DICOM file (file did not come from MWL flow)", false);
                    return;
                }

                string webhookUrl = careBackendURL + webhookPath;
                string payload = $"{{\"accession_number\":\"{accessionNumber}\",\"study_id\":\"{studyUid}\"}}";

                var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl);
                request.Headers.Add("Authorization", staticApiKey);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                WriteToLog($"Calling webhook: {webhookUrl} (study_id={studyUid}, accession_number={accessionNumber})", true);

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

        // Looks the accession number up in care_worklist. When it is not there, refreshes care_worklist
        // from the CARE worklist API (once per upload cycle) and looks it up again. Returns empty when
        // the file has no accession number or it is still not found.
        private string GetCarePatientId(string accessionNumber)
        {
            if (string.IsNullOrWhiteSpace(accessionNumber))
                return string.Empty;

            string carePatientId = LookupCarePatientId(accessionNumber);
            if (!string.IsNullOrWhiteSpace(carePatientId))
                return carePatientId;

            if (worklistRefreshedThisCycle)
                return string.Empty;

            WriteToLog($"AccessionNumber={accessionNumber} not found in care_worklist — refreshing it from the CARE worklist API", true);
            worklistRefreshedThisCycle = true;
            RefreshCareWorklist();
            return LookupCarePatientId(accessionNumber);
        }

        private string LookupCarePatientId(string accessionNumber)
        {
            string errorString = string.Empty;
            try
            {
                string carePatientId = objDAL.GetCarePatientIdByAccessionNo(accessionNumber, ref errorString);
                if (!string.IsNullOrEmpty(errorString))
                    WriteToLog($"care_worklist lookup failed for AccessionNumber={accessionNumber}: {errorString}", false);
                return carePatientId;
            }
            catch (Exception ex)
            {
                WriteToLog($"care_worklist lookup exception for AccessionNumber={accessionNumber}: {ex.Message}", false);
                return string.Empty;
            }
        }

        // Fetches the worklist for the Facility ID in the Server List and saves it to care_worklist,
        // the same way the MWL service's periodic refresh does. careModality and careFromDate must match
        // CARE_MWL_Service App.config: the sync marks scheduled rows missing from the response COMPLETED.
        private void RefreshCareWorklist()
        {
            string errorString = string.Empty;
            string resolvedFrom = string.Empty;
            try
            {
                string facilityId = objDAL.GetFacilityId(string.Empty, ref resolvedFrom, ref errorString);
                if (!string.IsNullOrEmpty(errorString))
                {
                    WriteToLog($"Not refreshing care_worklist: Facility ID lookup failed: {errorString}", false);
                    return;
                }
                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    WriteToLog($"Not refreshing care_worklist: no Facility ID - {resolvedFrom}", false);
                    return;
                }

                ucls_CareWorklist.RefreshCareWorklist(
                    objDAL,
                    ConfigurationManager.AppSettings["careBackendURL"]?.TrimEnd('/') ?? string.Empty,
                    ConfigurationManager.AppSettings["staticAPIKey"] ?? string.Empty,
                    ConfigurationManager.AppSettings["careModality"] ?? string.Empty,
                    ConfigurationManager.AppSettings["careFromDate"] ?? string.Empty,
                    facilityId,
                    WriteToLog);
            }
            catch (Exception ex)
            {
                WriteToLog($"Refreshing care_worklist failed with exception {ex.Message}", false);
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
