using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Plexus.Common.Database
{
    /// <summary>
    /// Calls the CARE worklist API and turns its results into care_worklist records. Shared by
    /// CARE_MWL_Service (periodic worklist refresh) and CARE_SCU_Service (refresh when an uploaded
    /// file's accession number is not in care_worklist yet), so both derive the same accession numbers.
    /// </summary>
    public static class ucls_CareWorklist
    {
        /// <summary>
        /// Calls the CARE worklist API, always scoped to one facility via the facility query param,
        /// and returns the parsed response. Throws when the API does not return a success status code.
        /// </summary>
        public static CareWorklistResponse FetchWorklist(string baseUrl, string token, string modality, string fromDate, string facilityId, Action<string, bool> writeToLog)
        {
            string responseBody = GetCareWorklistDetailsAsync(baseUrl, token, modality, fromDate, facilityId, writeToLog).GetAwaiter().GetResult();
            return JsonConvert.DeserializeObject<CareWorklistResponse>(responseBody);
        }

        /// <summary>
        /// True when CARE reported the response as successful. Only such a response may be synced to
        /// care_worklist: an empty successful response legitimately completes every scheduled row, a
        /// failed one must not.
        /// </summary>
        public static bool IsSuccess(CareWorklistResponse careResponse)
        {
            return careResponse != null &&
                   careResponse.status != null &&
                   careResponse.status.Equals("success", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The accession number for a worklist result: service_request.meta.accession_number, or when
        /// that is empty, the last two dash-separated parts of the service request id joined together.
        /// </summary>
        public static string GetAccessionNumber(CareWorklistResult item)
        {
            string acc_servicerequestid = item.service_request?.id ?? string.Empty;

            string[] parts = acc_servicerequestid.Split('-');

            string result = parts.Length >= 2 ? parts[parts.Length - 2] + parts[parts.Length - 1] : acc_servicerequestid;

            string accNum = item.service_request?.meta?.accession_number ?? string.Empty;

            return string.IsNullOrWhiteSpace(accNum) ? result : accNum;
        }

        public static CareWorklistRecord ToCareWorklistRecord(CareWorklistResult item, string accessionNumber)
        {
            CareServiceRequest sr = item.service_request;
            return new CareWorklistRecord
            {
                AccessionNumber = accessionNumber,
                ServiceRequestId = sr?.id,
                ServiceRequestName = sr?.name,
                ServiceRequestDate = sr?.date?.ToLocalTime(),
                ServiceRequestBodySite = sr?.body_site == null || sr.body_site.Type == JTokenType.Null ? null : sr.body_site.ToString(Formatting.None),
                ServiceRequestDescription = sr?.description,
                ServiceRequestModality = sr?.modality,
                ServiceRequestProcedureId = sr?.procedure_id,
                ServiceRequestPriority = sr?.priority,
                ServiceRequestTechnicianInstruction = sr?.technician_instruction,
                ServiceRequestPatientInstruction = sr?.patient_instruction,
                CreatedByPrefix = sr?.created_by?.prefix,
                CreatedByFirstName = sr?.created_by?.first_name,
                CreatedByLastName = sr?.created_by?.last_name,
                FacilityId = item.facility?.id,
                FacilityName = item.facility?.name,
                PatientId = item.patient?.id,
                PatientName = item.patient?.name,
                PatientGender = item.patient?.gender,
                PatientAge = item.patient?.age,
                PatientUhid = item.patient?.patient_uhid
            };
        }

        /// <summary>
        /// Fetches the worklist for a facility and saves it with SyncCareWorklist. Returns false when
        /// the API call fails, CARE does not report success, or saving fails; the reason is logged.
        /// </summary>
        public static bool RefreshCareWorklist(ucls_DAL objDal, string baseUrl, string token, string modality, string fromDate, string facilityId, Action<string, bool> writeToLog)
        {
            try
            {
                CareWorklistResponse careResponse = FetchWorklist(baseUrl, token, modality, fromDate, facilityId, writeToLog);
                if (!IsSuccess(careResponse))
                {
                    writeToLog($"CARE worklist API did not report success (status={careResponse?.status ?? "none"}) - care_worklist not updated", false);
                    return false;
                }

                List<CareWorklistRecord> careRecords = new List<CareWorklistRecord>();
                if (careResponse.results != null)
                {
                    foreach (CareWorklistResult item in careResponse.results)
                        careRecords.Add(ToCareWorklistRecord(item, GetAccessionNumber(item)));
                }

                string errorString = string.Empty;
                int insertedCount = 0;
                int completedCount = 0;
                if (!objDal.SyncCareWorklist(careRecords, ref insertedCount, ref completedCount, ref errorString))
                {
                    writeToLog(errorString, false);
                    return false;
                }
                writeToLog($"care_worklist synced: {insertedCount} new row(s) inserted, {completedCount} row(s) marked COMPLETED, {careRecords.Count} item(s) in the CARE response", true);
                return true;
            }
            catch (Exception ex)
            {
                writeToLog("Refreshing care_worklist from the CARE worklist API failed with exception " + ex.Message, false);
                return false;
            }
        }

        /// <param name="facilityId">Facility ID to filter on. Required.</param>
        private static async Task<string> GetCareWorklistDetailsAsync(string baseUrl, string token, string modality, string fromDate, string facilityId, Action<string, bool> writeToLog)
        {
            string responseBody = string.Empty;

            try
            {
                string toDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                string requestUrl = baseUrl +
                                    "/api/care_radiology/dicom/worklist/?modality=" + Uri.EscapeDataString(modality ?? string.Empty) +
                                    "&from=" + Uri.EscapeDataString(fromDate ?? string.Empty) +
                                    "&to=" + Uri.EscapeDataString(toDate);

                requestUrl += "&facility=" + Uri.EscapeDataString(facilityId.Trim());

                writeToLog("CARE Worklist URL: " + requestUrl, true);

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("Authorization", token);

                    HttpResponseMessage response = await client.GetAsync(requestUrl).ConfigureAwait(false);

                    // Read the body BEFORE throwing. EnsureSuccessStatusCode discards it, which
                    // made a rejected token, a moved route and a permissions failure all surface
                    // as a bare "403 (Forbidden)" - the server's own explanation was thrown away.
                    responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        writeToLog(
                            $"CARE Worklist API returned {(int)response.StatusCode} ({response.ReasonPhrase}). Response body: {Truncate(responseBody, 1000)}", false);

                        // A 401/403 is nearly always the token not matching the server's
                        // CARE_RADIOLOGY_WEBHOOK_SECRET. Log a fingerprint of the token in use so
                        // it can be compared against the server without either side echoing it:
                        //   echo -n "$SECRET" | sha256sum
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                            response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            writeToLog(
                                $"Authorization rejected by CARE. Token in use: {DescribeToken(token)}. Verify it matches CARE_RADIOLOGY_WEBHOOK_SECRET on the server.", false);
                        }

                        response.EnsureSuccessStatusCode();
                    }
                }

                writeToLog("CARE Worklist API call successful. Returning the value", true);
            }
            catch (Exception ex)
            {
                writeToLog("Error calling CARE Worklist API with exception " + ex.Message, false);
                throw;
            }

            return responseBody;
        }

        /// <summary>
        /// Caps a logged response body so an HTML error page cannot flood the log file, which
        /// rolls at 5 KB per part.
        /// </summary>
        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "(empty)";
            value = value.Trim();
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "... (truncated)";
        }

        /// <summary>
        /// Describes the configured token without writing it to the log: its length plus a short
        /// SHA-256 fingerprint, which is enough to compare against the server's own secret.
        /// </summary>
        private static string DescribeToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return "(not configured)";

            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
                string hex = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
                return $"{token.Length} chars, sha256:{hex.Substring(0, 16)}";
            }
        }
    }

    public class CareWorklistResponse
    {
        public string status { get; set; }
        public List<CareWorklistResult> results { get; set; }
    }

    public class CareWorklistResult
    {
        public CareServiceRequest service_request { get; set; }
        public CareFacility facility { get; set; }
        public CarePatient patient { get; set; }
    }

    public class CareServiceRequestMeta
    {
        public string accession_number { get; set; }
    }

    public class CareServiceRequest
    {
        public string id { get; set; }
        public string external_id { get; set; }
        public string name { get; set; }
        public DateTime? date { get; set; }
        public CareServiceRequestMeta meta { get; set; }
        // Kept as raw JSON: CARE sends null or an object, and it is only stored, never read.
        public JToken body_site { get; set; }
        public string description { get; set; }
        public string modality { get; set; }
        public CareCreatedBy created_by { get; set; }
        public string technician_instruction { get; set; }
        public string patient_instruction { get; set; }
        public string priority { get; set; }
        public string procedure_id { get; set; }
    }

    public class CareCreatedBy
    {
        public string prefix { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
    }

    public class CareFacility
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    public class CarePatient
    {
        public string external_id { get; set; }
        public string id { get; set; }
        public string name { get; set; }
        public string address { get; set; }
        public string phone_number { get; set; }
        public string gender { get; set; }
        public int? age { get; set; }
        public string patient_uhid { get; set; }
    }
}
