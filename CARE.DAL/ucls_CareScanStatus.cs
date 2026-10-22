using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Plexus.Common.Database
{
    /// <summary>
    /// Sends study_status for a service request to the CARE scan status API. Shared by CARE_MWL_Service
    /// (IN_PROGRESS when the modality sends MPPS IN PROGRESS, CANCELLED on MPPS DISCONTINUED) and
    /// CARE_SCU_Service (IN_PROGRESS before the first upload when MPPS did not send it, COMPLETED once
    /// the study is uploaded, and the retries of IN_PROGRESS and CANCELLED that could not be sent).
    /// </summary>
    public static class ucls_CareScanStatus
    {
        // The MWL service sends IN_PROGRESS and CANCELLED while the modality waits for its N-CREATE / N-SET
        // response, so a CARE that does not answer must not hold the modality for HttpClient's default 100 seconds
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>
        /// study_status values the CARE scan status API accepts. CARE sets the imaging study to the value
        /// (an imaging study already COMPLETED or CANCELLED is left unchanged) and tags the service request itself.
        /// </summary>
        public const string StatusInProgress = "IN_PROGRESS";
        public const string StatusCompleted = "COMPLETED";
        public const string StatusCancelled = "CANCELLED";

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
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    writeToLog($"Scan status sent to CARE: {studyStatus} for service_request {serviceRequestId} (AccessionNumber={accessionNumber})", true);
                    return true;
                }
                writeToLog($"Scan status failed: {studyStatus} - {(int)response.StatusCode} ({response.ReasonPhrase}) for service_request {serviceRequestId} (AccessionNumber={accessionNumber}): {responseBody}", false);
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
