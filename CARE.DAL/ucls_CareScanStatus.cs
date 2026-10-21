using System;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Plexus.Common.Database
{
    /// <summary>
    /// Sends study_status for a service request to the CARE scan status API. Shared by CARE_MWL_Service
    /// (Scan Started when the modality sends MPPS IN PROGRESS) and CARE_SCU_Service (Scan Started before
    /// the first upload when MPPS did not send it, and Scan Completed once the study is uploaded).
    /// </summary>
    public static class ucls_CareScanStatus
    {
        // The MWL service sends Scan Started while the modality waits for its N-CREATE response, so a
        // CARE that does not answer must not hold the modality for HttpClient's default 100 seconds
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>
        /// study_status values sent to the CARE scan status API. CARE adds the facility tag config whose
        /// display equals the value exactly, so they are configurable (App.config scanStatusStarted /
        /// scanStatusCompleted) to match how a facility named its tags.
        /// </summary>
        public static string StatusStarted => ReadStatus("scanStatusStarted", "Scan Started");
        public static string StatusCompleted => ReadStatus("scanStatusCompleted", "Scan Completed");

        private static string ReadStatus(string key, string defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        }

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
