using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.ServiceProcess;
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
        // Used when scan_completed_wait_seconds is blank or invalid in care_config and App.config.
        // Scan Completed is sent this long after the last upload of an accession number, once none of
        // its files are left in the SCP folder, since more files of the study may still be on their way.
        private const int DefaultScanCompletedWaitSeconds = 60;
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
        // Accession numbers whose Scan Completed is held back because of failed uploads, so the reason
        // is logged once rather than every cycle
        private readonly HashSet<string> scanCompletedHeldLogged = new HashSet<string>(StringComparer.Ordinal);
        // Whether CARE was found reachable in this upload cycle, null until checked. Checked at most once per cycle.
        private bool? careReachableThisCycle = null;
        // During a CARE outage, CARE is checked again every this many seconds even when no failed file is
        // due for retry, so the log does not keep showing an outage that has already ended.
        private const int CareOutageCheckIntervalSeconds = 20;
        // When CARE was last found unreachable or checked during the current outage
        private DateTime lastCareOutageCheckTime = DateTime.MinValue;

        // A DICOM file that cannot be read is moved to FailedSCP only once it has not changed for this
        // many seconds, so a file the SCP is still writing is not given up on.
        private const int UnreadableFileGraceSeconds = 60;

        private enum UploadState { New, AlreadyUploaded, DueForRetry, WaitingForRetry }

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
                string statusPath = ConfigurationManager.AppSettings["statusURL"] ?? string.Empty;
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

                // The upload and scan status endpoints are per facility: {facility_id} in uploadURL and
                // statusURL is replaced with the Facility ID from the Configuration tab (or App.config careFacilityId)
                string facilityErrorString = string.Empty;
                string facilityResolvedFrom = string.Empty;
                string facilityId = objDAL.GetFacilityId(string.Empty, ref facilityResolvedFrom, ref facilityErrorString);
                if (!string.IsNullOrEmpty(facilityErrorString))
                {
                    WriteToLog($"Not uploading: Facility ID lookup failed: {facilityErrorString}", false);
                    return;
                }
                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    WriteToLog($"Not uploading: no Facility ID - {facilityResolvedFrom}", false);
                    return;
                }

                string escapedFacilityId = Uri.EscapeDataString(facilityId.Trim());
                string uploadURL = careBackendURL + uploadPath.Replace("{facility_id}", escapedFacilityId);
                string statusURL = ucls_CareScanStatus.BuildStatusURL(careBackendURL, statusPath, facilityId);

                careReachableThisCycle = null;
                // Only updates the outage status for the log and the scan status updates; failed files are
                // still retried at their retry time
                if (careOutage != null && DateTime.Now >= lastCareOutageCheckTime.AddSeconds(CareOutageCheckIntervalSeconds))
                    IsCareReachableForRetry(careBackendURL);

                string[] dcmfiles = Directory.GetFiles(dcmPushPath, "*.*", SearchOption.AllDirectories);

                if (dcmfiles.Length <= 0)
                {
                    WriteToLog($"Folder {dcmPushPath} has no files to upload.", true);
                    SendPendingScanStartedStatuses(facilityId.Trim(), statusURL, staticAPIKey);
                    SendScanCompletedStatuses(facilityId.Trim(), statusURL, staticAPIKey, dcmPushPath, dcmfiles, new HashSet<string>());
                    return;
                }

                worklistRefreshedThisCycle = false;
                worklistRefreshSucceeded = false;
                int retryDelayMinutes = GetIntSetting("upload_retry_delay_minutes", null, DefaultUploadRetryDelayMinutes);
                int waitingCount = 0;
                bool foundLogged = false;
                // Accession numbers with a file still in the SCP folder after this cycle (waiting for its
                // retry, or kept for retry): Scan Completed is not sent for them yet
                var pendingAccessionNumbers = new HashSet<string>(StringComparer.Ordinal);
                // A file the SCP may still be writing could belong to any accession number
                bool unreadableFilePending = false;

                foreach (string dcmfile in dcmfiles)
                {
                    if (string.IsNullOrWhiteSpace(dcmfile)) continue;
                    DicomDataset dataset = null;
                    try
                    {
                        string extension = Path.GetExtension(dcmfile);
                        bool isDicomFile = extension.Equals(".dcm", StringComparison.OrdinalIgnoreCase) ||
                                           extension.Equals(".dicom", StringComparison.OrdinalIgnoreCase);

                        // The file's retry_count when this upload is a retry, null for a new file
                        int? failedRetryCount = null;
                        string instanceKey = null;
                        if (isDicomFile)
                        {
                            // The file is matched to its care_sync_upload row by its Study, Series and SOP Instance
                            // UIDs (encrypted as instance_key), not by its file name
                            if (!TryReadInstance(dcmfile, out dataset, out instanceKey, out string readError))
                            {
                                // The SCP may still be writing the file: it is only given up on once it has not changed for a while
                                if (File.GetLastWriteTime(dcmfile) > DateTime.Now.AddSeconds(-UnreadableFileGraceSeconds))
                                {
                                    unreadableFilePending = true;
                                    continue;
                                }
                                WriteToLog($"Could not read the Study, Series and SOP Instance UIDs from {dcmfile}: {readError}", false);
                                MoveToFailedSCP(dcmfile, string.Empty, string.Empty, 0, $"Could not read the Study, Series and SOP Instance UIDs: {readError}");
                                continue;
                            }

                            UploadState state = GetUploadState(dcmfile, instanceKey, retryDelayMinutes, out int retryCount);
                            if (state == UploadState.AlreadyUploaded)
                            {
                                string duplicateLog = "Not uploaded: the same instance was already uploaded to CARE";
                                WriteToLog($"{dcmfile}: {duplicateLog}", false);
                                MoveToFailedSCP(dcmfile, dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty),
                                    dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty), 0, duplicateLog);
                                continue;
                            }
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
                                RescheduleRetry(dcmfile, instanceKey, $"Not retried: {careOutage}", retryDelayMinutes, retryCount);
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

                        UploadDicomFileViaHttp(dcmfile, dataset, instanceKey, uploadURL, statusURL, staticAPIKey, retryDelayMinutes, failedRetryCount);
                    }
                    finally
                    {
                        string fileAccessionNumber = dataset?.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty);
                        if (!string.IsNullOrWhiteSpace(fileAccessionNumber) && File.Exists(dcmfile))
                            pendingAccessionNumbers.Add(fileAccessionNumber);
                    }
                }

                if (waitingCount > 0)
                {
                    if (careOutage != null)
                        WriteToLog($"{careOutage} - {waitingCount} file(s) waiting to be uploaded/retried once CARE is reachable", false);
                    else
                        WriteToLog($"{waitingCount} file(s) waiting for their next retry time", true);
                }

                SendPendingScanStartedStatuses(facilityId.Trim(), statusURL, staticAPIKey);
                if (!unreadableFilePending)
                    SendScanCompletedStatuses(facilityId.Trim(), statusURL, staticAPIKey, dcmPushPath, dcmfiles, pendingAccessionNumbers);
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

        // dataset and instanceKey come from TryReadInstance. failedRetryCount is the file's retry_count
        // when this upload is a retry, null for a new file
        private void UploadDicomFileViaHttp(string dcmfile, DicomDataset dataset, string instanceKey, string uploadURL, string statusURL, string staticApiKey, int retryDelayMinutes, int? failedRetryCount)
        {
            string studyInstanceId = string.Empty;
            string accessionNumber = string.Empty;
            try
            {
                WriteToLog($"Preparing upload for: {dcmfile}", true);

                studyInstanceId = dataset.GetString(DicomTag.StudyInstanceUID);
                string patientId = dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty);
                accessionNumber = dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty);

                if (string.IsNullOrWhiteSpace(accessionNumber))
                {
                    FailWithoutRetry(dcmfile, instanceKey, studyInstanceId, accessionNumber, "No AccessionNumber in the DICOM file - not uploaded");
                    return;
                }

                // CARE links the study to its service request by the file's accession number, and uploads it
                // unlinked when no service request has it. So only files whose accession number is in
                // care_worklist are uploaded; when it is not, care_worklist is refreshed from the CARE worklist
                // API and looked up again.
                if (!IsAccessionNoInCareWorklist(accessionNumber))
                {
                    WriteToLog($"AccessionNumber={accessionNumber} not found in care_worklist - refreshing it from the CARE worklist API", true);
                    if (!RefreshCareWorklistOncePerCycle())
                    {
                        // The worklist could not be fetched, so it is not known whether the accession number is in it
                        string refreshFailureLog = $"AccessionNumber={accessionNumber} is not in care_worklist and the CARE worklist API could not be reached to refresh it - not uploaded";
                        if (careReachableThisCycle == false)
                            RecordOutageFailure(dcmfile, instanceKey, studyInstanceId, accessionNumber, refreshFailureLog, retryDelayMinutes, failedRetryCount);
                        else
                            RecordUploadFailure(dcmfile, instanceKey, studyInstanceId, accessionNumber, refreshFailureLog, retryDelayMinutes);
                        return;
                    }
                    if (!IsAccessionNoInCareWorklist(accessionNumber))
                    {
                        FailWithoutRetry(dcmfile, instanceKey, studyInstanceId, accessionNumber,
                            $"AccessionNumber={accessionNumber} not found in care_worklist after refreshing it from the CARE worklist API - not uploaded");
                        return;
                    }
                }

                SendScanStartedStatus(accessionNumber, statusURL, staticApiKey);

                string fileName = Path.GetFileName(dcmfile);
                using (var content = new MultipartFormDataContent())
                {
                    byte[] fileBytes = File.ReadAllBytes(dcmfile);
                    var fileContent = new ByteArrayContent(fileBytes);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/dicom");
                    content.Add(fileContent, "file", fileName);

                    var request = new HttpRequestMessage(HttpMethod.Post, uploadURL);
                    request.Headers.Add("Authorization", staticApiKey);
                    request.Content = content;

                    WriteToLog($"Uploading to {uploadURL} (DICOM PatientID={patientId}, StudyUID={studyInstanceId}, AccessionNumber={accessionNumber})", true);

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
                        RecordOutageFailure(dcmfile, instanceKey, studyInstanceId, accessionNumber, $"Could not connect to CARE: {ex.Message}. {networkCheck}", retryDelayMinutes, failedRetryCount);
                        return;
                    }
                    string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    if (response.IsSuccessStatusCode)
                    {
                        WriteToLog($"Upload succeeded ({(int)response.StatusCode}) for {dcmfile}", true);
                        UpdateStudyStatusDB(3, studyInstanceId, dcmfile);
                        SaveStudyUploadDB(instanceKey, studyInstanceId, accessionNumber, dcmfile, "SUCCESS", null, out _);

                        // CARE links the study to its service request by the accession number. Without
                        // service_request_id it found none and uploaded the study unlinked, which cannot be
                        // archived through the API
                        string serviceRequestId = ParseServiceRequestIdFromResponse(responseBody);
                        if (string.IsNullOrWhiteSpace(serviceRequestId))
                            WriteToLog($"Uploaded {dcmfile} unlinked: CARE found no service request with AccessionNumber={accessionNumber} for this patient", false);
                        else
                            WriteToLog($"Uploaded study linked to service_request {serviceRequestId} (AccessionNumber={accessionNumber})", true);

                        File.Delete(dcmfile);
                        WriteToLog($"Deleted local file: {dcmfile}", true);
                    }
                    else
                    {
                        WriteToLog($"Upload failed ({(int)response.StatusCode}) for {dcmfile}: {responseBody}", false);

                        string failureLog = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {responseBody}";
                        // 502, 503 and 504 come from a proxy in front of CARE while CARE itself is down
                        if (ucls_NetworkCheck.IsCareUnavailableStatus(response.StatusCode))
                        {
                            StartCareOutage($"CARE server unavailable (HTTP {(int)response.StatusCode} {response.ReasonPhrase})");
                            RecordOutageFailure(dcmfile, instanceKey, studyInstanceId, accessionNumber, failureLog, retryDelayMinutes, failedRetryCount);
                        }
                        // 400 and 409 fail the same way on every retry; 401, 403, 429, 500 and any other status are retried
                        else if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Conflict)
                            FailWithoutRetry(dcmfile, instanceKey, studyInstanceId, accessionNumber, failureLog);
                        else
                            RecordUploadFailure(dcmfile, instanceKey, studyInstanceId, accessionNumber, failureLog, retryDelayMinutes);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"Upload exception for {dcmfile}: {ex.Message}", false);
                RecordUploadFailure(dcmfile, instanceKey, studyInstanceId, accessionNumber, "Exception: " + ex.Message, retryDelayMinutes);
            }
        }

        // Saves the failed attempt to care_sync_upload, then moves the file out of SCP once it
        // reaches maxUploadRetries retries. Until then it stays in SCP and is retried at its next retry time.
        private void RecordUploadFailure(string dcmfile, string instanceKey, string studyInstanceId, string accessionNumber, string failureLog, int retryDelayMinutes)
        {
            int maxRetries = GetIntSetting("max_upload_retries", "maxUploadRetries", DefaultMaxUploadRetries);

            UpdateStudyStatusDB(-10, studyInstanceId, dcmfile);
            SaveStudyUploadDB(instanceKey, studyInstanceId, accessionNumber, dcmfile, "FAILED", failureLog, out int retryCount);

            if (retryCount >= maxRetries)
                MoveToFailedSCP(dcmfile, studyInstanceId, accessionNumber, retryCount, failureLog);
            else
                WriteToLog($"Keeping {dcmfile} for retry {retryCount + 1} of {maxRetries} after {DateTime.Now.Add(GetRetryDelay(retryDelayMinutes, retryCount)):dd-MM-yyyy HH:mm:ss}", true);
        }

        // Saves the failed attempt to care_sync_upload and moves the file straight to FailedSCP, for
        // failures that a retry cannot fix.
        private void FailWithoutRetry(string dcmfile, string instanceKey, string studyInstanceId, string accessionNumber, string failureLog)
        {
            WriteToLog($"{failureLog} - moving {dcmfile} to FailedSCP without retrying", false);
            UpdateStudyStatusDB(-10, studyInstanceId, dcmfile);
            SaveStudyUploadDB(instanceKey, studyInstanceId, accessionNumber, dcmfile, "FAILED", failureLog, out int retryCount);
            MoveToFailedSCP(dcmfile, studyInstanceId, accessionNumber, retryCount, failureLog);
        }

        // Reads the file and builds its care_sync_upload instance_key from its Study, Series and SOP
        // Instance UIDs. Returns false, with the reason, when the file cannot be read or a UID is missing.
        private bool TryReadInstance(string dcmfile, out DicomDataset dataset, out string instanceKey, out string error)
        {
            dataset = null;
            instanceKey = null;
            error = null;
            try
            {
                dataset = DicomFile.Open(dcmfile).Dataset;
                string studyUid = dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty);
                string seriesUid = dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, string.Empty);
                string sopInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, string.Empty);
                if (string.IsNullOrWhiteSpace(studyUid) || string.IsNullOrWhiteSpace(seriesUid) || string.IsNullOrWhiteSpace(sopInstanceUid))
                {
                    error = "StudyInstanceUID, SeriesInstanceUID or SOPInstanceUID is missing";
                    return false;
                }
                instanceKey = ucls_DAL.BuildInstanceKey(studyUid, seriesUid, sopInstanceUid);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // An instance that has not been uploaded yet is uploaded straight away, and one already uploaded
        // is not uploaded again. A failed one is retried retryDelayMinutes * 2^retry_count after its last
        // attempt (2, 4, 8... minutes by default), so files that failed together are not all retried at the same time.
        private UploadState GetUploadState(string dcmfile, string instanceKey, int retryDelayMinutes, out int retryCount)
        {
            string errorString = string.Empty;
            string status = null;
            retryCount = 0;
            DateTime? lastRetryTime = null;
            bool found = objDAL.GetUploadRetryState(instanceKey, ref status, ref retryCount, ref lastRetryTime, ref errorString);
            if (!string.IsNullOrEmpty(errorString))
            {
                WriteToLog($"{errorString} - uploading {dcmfile} now", false);
                return UploadState.New;
            }
            if (!found)
                return UploadState.New;
            if (status == "SUCCESS")
                return UploadState.AlreadyUploaded;
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
        private void RescheduleRetry(string dcmfile, string instanceKey, string reason, int retryDelayMinutes, int retryCount)
        {
            string log = $"{reason} - retry not counted, next retry at {DateTime.Now.Add(GetRetryDelay(retryDelayMinutes, retryCount)):dd-MM-yyyy HH:mm:ss}";
            string errorString = string.Empty;
            objDAL.UpdateUploadRetryTime(instanceKey, log, ref errorString);
            if (!string.IsNullOrEmpty(errorString))
                WriteToLog($"care_sync_upload update failed for {dcmfile}: {errorString}", false);
            WriteToLog($"{dcmfile}: {log}", false);
        }

        // Records an upload that failed because CARE could not be reached. A new file gets its first
        // care_sync_upload row (retry_count 0) and so a last_retry_time; a retry is only rescheduled.
        private void RecordOutageFailure(string dcmfile, string instanceKey, string studyInstanceId, string accessionNumber, string failureLog, int retryDelayMinutes, int? failedRetryCount)
        {
            if (failedRetryCount == null)
                RecordUploadFailure(dcmfile, instanceKey, studyInstanceId, accessionNumber, failureLog, retryDelayMinutes);
            else
                RescheduleRetry(dcmfile, instanceKey, failureLog, retryDelayMinutes, failedRetryCount.Value);
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

        private void SaveStudyUploadDB(string instanceKey, string studyInstanceId, string accessionNumber, string dcmfile, string status, string log, out int retryCount)
        {
            string errorString = string.Empty;
            retryCount = 0;
            try
            {
                objDAL.SaveStudyUpload(instanceKey, studyInstanceId, accessionNumber, Path.GetFileName(dcmfile), status, log, ref retryCount, ref errorString);
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

        // Returns service_request_id from the upload response, or empty when CARE uploaded the study unlinked
        private string ParseServiceRequestIdFromResponse(string responseBody)
        {
            try
            {
                using (var doc = JsonDocument.Parse(responseBody))
                {
                    if (doc.RootElement.TryGetProperty("service_request_id", out var prop) && prop.ValueKind == JsonValueKind.String)
                        return prop.GetString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"Failed to parse service_request_id from upload response: {ex.Message}", false);
            }
            return string.Empty;
        }

        private static string ScanStatusStarted => ucls_CareScanStatus.StatusStarted;
        private static string ScanStatusCompleted => ucls_CareScanStatus.StatusCompleted;

        // Sends Scan Started for the accession number's service request before the first upload of a file
        // with it, unless the MWL service already sent it when the modality sent MPPS IN PROGRESS.
        // Once sent, care_worklist.scan_status is STARTED and it is not sent again. When it cannot
        // be sent (or CARE cannot be reached) the file is still uploaded and scan_status is set to
        // START_PENDING, so it is tried again every cycle by SendPendingScanStartedStatuses even when the
        // file was uploaded and deleted.
        private void SendScanStartedStatus(string accessionNumber, string statusURL, string staticApiKey)
        {
            string errorString = string.Empty;
            string serviceRequestId = null;
            string scanStatus = null;
            objDAL.GetScanState(accessionNumber, ref serviceRequestId, ref scanStatus, ref errorString);
            if (!string.IsNullOrEmpty(errorString))
            {
                WriteToLog($"Not sending {ScanStatusStarted} for AccessionNumber={accessionNumber}: {errorString}", false);
                return;
            }
            if (scanStatus == "STARTED" || scanStatus == "COMPLETED")
                return;
            if (string.IsNullOrWhiteSpace(serviceRequestId))
            {
                WriteToLog($"Not sending {ScanStatusStarted} for AccessionNumber={accessionNumber}: no service request id in care_worklist", false);
                return;
            }

            if (careOutage == null && SendScanStatus(statusURL, staticApiKey, serviceRequestId, accessionNumber, ScanStatusStarted))
                SaveScanStatus(accessionNumber, "STARTED");
            else if (scanStatus != "START_PENDING")
                SaveScanStatus(accessionNumber, "START_PENDING");
        }

        // Sends Scan Started for each of the facility's accession numbers whose Scan Started could not be
        // sent on MPPS IN PROGRESS or before its upload (scan_status START_PENDING). When it cannot be sent it is tried again
        // next cycle, and while CARE cannot be reached it is not tried until it is reachable again.
        private void SendPendingScanStartedStatuses(string facilityId, string statusURL, string staticApiKey)
        {
            if (careOutage != null)
                return;
            try
            {
                string errorString = string.Empty;
                List<ScanInProgress> scans = objDAL.GetScansWithStatus(facilityId, "START_PENDING", ref errorString);
                if (!string.IsNullOrEmpty(errorString))
                {
                    WriteToLog($"Not retrying {ScanStatusStarted}: {errorString}", false);
                    return;
                }

                foreach (ScanInProgress scan in scans)
                {
                    if (string.IsNullOrWhiteSpace(scan.ServiceRequestId))
                    {
                        WriteToLog($"Not sending {ScanStatusStarted} for AccessionNumber={scan.AccessionNumber}: no service request id in care_worklist", false);
                        continue;
                    }

                    if (SendScanStatus(statusURL, staticApiKey, scan.ServiceRequestId, scan.AccessionNumber, ScanStatusStarted))
                        SaveScanStatus(scan.AccessionNumber, "STARTED");
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"Retrying {ScanStatusStarted} failed with exception {ex.Message}", false);
            }
        }

        // Sends Scan Completed for each of the facility's accession numbers whose Scan Started was sent,
        // once none of its files are left in the SCP folder (pendingAccessionNumbers) and none was sent
        // to CARE for scan_completed_wait_seconds; MPPS COMPLETED from the modality does not send it.
        // It is held back while any of its instances is FAILED in care_sync_upload (a file moved
        // to FailedSCP is not a finished upload) or none uploaded; a resent copy of a failed instance that
        // uploads updates the same row to SUCCESS and releases it. When it
        // cannot be sent it is tried again next cycle, and while CARE cannot be reached it is not tried
        // until it is reachable again. pendingAccessionNumbers only covers cycleFiles, the files listed at
        // the start of the cycle, so before the first Scan Completed is sent the SCP folder is checked
        // again for files the Store SCP saved during the cycle.
        private void SendScanCompletedStatuses(string facilityId, string statusURL, string staticApiKey, string dcmPushPath, string[] cycleFiles, HashSet<string> pendingAccessionNumbers)
        {
            if (careOutage != null)
                return;
            try
            {
                string errorString = string.Empty;
                List<ScanInProgress> scans = objDAL.GetScansWithStatus(facilityId, "STARTED", ref errorString);
                if (!string.IsNullOrEmpty(errorString))
                {
                    WriteToLog($"Not sending {ScanStatusCompleted}: {errorString}", false);
                    return;
                }
                if (scans.Count == 0)
                    return;

                int waitSeconds = GetIntSetting("scan_completed_wait_seconds", "scanCompletedWaitSeconds", DefaultScanCompletedWaitSeconds);
                bool folderRechecked = false;
                foreach (ScanInProgress scan in scans)
                {
                    if (pendingAccessionNumbers.Contains(scan.AccessionNumber))
                        continue;
                    if (scan.LastUploadTime != null && DateTime.Now < scan.LastUploadTime.Value.AddSeconds(waitSeconds))
                        continue;
                    // Scan Started sent on MPPS IN PROGRESS, before any of the study's images arrived
                    if (scan.FailedUploads == 0 && scan.SuccessfulUploads == 0)
                    {
                        if (scanCompletedHeldLogged.Add(scan.AccessionNumber))
                            WriteToLog($"Not sending {ScanStatusCompleted} for AccessionNumber={scan.AccessionNumber}: no instance uploaded yet - waiting for its images", true);
                        continue;
                    }
                    if (scan.FailedUploads > 0)
                    {
                        if (scanCompletedHeldLogged.Add(scan.AccessionNumber))
                            WriteToLog($"Not sending {ScanStatusCompleted} for AccessionNumber={scan.AccessionNumber}: " +
                                       $"{scan.FailedUploads} instance(s) failed to upload, {scan.SuccessfulUploads} uploaded", false);
                        continue;
                    }
                    scanCompletedHeldLogged.Remove(scan.AccessionNumber);
                    if (string.IsNullOrWhiteSpace(scan.ServiceRequestId))
                    {
                        WriteToLog($"Not sending {ScanStatusCompleted} for AccessionNumber={scan.AccessionNumber}: no service request id in care_worklist", false);
                        continue;
                    }

                    if (!folderRechecked)
                    {
                        folderRechecked = true;
                        if (!AddAccessionNumbersOfNewFiles(dcmPushPath, cycleFiles, pendingAccessionNumbers))
                            return;
                    }
                    if (pendingAccessionNumbers.Contains(scan.AccessionNumber))
                        continue;

                    if (SendScanStatus(statusURL, staticApiKey, scan.ServiceRequestId, scan.AccessionNumber, ScanStatusCompleted))
                        SaveScanStatus(scan.AccessionNumber, "COMPLETED");
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"Sending {ScanStatusCompleted} failed with exception {ex.Message}", false);
            }
        }

        // Adds the accession numbers of .dcm / .dicom files in the SCP folder that are not in cycleFiles
        // (saved by the Store SCP while the cycle ran) to pendingAccessionNumbers. Returns false when one
        // of them cannot be read yet, since the SCP may still be writing it and it could belong to any
        // accession number; those files are uploaded next cycle.
        private bool AddAccessionNumbersOfNewFiles(string dcmPushPath, string[] cycleFiles, HashSet<string> pendingAccessionNumbers)
        {
            var listedFiles = new HashSet<string>(cycleFiles, StringComparer.OrdinalIgnoreCase);
            foreach (string dcmfile in Directory.GetFiles(dcmPushPath, "*.*", SearchOption.AllDirectories))
            {
                if (listedFiles.Contains(dcmfile))
                    continue;
                string extension = Path.GetExtension(dcmfile);
                if (!extension.Equals(".dcm", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".dicom", StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    string accessionNumber = DicomFile.Open(dcmfile).Dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty);
                    if (!string.IsNullOrWhiteSpace(accessionNumber))
                        pendingAccessionNumbers.Add(accessionNumber);
                }
                catch (Exception ex)
                {
                    WriteToLog($"Not sending {ScanStatusCompleted} this cycle: {dcmfile} arrived during the cycle and cannot be read yet ({ex.Message})", true);
                    return false;
                }
            }
            return true;
        }

        // PATCHes study_status for a service request to the CARE scan status API. Returns whether CARE accepted it.
        private bool SendScanStatus(string statusURL, string staticApiKey, string serviceRequestId, string accessionNumber, string studyStatus)
        {
            return ucls_CareScanStatus.Send(statusURL, staticApiKey, serviceRequestId, accessionNumber, studyStatus, WriteToLog);
        }

        private void SaveScanStatus(string accessionNumber, string scanStatus)
        {
            string errorString = string.Empty;
            objDAL.SetScanStatus(accessionNumber, scanStatus, ref errorString);
            if (!string.IsNullOrEmpty(errorString))
                WriteToLog($"care_worklist scan_status update failed for AccessionNumber={accessionNumber}: {errorString}", false);
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
