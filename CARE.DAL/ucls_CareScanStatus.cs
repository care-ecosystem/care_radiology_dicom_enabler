using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Plexus.Common.Database
{
    /// <summary>
    /// Sends study_status for a service request to the CARE scan status API. Shared by CARE_MWL_Service
    /// (in_progress when the modality sends MPPS IN PROGRESS, cancelled on MPPS DISCONTINUED) and
    /// CARE_SCU_Service (in_progress before the first upload when MPPS did not send it, completed once
    /// the study is uploaded, and the retries of in_progress and cancelled that could not be sent).
    /// </summary>
    public static class ucls_CareScanStatus
    {
        // The MWL service sends in_progress and cancelled while the modality waits for its N-CREATE / N-SET
        // response, so a CARE that does not answer must not hold the modality for HttpClient's default 100 seconds
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>
        /// study_status values the CARE scan status API accepts. CARE sets the imaging study to the value
        /// (an imaging study already completed or cancelled is left unchanged) and tags the service request itself.
        /// </summary>
        public const string StatusInProgress = "in_progress";
        public const string StatusCompleted = "completed";
        public const string StatusCancelled = "cancelled";

        /// <summary>
        /// The facility's scan status API URL: statusPath (App.config statusURL) under baseUrl, with
        /// {facility_id} replaced by the Facility ID.
        /// </summary>
        public static string BuildStatusURL(string baseUrl, string statusPath, string facilityId)
        {
            return (baseUrl ?? string.Empty).TrimEnd('/') +
                   (statusPath ?? string.Empty).Replace("{facility_id}", Uri.EscapeDataString((facilityId ?? string.Empty).Trim()));
        }

        /// <summary>PATCHes study_status for a service request. Returns whether CARE accepted it.</summary>
        public static bool Send(string statusURL, string token, string serviceRequestId, string accessionNumber, string studyStatus, Action<string, bool> writeToLog)
        {
            try
            {
                string payload = JsonConvert.SerializeObject(new { service_request_id = serviceRequestId, study_status = studyStatus });
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), statusURL);
                request.Headers.Add("Authorization", token);
                ucls_CareApiError.Detail(writeToLog, $"Scan status PATCH {statusURL}: {payload}");
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
#if DEBUG
                    writeToLog($"Scan status sent to CARE: {studyStatus} for AccessionNumber={accessionNumber} (service_request {serviceRequestId})", true);
#else
                    writeToLog($"PATCH {ucls_CareApiError.DescribeEndpoint(statusURL)} ({ucls_CareApiError.DescribePayload(payload)}) " +
                               $"for AccessionNumber={accessionNumber} - {(int)response.StatusCode} OK", true);
#endif
                    return true;
                }
                ucls_CareApiError.Detail(writeToLog, $"Scan status API response body: {ucls_CareWorklist.Truncate(responseBody, 1000)}");
                writeToLog($"Scan status {studyStatus} for AccessionNumber={accessionNumber} (service_request {serviceRequestId}) failed: " +
                    ucls_CareApiError.Describe("CARE scan status API", response.StatusCode, response.ReasonPhrase, responseBody), false);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                writeToLog($"Scan status could not connect to CARE: {studyStatus} for service_request {serviceRequestId}: {ex.Message}. {ucls_NetworkCheck.Describe()}", false);
            }
            catch (Exception ex)
            {
                writeToLog($"Scan status failed: {studyStatus} for service_request {serviceRequestId} with exception {ex.Message}", false);
            }
            return false;
        }
    }
}
