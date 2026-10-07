using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
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
        // Used when upload_retry_delay_minutes is blank or invalid in care_config. A failed file is
        // retried this many minutes after its last attempt, then twice as long after each retry.
        private const int DefaultUploadRetryDelayMinutes = 2;
        // The CARE worklist is fetched at most once per upload cycle, however many files in the
        // cycle have an accession number or CARE patient id that is not in care_worklist.
        private bool worklistRefreshedThisCycle = false;
        private bool worklistRefreshSucceeded = false;
        // Set, with the reason for the log, when CARE could not be reached: a local network issue, an
        // internet issue, or the CARE server itself unreachable or unavailable. While set, a failed file
        // whose retry time comes is retried only after a check finds CARE reachable again; otherwise its
        // last_retry_time is moved to now. New files are still tried once so they get a care_sync_upload
        // row and a last_retry_time.
        private string careOutage = null;
        // Whether CARE was found reachable in this upload cycle, null until checked. Checked at most once per cycle.
        private bool? careReachableThisCycle = null;
        // During a CARE outage, CARE is checked again every this many seconds even when no failed file is
        // due for retry, so the log does not keep showing an outage that has already ended.
        private const int CareOutageCheckIntervalSeconds = 20;
        // When CARE was last found unreachable or checked during the current outage
        private DateTime lastCareOutageCheckTime = DateTime.MinValue;

        private enum UploadState { New, DueForRetry, WaitingForRetry }

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

                string uploadURL = careBackendURL + uploadPath;
                worklistRefreshedThisCycle = false;
                worklistRefreshSucceeded = false;
                careReachableThisCycle = null;
                // Only updates the outage status for the log; failed files are still retried at their retry time
                if (careOutage != null && DateTime.Now >= lastCareOutageCheckTime.AddSeconds(CareOutageCheckIntervalSeconds))
                    IsCareReachableForRetry(careBackendURL);
                int retryDelayMinutes = GetIntSetting("upload_retry_delay_minutes", null, DefaultUploadRetryDelayMinutes);
                int waitingCount = 0;
                bool foundLogged = false;

                foreach (string dcmfile in dcmfiles)
                {
                    if (string.IsNullOrWhiteSpace(dcmfile)) continue;

                    string extension = Path.GetExtension(dcmfile);
                    bool isDicomFile = extension.Equals(".dcm", StringComparison.OrdinalIgnoreCase) ||
                                       extension.Equals(".dicom", StringComparison.OrdinalIgnoreCase);

                    // The file's retry_count when this upload is a retry, null for a new file
                    int? failedRetryCount = null;
                    if (isDicomFile)
                    {
                        UploadState state = GetUploadState(dcmfile, retryDelayMinutes, out int retryCount);
                        if (state != UploadState.New)
                            failedRetryCount = retryCount;

                        // A file that failed before waits for its next retry time
                        if (state == UploadState.WaitingForRetry)
                        {
                            waitingCount++;
                            continue;
                        }

                        // While CARE cannot be reached, a file whose retry time has come is not retried:
                        // its retry is rescheduled instead, so the outage does not use up its retries
                        if (state == UploadState.DueForRetry && !IsCareReachableForRetry(careBackendURL))
                        {
                            RescheduleRetry(dcmfile, $"Not retried: {careOutage}", retryDelayMinutes, retryCount);
                            waitingCount++;
                            continue;
                        }
                    }

                    // Logged only once a file is actually processed, so a cycle where every file is
                    // waiting for its retry time logs just the "waiting" line below
                    if (!foundLogged)
                    {
                        WriteToLog($"Found {dcmfiles.Length} file(s) to upload from {dcmPushPath}", true);
                        foundLogged = true;
                    }

                    // Only .dcm / .dicom files are uploaded; any other file is moved straight to the failed folder
                    if (!isDicomFile)
                    {
                        WriteToLog($"Not a .dcm or .dicom file - not uploaded: {dcmfile}", false);
                        MoveToFailedSCP(dcmfile, string.Empty, string.Empty, 0, "Not a .dcm or .dicom file");
                        continue;
                    }

                    UploadDicomFileViaHttp(dcmfile, uploadURL, staticAPIKey, retryDelayMinutes, failedRetryCount);
                }

                if (waitingCount > 0)
                {
                    if (careOutage != null)
                        WriteToLog($"{careOutage} - {waitingCount} file(s) waiting to be uploaded/retried once CARE is reachable", false);
                    else
                        WriteToLog($"{waitingCount} file(s) waiting for their next retry time", true);
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

        // failedRetryCount is the file's retry_count when this upload is a retry, null for a new file
        private void UploadDicomFileViaHttp(string dcmfile, string uploadURL, string staticApiKey, int retryDelayMinutes, int? failedRetryCount)
        {
            string studyInstanceId = string.Empty;
            string accessionNumber = string.Empty;
            try
            {
                WriteToLog($"Preparing upload for: {dcmfile}", true);

                DicomDataset dataset = DicomFile.Open(dcmfile).Dataset;
                studyInstanceId = dataset.GetString(DicomTag.StudyInstanceUID);
                string patientId = dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty);
                accessionNumber = dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty);

                if (string.IsNullOrWhiteSpace(accessionNumber))
                {
                    FailWithoutRetry(dcmfile, studyInstanceId, accessionNumber, "No AccessionNumber in the DICOM file - not uploaded");
                    return;
                }

                // patient_id is the CARE patient id saved in care_patient, found through the care_worklist
                // row with the file's accession number. When the accession number or its patient is not in
                // care_worklist, care_worklist is refreshed from the CARE worklist API and looked up again.
                string carePatientId = LookupCarePatientId(accessionNumber);
                bool inWorklist = !string.IsNullOrWhiteSpace(carePatientId) || IsAccessionNoInCareWorklist(accessionNumber);
                if (string.IsNullOrWhiteSpace(carePatientId))
                {
                    WriteToLog(inWorklist
                        ? $"AccessionNumber={accessionNumber} has no CARE patient id in care_worklist - refreshing it from the CARE worklist API"
                        : $"AccessionNumber={accessionNumber} not found in care_worklist - refreshing it from the CARE worklist API", true);
                    if (!RefreshCareWorklistOncePerCycle())
                    {
                        // The worklist could not be fetched, so it is not known whether the accession number is in it
                        string refreshFailureLog = $"AccessionNumber={accessionNumber} or its CARE patient id is not in care_worklist and the CARE worklist API could not be reached to refresh it - not uploaded";
                        if (careReachableThisCycle == false)
                            RecordOutageFailure(dcmfile, studyInstanceId, accessionNumber, refreshFailureLog, retryDelayMinutes, failedRetryCount);
                        else
                            RecordUploadFailure(dcmfile, studyInstanceId, accessionNumber, refreshFailureLog, retryDelayMinutes);
                        return;
                    }
                    carePatientId = LookupCarePatientId(accessionNumber);
                    inWorklist = !string.IsNullOrWhiteSpace(carePatientId) || IsAccessionNoInCareWorklist(accessionNumber);
                }

                if (!inWorklist)
                {
                    FailWithoutRetry(dcmfile, studyInstanceId, accessionNumber,
                        $"AccessionNumber={accessionNumber} not found in care_worklist after refreshing it from the CARE worklist API - not uploaded");
                    return;
                }

                // The accession number is in care_worklist but its CARE patient id is still missing: the
                // DICOM PatientID is sent instead, for this upload only (care_worklist and care_patient are not changed)
                string patientIdNote = string.Empty;
                if (string.IsNullOrWhiteSpace(carePatientId))
                {
                    if (string.IsNullOrWhiteSpace(patientId))
                    {
                        FailWithoutRetry(dcmfile, studyInstanceId, accessionNumber,
                            $"CARE patient id missing for AccessionNumber={accessionNumber} after refreshing care_worklist and no PatientID in the DICOM file - not uploaded");
                        return;
                    }
                    carePatientId = patientId;
                    patientIdNote = $" (CARE patient id missing for AccessionNumber={accessionNumber} after refreshing care_worklist - sent the DICOM PatientID={patientId})";
                    WriteToLog($"CARE patient id missing for AccessionNumber={accessionNumber} after refreshing care_worklist - uploading with the DICOM PatientID={patientId}", false);
                }

                string fileName = Path.GetFileName(dcmfile);
                using (var content = new MultipartFormDataContent())
                {
                    content.Add(new StringContent(carePatientId), "patient_id");
                    WriteToLog($"Sending patient_id={carePatientId} for AccessionNumber={accessionNumber}", true);

                    content.Add(new StringContent(fileName), "filename");

                    byte[] fileBytes = File.ReadAllBytes(dcmfile);
                    var fileContent = new ByteArrayContent(fileBytes);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/dicom");
                    content.Add(fileContent, "file", fileName);

                    var request = new HttpRequestMessage(HttpMethod.Post, uploadURL);
                    request.Headers.Add("Authorization", staticApiKey);
                    request.Content = content;

                    WriteToLog($"Uploading to {uploadURL} (patient_id={carePatientId}, DICOM PatientID={patientId}, StudyUID={studyInstanceId}, AccessionNumber={accessionNumber})", true);

                    HttpResponseMessage response;
                    try
                    {
                        response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                    }
                    catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                    {
                        // CARE could not be reached: find out whether the local network, the internet or the CARE server is down
                        ucls_NetworkCheck.HasNetworkIssue(out string networkCheck);
                        WriteToLog($"Could not connect to CARE - upload of {dcmfile} failed: {ex.Message}. {networkCheck}", false);
                        StartCareOutage(networkCheck);
                        RecordOutageFailure(dcmfile, studyInstanceId, accessionNumber, $"Could not connect to CARE: {ex.Message}. {networkCheck}{patientIdNote}", retryDelayMinutes, failedRetryCount);
                        return;
                    }
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
                        WriteToLog($"Upload failed ({(int)response.StatusCode}) for {dcmfile}: {responseBody}{patientIdNote}", false);

                        string failureLog = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {responseBody}{patientIdNote}";
                        // 502, 503 and 504 come from a proxy in front of CARE while CARE itself is down
                        if (ucls_NetworkCheck.IsCareUnavailableStatus(response.StatusCode))
                        {
                            StartCareOutage($"CARE server unavailable (HTTP {(int)response.StatusCode} {response.ReasonPhrase})");
                            RecordOutageFailure(dcmfile, studyInstanceId, accessionNumber, failureLog, retryDelayMinutes, failedRetryCount);
                        }
                        // 400 and 409 fail the same way on every retry; 401, 403, 429, 500 and any other status are retried
                        else if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Conflict)
                            FailWithoutRetry(dcmfile, studyInstanceId, accessionNumber, failureLog);
                        else
                            RecordUploadFailure(dcmfile, studyInstanceId, accessionNumber, failureLog, retryDelayMinutes);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"Upload exception for {dcmfile}: {ex.Message}", false);
                RecordUploadFailure(dcmfile, studyInstanceId, accessionNumber, "Exception: " + ex.Message, retryDelayMinutes);
            }
        }

        // Saves the failed attempt to care_sync_upload, then moves the file out of SCP once it
        // reaches maxUploadRetries retries. Until then it stays in SCP and is retried at its next retry time.
        private void RecordUploadFailure(string dcmfile, string studyInstanceId, string accessionNumber, string failureLog, int retryDelayMinutes)
        {
            int maxRetries = GetIntSetting("max_upload_retries", "maxUploadRetries", DefaultMaxUploadRetries);

            UpdateStudyStatusDB(-10, studyInstanceId, dcmfile);
            SaveStudyUploadDB(studyInstanceId, accessionNumber, dcmfile, "FAILED", failureLog, out int retryCount);

            if (retryCount >= maxRetries)
                MoveToFailedSCP(dcmfile, studyInstanceId, accessionNumber, retryCount, failureLog);
            else
                WriteToLog($"Keeping {dcmfile} for retry {retryCount + 1} of {maxRetries} after {DateTime.Now.Add(GetRetryDelay(retryDelayMinutes, retryCount)):dd-MM-yyyy HH:mm:ss}", true);
        }

        // Saves the failed attempt to care_sync_upload and moves the file straight to FailedSCP, for
        // failures that a retry cannot fix.
        private void FailWithoutRetry(string dcmfile, string studyInstanceId, string accessionNumber, string failureLog)
        {
            WriteToLog($"{failureLog} - moving {dcmfile} to FailedSCP without retrying", false);
            UpdateStudyStatusDB(-10, studyInstanceId, dcmfile);
            SaveStudyUploadDB(studyInstanceId, accessionNumber, dcmfile, "FAILED", failureLog, out int retryCount);
            MoveToFailedSCP(dcmfile, studyInstanceId, accessionNumber, retryCount, failureLog);
        }

        // A file that has not failed yet is uploaded straight away. A failed one is retried
        // retryDelayMinutes * 2^retry_count after its last attempt (2, 4, 8... minutes by default),
        // so files that failed together are not all retried at the same time.
        private UploadState GetUploadState(string dcmfile, int retryDelayMinutes, out int retryCount)
        {
            string errorString = string.Empty;
            retryCount = 0;
            DateTime? lastRetryTime = null;
            bool hasFailed = objDAL.GetUploadRetryState(Path.GetFileName(dcmfile), ref retryCount, ref lastRetryTime, ref errorString);
            if (!string.IsNullOrEmpty(errorString))
            {
                WriteToLog($"{errorString} - uploading {dcmfile} now", false);
                return UploadState.New;
            }
            if (!hasFailed)
                return UploadState.New;
            if (lastRetryTime == null || DateTime.Now >= lastRetryTime.Value.Add(GetRetryDelay(retryDelayMinutes, retryCount)))
                return UploadState.DueForRetry;
            return UploadState.WaitingForRetry;
        }

        // True when a failed file may be retried now: there is no CARE outage, or a check (at most
        // once per cycle) finds CARE reachable again, which ends the outage.
        private bool IsCareReachableForRetry(string careBackendURL)
        {
            if (careOutage == null)
                return true;
            if (careReachableThisCycle == null)
            {
                careReachableThisCycle = ucls_NetworkCheck.IsCareReachable(careBackendURL, out string description);
                lastCareOutageCheckTime = DateTime.Now;
                if (careReachableThisCycle.Value)
                {
                    WriteToLog($"CARE is reachable again (was: {careOutage}) - retrying failed uploads at their next retry time", true);
                    careOutage = null;
                    // So the MWL service refreshes its worklist now instead of at its next refresh
                    if (!ucls_NetworkCheck.SignalCareReachable(out string signalError))
                        WriteToLog($"Could not tell the MWL service that CARE is reachable again: {signalError}", false);
                }
                else
                    careOutage = description;
            }
            return careReachableThisCycle.Value;
        }

        // Called when CARE could not be reached, with the reason for the log. Logs the start of an
        // outage once; until CARE is reachable again, failed files are rescheduled instead of retried.
        private void StartCareOutage(string description)
        {
            careReachableThisCycle = false;
            lastCareOutageCheckTime = DateTime.Now;
            if (careOutage == null)
                WriteToLog($"{description} - failed uploads will be retried only once CARE is reachable again", false);
            careOutage = description;
        }

        // For a retry not made, or failed, because CARE could not be reached: moves the file's
        // last_retry_time to now and saves the reason to its log, without counting a retry, so an
        // outage never sends it to FailedSCP.
        private void RescheduleRetry(string dcmfile, string reason, int retryDelayMinutes, int retryCount)
        {
            string log = $"{reason} - retry not counted, next retry at {DateTime.Now.Add(GetRetryDelay(retryDelayMinutes, retryCount)):dd-MM-yyyy HH:mm:ss}";
            string errorString = string.Empty;
            objDAL.UpdateUploadRetryTime(Path.GetFileName(dcmfile), log, ref errorString);
            if (!string.IsNullOrEmpty(errorString))
                WriteToLog($"care_sync_upload update failed for {dcmfile}: {errorString}", false);
            WriteToLog($"{dcmfile}: {log}", false);
        }

        // Records an upload that failed because CARE could not be reached. A new file gets its first
        // care_sync_upload row (retry_count 0) and so a last_retry_time; a retry is only rescheduled.
        private void RecordOutageFailure(string dcmfile, string studyInstanceId, string accessionNumber, string failureLog, int retryDelayMinutes, int? failedRetryCount)
        {
            if (failedRetryCount == null)
                RecordUploadFailure(dcmfile, studyInstanceId, accessionNumber, failureLog, retryDelayMinutes);
            else
                RescheduleRetry(dcmfile, failureLog, retryDelayMinutes, failedRetryCount.Value);
        }

        private static TimeSpan GetRetryDelay(int retryDelayMinutes, int retryCount)
        {
            // Capped so a large retry_count cannot overflow the delay
            return TimeSpan.FromMinutes(retryDelayMinutes * Math.Pow(2, Math.Min(Math.Max(retryCount, 0), 20)));
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
                    WriteToLog($"care_sync_upload update failed for {dcmfile}: {errorString}", false);
            }
            catch (Exception ex)
            {
                WriteToLog($"care_sync_upload update exception for {dcmfile}: {ex.Message}", false);
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

                // A file with the same name already moved there today is kept: this one is saved as name_1, name_2...
                string renamedNote = string.Empty;
                for (int suffix = 1; File.Exists(destination); suffix++)
                    destination = Path.Combine(failedFolder, $"{Path.GetFileNameWithoutExtension(fileName)}_{suffix}{Path.GetExtension(fileName)}");
                if (Path.GetFileName(destination) != fileName)
                {
                    renamedNote = $" | Renamed to: {Path.GetFileName(destination)} (a file named {fileName} is already in {failedFolder})";
                    WriteToLog($"{fileName} is already in {failedFolder} - saving {dcmfile} as {Path.GetFileName(destination)}", false);
                }
                File.Move(dcmfile, destination);

                string entry = $"{DateTime.Now:dd-MM-yyyy HH:mm:ss} | File: {fileName}{renamedNote} | AccessionNumber: {accessionNumber} | StudyUID: {studyInstanceId} | " +
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
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                WriteToLog($"Webhook call could not connect to CARE: {ex.Message}. {ucls_NetworkCheck.Describe()}", false);
            }
            catch (Exception ex)
            {
                WriteToLog($"Webhook call exception: {ex.Message}", false);
            }
        }

        // Refreshes care_worklist from the CARE worklist API at most once per upload cycle. Returns
        // whether that refresh succeeded; later calls in the same cycle reuse its result.
        private bool RefreshCareWorklistOncePerCycle()
        {
            if (!worklistRefreshedThisCycle)
            {
                worklistRefreshedThisCycle = true;
                worklistRefreshSucceeded = RefreshCareWorklist();

                // A refresh that failed because CARE could not be reached starts an outage, like a failed upload
                if (!worklistRefreshSucceeded && careReachableThisCycle != false)
                {
                    string careBackendURL = (ConfigurationManager.AppSettings["careBackendURL"] ?? string.Empty).TrimEnd('/');
                    if (!ucls_NetworkCheck.IsCareReachable(careBackendURL, out string description))
                        StartCareOutage(description);
                }
            }
            return worklistRefreshSucceeded;
        }

        private bool IsAccessionNoInCareWorklist(string accessionNumber)
        {
            string errorString = string.Empty;
            bool found = objDAL.IsAccessionNoInCareWorklist(accessionNumber, ref errorString);
            if (!string.IsNullOrEmpty(errorString))
                WriteToLog($"care_worklist lookup failed for AccessionNumber={accessionNumber}: {errorString}", false);
            return found;
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
        // rows missing from the response COMPLETED. Returns false when care_worklist was not refreshed.
        private bool RefreshCareWorklist()
        {
            string errorString = string.Empty;
            string resolvedFrom = string.Empty;
            try
            {
                string facilityId = objDAL.GetFacilityId(string.Empty, ref resolvedFrom, ref errorString);
                if (!string.IsNullOrEmpty(errorString))
                {
                    WriteToLog($"Not refreshing care_worklist: Facility ID lookup failed: {errorString}", false);
                    return false;
                }
                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    WriteToLog($"Not refreshing care_worklist: no Facility ID - {resolvedFrom}", false);
                    return false;
                }

                return ucls_CareWorklist.RefreshCareWorklist(
                    objDAL,
                    (ConfigurationManager.AppSettings["careBackendURL"] ?? string.Empty).TrimEnd('/'),
                    ConfigurationManager.AppSettings["staticAPIKey"] ?? string.Empty,
                    GetConfigSetting("care_modality", "careModality"),
                    GetConfigSetting("care_from_date", "careFromDate"),
                    facilityId,
                    // The worklist API's progress lines belong to the MWL log; keep only its errors here,
                    // since they explain why an upload is kept for retry
                    (message, isInfo) => { if (!isInfo) WriteToLog(message, false); });
            }
            catch (Exception ex)
            {
                WriteToLog($"Refreshing care_worklist failed with exception {ex.Message}", false);
                return false;
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
