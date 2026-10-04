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
using Plexus_MWL_Service.logs;
using Serilog;

namespace Plexus_SCU_Service
{
    public partial class Plexus_SCU_Service : ServiceBase
    {
        public static Serilog.ILogger fileLogger = null;
        private static readonly HttpClient httpClient = new HttpClient();
        Timer timer = new Timer(TimeSpan.FromHours(24).TotalMilliseconds);
        public ucls_DAL objDAL = null;
        // Used when maxUploadRetries is missing or invalid in care_config and App.config.
        private const int DefaultMaxUploadRetries = 10;
        // Used when scu_poll_interval_seconds is blank or invalid in care_config.
        private const int DefaultPollIntervalSeconds = 5;
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
                // The Configuration tab restarts the service with the changed settings as start parameters
                foreach (string change in args)
                    WriteToLog($"Restarted after a Configuration tab change: {change}", true);
                timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
                int pollIntervalSeconds = GetIntSetting("scu_poll_interval_seconds", null, DefaultPollIntervalSeconds);
                WriteToLog($"Scanning the SCP folder every {pollIntervalSeconds}s", true);
                timer.Interval = pollIntervalSeconds * 1000;
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

                string careBackendURL = (ConfigurationManager.AppSettings["careBackendURL"] ?? string.Empty).TrimEnd('/');
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

                string dcmPushPath = GetFolderSetting("scp_folder", "SCP");
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

                    // Only .dcm / .dicom files are uploaded; any other file is moved straight to the failed folder
                    string extension = Path.GetExtension(dcmfile);
                    if (!extension.Equals(".dcm", StringComparison.OrdinalIgnoreCase) &&
                        !extension.Equals(".dicom", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteToLog($"Not a .dcm or .dicom file - not uploaded: {dcmfile}", false);
                        MoveToFailedSCP(dcmfile, string.Empty, string.Empty, 0, "Not a .dcm or .dicom file");
                        continue;
                    }

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
                // Without a patient_id the file is not uploaded: it stays in SCP and is retried, and
                // after maxUploadRetries it moves to FailedSCP.
                string carePatientId = GetCarePatientId(accessionNumber);
                if (string.IsNullOrWhiteSpace(carePatientId))
                {
                    string notFoundLog = string.IsNullOrWhiteSpace(accessionNumber)
                        ? "No AccessionNumber in the DICOM file - not uploaded"
                        : $"AccessionNumber={accessionNumber} not found in care_worklist after refreshing it from the CARE worklist API - not uploaded";
                    WriteToLog($"{notFoundLog}: {dcmfile}", false);
                    UpdateStudyStatusDB(-10, studyInstanceId, dcmfile);
                    RecordUploadFailure(dcmfile, studyInstanceId, accessionNumber, notFoundLog);
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
                        SaveStudyUploadDB(studyInstanceId, accessionNumber, dcmfile, "SUCCESS", null, out _);

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

                        string failureLog = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {responseBody}";
                        RecordUploadFailure(dcmfile, studyInstanceId, accessionNumber, failureLog);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"Upload exception for {dcmfile}: {ex.Message}", false);
                UpdateStudyStatusDB(-10, studyInstanceId, dcmfile);
                RecordUploadFailure(dcmfile, studyInstanceId, accessionNumber, "Exception: " + ex.Message);
            }
        }

        // Saves the failed attempt to care_study_upload, then moves the file out of SCP once it
        // reaches maxUploadRetries retries.
        private void RecordUploadFailure(string dcmfile, string studyInstanceId, string accessionNumber, string failureLog)
        {
            int maxRetries = GetIntSetting("max_upload_retries", "maxUploadRetries", DefaultMaxUploadRetries);

            SaveStudyUploadDB(studyInstanceId, accessionNumber, dcmfile, "FAILED", failureLog, out int retryCount);

            if (retryCount >= maxRetries)
                MoveToFailedSCP(dcmfile, studyInstanceId, accessionNumber, retryCount, failureLog);
        }

