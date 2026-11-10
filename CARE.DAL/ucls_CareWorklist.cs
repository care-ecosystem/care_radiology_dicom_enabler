using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Plexus.Common.Database
{
    /// <summary>
    /// Calls the CARE worklist API and turns its results into care_worklist records. Shared by
    /// CARE_MWL_Service (periodic worklist refresh) and CARE_SCU_Service (refresh when an uploaded
    /// file's accession number is not in care_worklist yet), so both derive the same accession numbers.
    /// </summary>
    public static class ucls_CareWorklist
    {
        /// <summary>Page size sent as limit; CARE returns at most this many results per call.</summary>
        public const int PageSize = 14;

        /// <summary>
        /// Fetches every page of a DICOM Enabler's worklist created from fromDate until now: only the
        /// facility's requests scheduled in the rooms of the enabler's DICOM devices, each listing only
        /// its devices (device={dicomEnablerId}). dicomEnablerId is required - the whole facility's
        /// worklist is never fetched. It is not filtered by modality: the modality a device queries with
        /// is matched when it sends C-FIND, against the modality of the devices each request is on.
        /// </summary>
        public static CareWorklistResponse FetchWorklist(string baseUrl, string token, string fromDate, string facilityId, string dicomEnablerId, Action<string, bool> writeToLog)
        {
            if (string.IsNullOrWhiteSpace(dicomEnablerId))
                throw new ArgumentException("The CARE worklist is only fetched for a DICOM Enabler: dicomEnablerId is required.", nameof(dicomEnablerId));

            // Fixed for every page so the date window does not move while paging
            string toDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var all = new CareWorklistResponse { status = "success", results = new List<CareWorklistResult>() };
            int offset = 0;
            while (true)
            {
                string responseBody = GetCareWorklistDetailsAsync(baseUrl, token, fromDate, toDate, facilityId, dicomEnablerId, offset, writeToLog).GetAwaiter().GetResult();
                CareWorklistResponse page = JsonConvert.DeserializeObject<CareWorklistResponse>(responseBody);
                if (!IsSuccess(page))
                    return page;

                all.count = page.count;
                int pageCount = page.results?.Count ?? 0;
                if (pageCount > 0)
                    all.results.AddRange(page.results);

                offset += pageCount;
                // An empty page ends the loop even if count says more, so a count that changed
                // between calls cannot keep it requesting pages forever
                if (pageCount == 0 || offset >= page.count)
                    break;
            }
            return all;
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
        /// The accession number of a worklist result: service_request.accession_number, trimmed. CARE
        /// always sends one; null when it is missing, and the entry is then not added to the worklist.
        /// </summary>
        public static string GetAccessionNumber(CareWorklistResult item)
        {
            return NullIfBlank(item?.service_request?.accession_number);
        }

        /// <summary>
        /// Maps a worklist result to a care_worklist record. The facility ID is the Configuration tab
        /// value the worklist was fetched with, not a value from the response. The modality is that of
        /// the first DICOM device in the response's locations; with none it is left blank, and saving
        /// keeps the modality already stored for the service request.
        /// </summary>
        public static CareWorklistRecord ToCareWorklistRecord(CareWorklistResult item, string accessionNumber, string facilityId)
        {
            CareServiceRequest sr = item.service_request;
            return new CareWorklistRecord
            {
                AccessionNumber = accessionNumber,
                ServiceRequestId = sr?.id,
                ServiceRequestName = sr?.name,
                ServiceRequestDate = sr?.date?.ToLocalTime(),
                ServiceRequestBodySite = sr?.body_site,
                ServiceRequestBodySiteId = sr?.body_site_id,
                ServiceRequestReferringPhysician = NullIfBlank(sr?.referring_physician),
                ServiceRequestDescription = sr?.description,
                ServiceRequestModality = NullIfBlank(item.locations?.Where(l => l?.devices != null).SelectMany(l => l.devices)
                                                         .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d?.modality))?.modality),
                ServiceRequestProcedureId = sr?.procedure_id,
                ServiceRequestPriority = sr?.priority,
                ServiceRequestTechnicianInstruction = sr?.technician_instruction,
                ServiceRequestPatientInstruction = sr?.patient_instruction,
                FacilityId = NullIfBlank(facilityId),
                FacilityName = item.facility?.name,
                PatientId = item.patient?.id,
                PatientName = item.patient?.name,
                PatientGender = item.patient?.gender,
                PatientAge = item.patient?.age,
                PatientUhid = item.patient?.patient_uhid,
                Devices = ToCareWorklistDevices(item.locations)
            };
        }

        /// <summary>
        /// Flattens the result's locations into one entry per device, or one entry without a device
        /// for a location that has none.
        /// </summary>
        private static List<CareWorklistDevice> ToCareWorklistDevices(List<CareLocation> locations)
        {
            var devices = new List<CareWorklistDevice>();
            if (locations == null)
                return devices;

            foreach (CareLocation location in locations)
            {
                if (location == null)
                    continue;
                if (location.devices == null || location.devices.Count == 0)
                {
                    devices.Add(new CareWorklistDevice { LocationId = location.id, LocationName = location.name });
                    continue;
                }
                foreach (CareDevice device in location.devices)
                {
                    devices.Add(new CareWorklistDevice
                    {
                        LocationId = location.id,
                        LocationName = location.name,
                        DeviceId = device?.id,
                        DeviceName = device?.name,
                        DeviceAeTitle = NullIfBlank(device?.ae_title),
                        DeviceModality = NullIfBlank(device?.modality)?.ToUpperInvariant()
                    });
                }
            }
            return devices;
        }

        private static string NullIfBlank(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>
        /// Fetches the DICOM Enabler's worklist (device filter) and saves it with SyncCareWorklist. Every
        /// refresh - the MWL service's periodic one, the one when a C-FIND matches no row, and the SCU
        /// service's - goes through here. Returns false without calling CARE when no DICOM Enabler is
        /// selected in the Configuration tab, and when the API call fails, CARE does not report success,
        /// or saving fails; the reason is logged.
        /// </summary>
        public static bool RefreshCareWorklist(ucls_DAL objDal, string baseUrl, string token, string fromDate, string facilityId, string dicomEnablerId, Action<string, bool> writeToLog)
        {
            if (string.IsNullOrWhiteSpace(dicomEnablerId))
            {
                writeToLog("Not calling the CARE worklist API: no DICOM Enabler is selected in the Configuration tab. " +
                           "The worklist is only fetched for a DICOM Enabler (device filter) - select one and save it.", false);
                return false;
            }
            try
            {
                CareWorklistResponse careResponse = FetchWorklist(baseUrl, token, fromDate, facilityId, dicomEnablerId, writeToLog);
                if (!IsSuccess(careResponse))
                {
                    writeToLog($"CARE worklist API did not report success (status={careResponse?.status ?? "none"}) - care_worklist not updated", false);
                    return false;
                }

                List<CareWorklistRecord> careRecords = new List<CareWorklistRecord>();
                // An accession number identifies one service request; care_worklist keeps one row per accession number
                var keptByAccession = new Dictionary<string, CareServiceRequest>(StringComparer.OrdinalIgnoreCase);
                foreach (CareWorklistResult item in careResponse.results ?? new List<CareWorklistResult>())
                {
                    CareServiceRequest sr = item?.service_request;
                    string accessionNumber = GetAccessionNumber(item);
                    if (accessionNumber == null)
                    {
                        writeToLog($"Worklist entry skipped: service request {sr?.id} ({sr?.name}) has no accession_number in CARE", false);
                        continue;
                    }
                    if (keptByAccession.TryGetValue(accessionNumber, out CareServiceRequest kept))
                    {
                        writeToLog($"Duplicate accession_number {accessionNumber} in the CARE worklist: service request {sr?.id} ({sr?.name}) skipped, " +
                                   $"{kept?.id} ({kept?.name}) kept. Each service request needs its own accession number in CARE.", false);
                        continue;
                    }
                    keptByAccession[accessionNumber] = sr;
                    careRecords.Add(ToCareWorklistRecord(item, accessionNumber, facilityId));
                }

                string errorString = string.Empty;
                int insertedCount = 0;
                int completedCount = 0;
                if (!objDal.SyncCareWorklist(careRecords, facilityId, ref insertedCount, ref completedCount, ref errorString))
                {
                    writeToLog(errorString, false);
                    return false;
                }
                writeToLog($"care_worklist synced: {insertedCount} new row(s) inserted, {completedCount} row(s) marked COMPLETED, {careRecords.Count} item(s) in the CARE response (count={careResponse.count})", true);
                return true;
            }
            catch (Exception ex)
            {
                writeToLog("Refreshing care_worklist from the CARE worklist API failed with exception " + ex.Message, false);
                return false;
            }
        }

        /// <summary>Fetches one page (PageSize results from offset) of the facility's worklist.</summary>
        /// <param name="facilityId">Facility ID (UUID) the worklist is fetched for. Required.</param>
        private static async Task<string> GetCareWorklistDetailsAsync(string baseUrl, string token, string fromDate, string toDate, string facilityId, string dicomEnablerId, int offset, Action<string, bool> writeToLog)
        {
            string responseBody = string.Empty;
            bool responseReceived = false;

            try
            {
                string requestUrl = baseUrl +
                                    "/api/care_radiology/facility/" + Uri.EscapeDataString(facilityId.Trim()) + "/external/worklist/" +
                                    "?limit=" + PageSize +
                                    "&offset=" + offset;

                requestUrl += "&device=" + Uri.EscapeDataString(dicomEnablerId.Trim());
                if (!string.IsNullOrWhiteSpace(fromDate))
                    requestUrl += "&created_date_after=" + Uri.EscapeDataString(fromDate.Trim());
                requestUrl += "&created_date_before=" + Uri.EscapeDataString(toDate);

                writeToLog("CARE Worklist URL: " + requestUrl, true);

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("Authorization", token);

                    HttpResponseMessage response = await client.GetAsync(requestUrl).ConfigureAwait(false);
                    responseReceived = true;

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
                // The exception itself is logged by RefreshCareWorklist. No response at all: CARE could not be reached
                if (!responseReceived && (ex is HttpRequestException || ex is TaskCanceledException))
                    writeToLog("Could not connect to the CARE Worklist API. " + ucls_NetworkCheck.Describe(), false);
                throw;
            }

            return responseBody;
        }

        /// <summary>
        /// Caps a logged response body so an HTML error page cannot flood the log file, which
        /// rolls at 5 KB per part.
        /// </summary>
        internal static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "(empty)";
            value = value.Trim();
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "... (truncated)";
        }

        /// <summary>
        /// Describes the configured token without writing it to the log: its length plus a short
        /// SHA-256 fingerprint, which is enough to compare against the server's own secret.
        /// </summary>
        internal static string DescribeToken(string token)
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
        /// <summary>Total results across all pages, not just this page's.</summary>
        public int count { get; set; }
        public string status { get; set; }
        public List<CareWorklistResult> results { get; set; }
    }

    public class CareWorklistResult
    {
        public CareServiceRequest service_request { get; set; }
        public List<CareLocation> locations { get; set; }
        public CareFacility facility { get; set; }
        public CarePatient patient { get; set; }
    }

    public class CareServiceRequest
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string body_site { get; set; }
        public string body_site_id { get; set; }
        public string procedure_id { get; set; }
        public string priority { get; set; }
        public string technician_instruction { get; set; }
        public string patient_instruction { get; set; }
        public DateTime? date { get; set; }
        public string accession_number { get; set; }
        public string referring_physician { get; set; }
    }

    public class CareLocation
    {
        public string id { get; set; }
        public string name { get; set; }
        public List<CareDevice> devices { get; set; }
    }

    /// <summary>
    /// A DICOM device (care_type: dicom) in a worklist location. ae_title is its Scheduled Station AE
    /// Title (0040,0001) in the worklist, not the AE title a modality calls in with.
    /// </summary>
    public class CareDevice
    {
        public string id { get; set; }
        public string name { get; set; }
        public string ae_title { get; set; }
        public string modality { get; set; }
    }

    public class CareFacility
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    public class CarePatient
    {
        public string id { get; set; }
        public string name { get; set; }
        public string gender { get; set; }
        public int? age { get; set; }
        public string patient_uhid { get; set; }
    }
}