        private int GetIntSetting(string configKey, string appSettingKey, int defaultValue)
        {
            string value = GetConfigSetting(configKey, appSettingKey);
            if (int.TryParse(value, out int parsed) && parsed > 0)
                return parsed;
            if (!string.IsNullOrWhiteSpace(value))
                WriteToLog($"{configKey}='{value}' is not a positive number — using {defaultValue}", false);
            return defaultValue;
        }

        // Reads a setting from care_config (Configuration tab). When it is blank there, or care_config
        // cannot be read, the appSettingKey value from App.config is used (empty when appSettingKey is null).
        private string GetConfigSetting(string configKey, string appSettingKey)
        {
            string fallback = appSettingKey == null ? string.Empty : ConfigurationManager.AppSettings[appSettingKey] ?? string.Empty;
            string errorString = string.Empty;
            string value = objDAL.GetConfigValue(configKey, fallback, ref errorString);
            if (!string.IsNullOrEmpty(errorString))
                WriteToLog($"{errorString} — using {(appSettingKey == null ? "the default" : "App.config " + appSettingKey)}", false);
            return value;
        }

        // A folder from care_config, or defaultFolderName under the install folder when it is blank.
        private string GetFolderSetting(string configKey, string defaultFolderName)
        {
            string folder = GetConfigSetting(configKey, null);
            return string.IsNullOrWhiteSpace(folder)
                ? Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), defaultFolderName)
                : folder;
        }

        private void SaveStudyUploadDB(string studyInstanceId, string accessionNumber, string dcmfile, string status, string log, out int retryCount)
        {
            string errorString = string.Empty;
            retryCount = 0;
            try
            {
                objDAL.SaveStudyUpload(studyInstanceId, accessionNumber, Path.GetFileName(dcmfile), status, log, ref retryCount, ref errorString);
                if (!string.IsNullOrEmpty(errorString))
                    WriteToLog($"care_study_upload update failed for {dcmfile}: {errorString}", false);
            }
            catch (Exception ex)
            {
                WriteToLog($"care_study_upload update exception for {dcmfile}: {ex.Message}", false);
            }
        }

        // Moves a file that hit the upload retry limit, or is not a .dcm / .dicom file, out of SCP to
        // FailedSCP\<dd-MM-yyyy>\ so it is no longer picked up, and appends the details to error.log in that folder.
        private void MoveToFailedSCP(string dcmfile, string studyInstanceId, string accessionNumber, int retryCount, string failureLog)
        {
            try
            {
                string failedFolder = Path.Combine(GetFolderSetting("failed_scp_folder", "FailedSCP"), DateTime.Now.ToString("dd-MM-yyyy"));
                if (!Directory.Exists(failedFolder))
                    Directory.CreateDirectory(failedFolder);

                string fileName = Path.GetFileName(dcmfile);
                string destination = Path.Combine(failedFolder, fileName);
                if (File.Exists(destination))
                    File.Delete(destination);
                File.Move(dcmfile, destination);

                string entry = $"{DateTime.Now:dd-MM-yyyy HH:mm:ss} | File: {fileName} | AccessionNumber: {accessionNumber} | StudyUID: {studyInstanceId} | " +
                               $"Retries: {retryCount} | Response: {failureLog}{Environment.NewLine}";
                File.AppendAllText(Path.Combine(failedFolder, "error.log"), entry);

                WriteToLog($"Moved {dcmfile} to {destination} ({retryCount} retries)", false);
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
                string careBackendURL = (ConfigurationManager.AppSettings["careBackendURL"] ?? string.Empty).TrimEnd('/');
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

        // Fetches the worklist for the Facility ID in the Configuration tab and saves it to care_worklist,
        // the same way the MWL service's periodic refresh does. The modality and from date must match the
        // MWL service's (set them in care_config so both read the same values): the sync marks scheduled
        // rows missing from the response COMPLETED.
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
                    (ConfigurationManager.AppSettings["careBackendURL"] ?? string.Empty).TrimEnd('/'),
                    ConfigurationManager.AppSettings["staticAPIKey"] ?? string.Empty,
                    GetConfigSetting("care_modality", "careModality"),
                    GetConfigSetting("care_from_date", "careFromDate"),
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
            return new LoggerConfiguration()
                .WriteTo.Sink(DailyFolderSink.For("StoreSCU.txt"), Serilog.Events.LogEventLevel.Information)
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
