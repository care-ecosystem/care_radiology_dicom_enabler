using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Plexus.Common.Database
{
    /// <summary>
    /// Calls the CARE modalities API, which lists the facility's DICOM Enablers (care_type dicom-enabler,
    /// one per DICOM Enabler, usually per room) and DICOM devices (care_type dicom, one per modality
    /// machine). Every DICOM device is connected to exactly one DICOM Enabler through its dicom_enabler.
    /// Only devices whose status is active and availability_status is available are listed, assigned or not.
    /// Authenticated with the same static API key (CARE_RADIOLOGY_WEBHOOK_SECRET) as the imaging study APIs.
    /// </summary>
    public static class ucls_CareModalities
    {
        /// <summary>The modalities API path under careBackendURL; {facility_id} is replaced by the Facility ID.</summary>
        public const string DefaultModalitiesPath = "/api/care_radiology/facility/{facility_id}/external/modalities/";

        /// <summary>Page size sent as limit; CARE returns at most this many results per call.</summary>
        public const int PageSize = 14;

        /// <summary>Shown for a field CARE did not return (or returned empty).</summary>
        public const string NotAvailable = "N/A";

        /// <summary>value trimmed, or NotAvailable when it is empty.</summary>
        public static string OrNotAvailable(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? NotAvailable : value.Trim();
        }

        /// <summary>care_type / device_type values.</summary>
        public const string CareTypeDicomEnabler = "dicom-enabler";
        public const string CareTypeDicom = "dicom";

        /// <summary>assignment_status values.</summary>
        public const string AssignmentUnassigned = "unassigned";
        public const string AssignmentAssigned = "assigned";

        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>
        /// The facility's modalities API URL: modalitiesPath (DefaultModalitiesPath when empty) under
        /// baseUrl, with {facility_id} replaced by the Facility ID.
        /// </summary>
        public static string BuildModalitiesURL(string baseUrl, string modalitiesPath, string facilityId)
        {
            string path = string.IsNullOrWhiteSpace(modalitiesPath) ? DefaultModalitiesPath : modalitiesPath.Trim();
            return (baseUrl ?? string.Empty).TrimEnd('/') +
                   path.Replace("{facility_id}", Uri.EscapeDataString((facilityId ?? string.Empty).Trim()));
        }

        /// <summary>
        /// Fetches every page of the facility's DICOM Enablers and DICOM devices. All filters are optional
        /// (null or empty leaves them out):
        ///   careType         - CareTypeDicomEnabler or CareTypeDicom; omitted lists both types.
        ///   assignmentStatus - AssignmentUnassigned or AssignmentAssigned; omitted lists both.
        ///   dicomEnablerId   - a DICOM Enabler's id; lists only the DICOM devices connected to it
        ///                      (the enabler itself is not included).
        /// Returns false when the call fails or CARE rejects it; error then says why (it is also logged).
        /// </summary>
        public static bool TryFetchModalities(string modalitiesURL, string token, string careType, string assignmentStatus, string dicomEnablerId,
                                              Action<string, bool> writeToLog, out CareModalitiesResponse modalities, out string error)
        {
            modalities = null;
            if (!ValidateFilters(careType, assignmentStatus, dicomEnablerId, writeToLog, out error))
                return false;

            var all = new CareModalitiesResponse { results = new List<CareModality>() };
            int offset = 0;
            try
            {
                while (true)
                {
                    string requestUrl = BuildQueryURL(modalitiesURL, careType, assignmentStatus, dicomEnablerId, offset);
                    if (!TryGetPage(requestUrl, token, writeToLog, out CareModalitiesResponse page, out error))
                        return false;

                    all.count = page.count;
                    int pageCount = page.results?.Count ?? 0;
                    if (pageCount > 0)
                        all.results.AddRange(page.results.Where(m => m != null));

                    offset += pageCount;
                    // An empty page ends the loop even if count says more, so a count that changed
                    // between calls cannot keep it requesting pages forever
                    if (pageCount == 0 || offset >= page.count)
                        break;
                }
            }
            catch (Exception ex)
            {
                error = "Fetching modalities from CARE failed with exception " + ex.Message;
                writeToLog(error, false);
                return false;
            }

            writeToLog($"CARE Modalities API returned {all.results.Count} device(s) (count={all.count})", true);
            modalities = all;
            return true;
        }

        /// <summary>
        /// Fetches one page (at most PageSize devices, starting at offset) with the same filters as
        /// TryFetchModalities. page.count is the total across all pages, for paging through them.
        /// Returns false when the call fails or CARE rejects it; error then says why (it is also logged).
        /// </summary>
        public static bool TryFetchModalitiesPage(string modalitiesURL, string token, string careType, string assignmentStatus, string dicomEnablerId,
                                                  int offset, Action<string, bool> writeToLog, out CareModalitiesResponse page, out string error)
        {
            page = null;
            if (!ValidateFilters(careType, assignmentStatus, dicomEnablerId, writeToLog, out error))
                return false;

            try
            {
                string requestUrl = BuildQueryURL(modalitiesURL, careType, assignmentStatus, dicomEnablerId, Math.Max(0, offset));
                if (!TryGetPage(requestUrl, token, writeToLog, out page, out error))
                    return false;
            }
            catch (Exception ex)
            {
                page = null;
                error = "Fetching modalities from CARE failed with exception " + ex.Message;
                writeToLog(error, false);
                return false;
            }

            if (page.results == null)
                page.results = new List<CareModality>();
            page.results.RemoveAll(m => m == null);
            writeToLog($"CARE Modalities API returned {page.results.Count} device(s) from offset {Math.Max(0, offset)} (count={page.count})", true);
            return true;
        }

        /// <summary>The DICOM Enablers that are not assigned yet, for the user to pick from.</summary>
        public static bool TryFetchUnassignedDicomEnablers(string modalitiesURL, string token, Action<string, bool> writeToLog,
                                                           out CareModalitiesResponse modalities, out string error)
        {
            return TryFetchModalities(modalitiesURL, token, CareTypeDicomEnabler, AssignmentUnassigned, null, writeToLog, out modalities, out error);
        }

        /// <summary>Checks the filters before calling CARE, so a bad value gets a clear error without a request.</summary>
        private static bool ValidateFilters(string careType, string assignmentStatus, string dicomEnablerId, Action<string, bool> writeToLog, out string error)
        {
            error = null;
            if (!string.IsNullOrWhiteSpace(careType) && careType.Trim() != CareTypeDicomEnabler && careType.Trim() != CareTypeDicom)
                error = $"care_type must be {CareTypeDicomEnabler} or {CareTypeDicom}, not '{careType}'.";
            else if (!string.IsNullOrWhiteSpace(assignmentStatus) && assignmentStatus.Trim() != AssignmentUnassigned && assignmentStatus.Trim() != AssignmentAssigned)
                error = $"assignment_status must be {AssignmentUnassigned} or {AssignmentAssigned}, not '{assignmentStatus}'.";
            else if (!string.IsNullOrWhiteSpace(dicomEnablerId) && !Guid.TryParse(dicomEnablerId.Trim(), out _))
                error = $"dicom_enabler must be a DICOM Enabler id (UUID), not '{dicomEnablerId}'.";
            if (error == null)
                return true;
            writeToLog("CARE Modalities API not called: " + error, false);
            return false;
        }

        /// <summary>The DICOM devices (modality machines) connected to a DICOM Enabler.</summary>
        public static bool TryFetchDicomDevices(string modalitiesURL, string token, string dicomEnablerId, Action<string, bool> writeToLog,
                                                out CareModalitiesResponse modalities, out string error)
        {
            if (string.IsNullOrWhiteSpace(dicomEnablerId))
            {
                modalities = null;
                error = "dicom_enabler is required to list the DICOM devices connected to a DICOM Enabler.";
                writeToLog("CARE Modalities API not called: " + error, false);
                return false;
            }
            return TryFetchModalities(modalitiesURL, token, CareTypeDicom, null, dicomEnablerId, writeToLog, out modalities, out error);
        }

        private static string BuildQueryURL(string modalitiesURL, string careType, string assignmentStatus, string dicomEnablerId, int offset)
        {
            string requestUrl = modalitiesURL + (modalitiesURL.Contains("?") ? "&" : "?") +
                                "limit=" + PageSize +
                                "&offset=" + offset;
            if (!string.IsNullOrWhiteSpace(careType))
                requestUrl += "&care_type=" + Uri.EscapeDataString(careType.Trim());
            if (!string.IsNullOrWhiteSpace(assignmentStatus))
                requestUrl += "&assignment_status=" + Uri.EscapeDataString(assignmentStatus.Trim());
            if (!string.IsNullOrWhiteSpace(dicomEnablerId))
                requestUrl += "&dicom_enabler=" + Uri.EscapeDataString(dicomEnablerId.Trim());
            return requestUrl;
        }

        /// <summary>Fetches one page. Returns false with error set (and logged) when CARE cannot be reached or rejects the call.</summary>
        private static bool TryGetPage(string requestUrl, string token, Action<string, bool> writeToLog, out CareModalitiesResponse page, out string error)
        {
            page = null;
            if (!TrySend(HttpMethod.Get, requestUrl, token, null, writeToLog, out string responseBody, out error))
                return false;

            try
            {
                page = JsonConvert.DeserializeObject<CareModalitiesResponse>(responseBody);
            }
            catch (JsonException ex)
            {
                error = "CARE Modalities API returned a response that could not be read: " + ex.Message;
                writeToLog($"{error}. Response body: {ucls_CareWorklist.Truncate(responseBody, 1000)}", false);
                return false;
            }
            if (page == null)
            {
                error = "CARE Modalities API returned an empty response.";
                writeToLog(error, false);
                return false;
            }
            return true;
        }

        /// <summary>
        /// GET .../external/modalities/{device_id}/: the full details of a DICOM Enabler (with its DICOM
        /// endpoints store_scp and modality_scp) or a DICOM device (returned as in the list). Used when a
        /// DICOM Enabler is selected and by Sync, for the enabler and each of its DICOM devices.
        /// Returns false when the call fails or CARE rejects it; error then says why (it is also logged).
        /// </summary>
        public static bool TryRetrieveModality(string modalitiesURL, string token, string deviceId, Action<string, bool> writeToLog,
                                               out CareModality modality, out string error)
        {
            modality = null;
            if (!ValidateDeviceId(deviceId, writeToLog, out error))
                return false;

            string requestUrl = BuildDeviceURL(modalitiesURL, deviceId, null);
            if (!TrySend(HttpMethod.Get, requestUrl, token, null, writeToLog, out string responseBody, out error))
                return false;

            try
            {
                modality = JsonConvert.DeserializeObject<CareModality>(responseBody);
            }
            catch (JsonException ex)
            {
                modality = null;
                error = "CARE Modalities API returned details that could not be read: " + ex.Message;
                writeToLog($"{error}. Response body: {ucls_CareWorklist.Truncate(responseBody, 1000)}", false);
                return false;
            }
            if (modality == null)
            {
                error = "CARE Modalities API returned empty details.";
                writeToLog(error, false);
                return false;
            }
            writeToLog($"CARE Modalities API returned the details of {modality.device_type ?? "device"} {deviceId} ({modality.device_name})", true);
            return true;
        }

        /// <summary>
        /// A device as CARE returned it, as a care_devices row: one column per field. A DICOM device's
        /// machine (modality, AE title, IP) and a DICOM Enabler's IP and DICOM endpoints each get their own
        /// columns. dicomEnablerId is the enabler a DICOM device is connected to (null for an enabler).
        /// </summary>
        public static CareDeviceRecord ToDeviceRecord(CareModality device, string facilityId, string dicomEnablerId)
        {
            return new CareDeviceRecord
            {
                DeviceId = device.id?.Trim(),
                FacilityId = facilityId,
                DicomEnablerId = string.IsNullOrWhiteSpace(dicomEnablerId) ? null : dicomEnablerId.Trim(),
                DeviceName = device.device_name,
                DeviceType = device.device_type,
                Modality = device.machine?.modality?.Trim().ToUpperInvariant(),
                AeTitle = device.machine?.ae_title,
                Ip = device.machine?.ip ?? device.ip,
                LocationId = device.location?.id,
                LocationName = device.location?.name,
                Status = device.device_status?.status,
                AvailabilityStatus = device.device_status?.availability_status,
                AssignmentStatus = device.assignment_status,
                LastSyncedAt = device.last_synced_at?.ToString("o"),
                StoreScpAeTitle = device.store_scp?.ae_title,
                StoreScpHost = device.store_scp?.host,
                StoreScpPort = device.store_scp?.port,
                ModalityScpAeTitle = device.modality_scp?.ae_title,
                ModalityScpHost = device.modality_scp?.host,
                ModalityScpPort = device.modality_scp?.port,
                UpdatedTime = DateTime.Now
            };
        }

        /// <summary>
        /// The DICOM endpoints (Modality Worklist SCP and Store SCP) of a DICOM Enabler saved in care_devices.
        /// These are the only source of the Modality SCP and Store SCP AE titles and ports. Returns false with
        /// error set when either has no AE title or a port outside 1-65535.
        /// </summary>
        public static bool TryGetDicomEndpoints(CareDeviceRecord enabler, out CareDicomEndpoint modalityScp, out CareDicomEndpoint storeScp, out string error)
        {
            modalityScp = null;
            storeScp = null;
            if (enabler == null)
            {
                error = "No DICOM Enabler is saved.";
                return false;
            }
            var modality = new CareDicomEndpoint { ae_title = enabler.ModalityScpAeTitle, host = enabler.ModalityScpHost, port = enabler.ModalityScpPort };
            var store = new CareDicomEndpoint { ae_title = enabler.StoreScpAeTitle, host = enabler.StoreScpHost, port = enabler.StoreScpPort };
            if (!ValidateDicomEndpoints(modality, store, out error))
                return false;
            modalityScp = modality;
            storeScp = store;
            return true;
        }

        /// <summary>
        /// Checks a DICOM Enabler's endpoints as CARE returned them (modality_scp, store_scp): each needs an
        /// AE title of at most 16 characters and a port from 1 to 65535. Returns false with error set otherwise.
        /// </summary>
        public static bool ValidateDicomEndpoints(CareDicomEndpoint modalityScp, CareDicomEndpoint storeScp, out string error)
        {
            error = DescribeEndpointProblem("modality_scp", modalityScp) ?? DescribeEndpointProblem("store_scp", storeScp);
            return error == null;
        }

        /// <summary>Why an endpoint cannot be used, or null when it can.</summary>
        private static string DescribeEndpointProblem(string name, CareDicomEndpoint endpoint)
        {
            if (endpoint == null)
                return $"The DICOM Enabler has no {name} in CARE.";
            if (string.IsNullOrWhiteSpace(endpoint.ae_title))
                return $"The DICOM Enabler's {name} has no ae_title in CARE.";
            // DICOM limits an AE title to 16 characters
            if (endpoint.ae_title.Trim().Length > 16)
                return $"The DICOM Enabler's {name} ae_title '{endpoint.ae_title.Trim()}' is longer than 16 characters.";
            if (endpoint.port == null || endpoint.port < 1 || endpoint.port > 65535)
                return $"The DICOM Enabler's {name} port '{endpoint.port}' is not a port number (1-65535).";
            return null;
        }

        /// <summary>
        /// PATCH .../external/modalities/{device_id}/status/ with assignment_status: assigned once a
        /// DICOM Enabler is selected, or unassigned once no DICOM Enabler uses it any more.
        /// Returns false when the call fails or CARE rejects it; error then says why (it is also logged).
        /// </summary>
        public static bool TryUpdateAssignmentStatus(string modalitiesURL, string token, string deviceId, string assignmentStatus,
                                                     Action<string, bool> writeToLog, out string error, TimeSpan? timeout = null)
        {
            if (!ValidateDeviceId(deviceId, writeToLog, out error))
                return false;
            if (assignmentStatus != AssignmentAssigned && assignmentStatus != AssignmentUnassigned)
            {
                error = $"assignment_status must be {AssignmentUnassigned} or {AssignmentAssigned}, not '{assignmentStatus}'.";
                writeToLog("CARE Modalities API not called: " + error, false);
                return false;
            }

            string requestUrl = BuildDeviceURL(modalitiesURL, deviceId, "status/");
            string payload = JsonConvert.SerializeObject(new { assignment_status = assignmentStatus });
            var patch = new HttpMethod("PATCH");
            if (!TrySend(patch, requestUrl, token, payload, writeToLog, out _, out error, timeout))
                return false;

            writeToLog($"CARE Modalities API set assignment_status of {deviceId} to {assignmentStatus}", true);
            return true;
        }

        /// <summary>
        /// Sets the saved DICOM Enabler (care_config dicom_enabler_id) and its saved DICOM devices (care_devices)
        /// to assignmentStatus in CARE (PATCH .../{device_id}/status/), and saves the same status in their
        /// care_devices rows. Used by the MWL service: unassigned when it stops, assigned when it starts.
        /// The devices are skipped when the enabler cannot be set (CARE unreachable or rejecting the call).
        /// Returns false when the enabler or any device could not be set; summary says what was done either way.
        /// Nothing is done (true) when no Facility Id or DICOM Enabler is saved.
        /// requestTimeout / deadline (while Windows shuts down): each call gets at most requestTimeout, the
        /// enabler goes first, and the devices left when the deadline passes are skipped (they are set to
        /// assigned again when the service starts).
        /// </summary>
        public static bool TrySetEnablerAssignment(ucls_DAL dal, string baseUrl, string token, string modalitiesPath, string assignmentStatus,
                                                   Action<string, bool> writeToLog, out string summary,
                                                   TimeSpan? requestTimeout = null, DateTime? deadline = null)
        {
            string errorString = string.Empty;
            string facilityId = dal.GetConfigValue("facility_id", string.Empty, ref errorString).Trim();
            string enablerId = string.IsNullOrEmpty(errorString) ? dal.GetConfigValue("dicom_enabler_id", string.Empty, ref errorString).Trim() : string.Empty;
            if (!string.IsNullOrEmpty(errorString))
            {
                summary = $"DICOM Enabler not set to {assignmentStatus} in CARE: the configuration could not be read: {errorString}";
                writeToLog(summary, false);
                return false;
            }
            if (facilityId == string.Empty || enablerId == string.Empty)
            {
                summary = $"DICOM Enabler not set to {assignmentStatus} in CARE: no Facility Id or DICOM Enabler is saved";
                writeToLog(summary, true);
                return true;
            }
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
            {
                summary = $"DICOM Enabler {enablerId} not set to {assignmentStatus} in CARE: the CARE URL or API key is not set in App.config";
                writeToLog(summary, false);
                return false;
            }
            string modalitiesURL = BuildModalitiesURL(baseUrl, modalitiesPath, facilityId);

            if (!TryUpdateAssignmentStatus(modalitiesURL, token, enablerId, assignmentStatus, writeToLog, out string error, requestTimeout))
            {
                summary = $"DICOM Enabler {enablerId} and its devices not set to {assignmentStatus} in CARE: {error}";
                writeToLog(summary, false);
                return false;
            }
            var done = new List<string> { enablerId };
            var failed = new List<string>();
            List<CareDeviceRecord> devices = dal.GetCareDevices(enablerId, ref errorString);
            if (!string.IsNullOrEmpty(errorString))
                failed.Add("the saved devices could not be read: " + errorString);
            foreach (CareDeviceRecord device in devices.Where(d => !string.IsNullOrWhiteSpace(d.DeviceId)))
            {
                if (deadline.HasValue && DateTime.UtcNow >= deadline.Value)
                {
                    failed.Add("the remaining device(s): out of time (Windows is shutting down)");
                    break;
                }
                if (TryUpdateAssignmentStatus(modalitiesURL, token, device.DeviceId, assignmentStatus, writeToLog, out error, requestTimeout))
                    done.Add(device.DeviceId);
                else
                    failed.Add($"device {device.DeviceName ?? device.DeviceId}: {error}");
            }

            int updated = 0;
            errorString = string.Empty;
            if (!dal.SetCareDevicesAssignmentStatus(done, assignmentStatus, ref updated, ref errorString))
                failed.Add("care_devices could not be updated: " + errorString);

            summary = $"DICOM Enabler {enablerId} and {done.Count - 1} of {devices.Count} device(s) set to {assignmentStatus} in CARE" +
                      (failed.Count == 0 ? string.Empty : "; not done for " + string.Join("; ", failed));
            writeToLog(summary, failed.Count == 0);
            return failed.Count == 0;
        }

        private static bool ValidateDeviceId(string deviceId, Action<string, bool> writeToLog, out string error)
        {
            error = null;
            if (!string.IsNullOrWhiteSpace(deviceId) && Guid.TryParse(deviceId.Trim(), out _))
                return true;
            error = $"The device id must be a UUID, not '{deviceId}'.";
            writeToLog("CARE Modalities API not called: " + error, false);
            return false;
        }

        /// <summary>The host (and port) of a URL, for messages; the text itself when it is not a URL.</summary>
        private static string DescribeHost(string url)
        {
            return Uri.TryCreate(url ?? string.Empty, UriKind.Absolute, out Uri uri) ? uri.Authority : url;
        }

        /// <summary>modalitiesURL/{device_id}/ followed by suffix (e.g. "status/").</summary>
        private static string BuildDeviceURL(string modalitiesURL, string deviceId, string suffix)
        {
            string url = modalitiesURL ?? string.Empty;
            int query = url.IndexOf('?');
            if (query >= 0)
                url = url.Substring(0, query);
            return url.TrimEnd('/') + "/" + Uri.EscapeDataString(deviceId.Trim()) + "/" + (suffix ?? string.Empty);
        }

        /// <summary>
        /// Sends one request to the CARE Modalities API with the static API key, and a JSON body when
        /// jsonBody is set. Returns false with error set (and logged) when CARE cannot be reached or
        /// does not answer with a success status.
        /// </summary>
        /// <remarks>
        /// timeout: a shorter limit than the client's 30 seconds for this request (e.g. while Windows shuts
        /// down); the network check (gateway / internet pings) is then skipped on failure, as it takes time.
        /// </remarks>
        private static bool TrySend(HttpMethod method, string requestUrl, string token, string jsonBody, Action<string, bool> writeToLog,
                                    out string responseBody, out string error, TimeSpan? timeout = null)
        {
            responseBody = null;
            error = null;
            writeToLog($"CARE Modalities API: {method} {requestUrl}", true);

            HttpResponseMessage response;
            try
            {
                var request = new HttpRequestMessage(method, requestUrl);
                request.Headers.Add("Authorization", token ?? string.Empty);
                if (jsonBody != null)
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                using (var cancel = timeout.HasValue ? new System.Threading.CancellationTokenSource(timeout.Value) : new System.Threading.CancellationTokenSource())
                {
                    response = httpClient.SendAsync(request, cancel.Token).GetAwaiter().GetResult();
                    responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
            }
            catch (TaskCanceledException)
            {
                // HttpClient reports its timeout (and the shorter one given here) as a cancelled task
                double seconds = Math.Min(timeout?.TotalSeconds ?? double.MaxValue, httpClient.Timeout.TotalSeconds);
                error = $"CARE did not answer within {seconds:0.#} seconds ({DescribeHost(requestUrl)}). " +
                        (timeout.HasValue ? string.Empty : ucls_NetworkCheck.Describe() + ". ") + "Try again later.";
                writeToLog(error, false);
                return false;
            }
            catch (HttpRequestException ex)
            {
                string reason = ex.InnerException?.Message ?? ex.Message;
                error = $"Could not connect to CARE ({DescribeHost(requestUrl)}): {reason}." +
                        (timeout.HasValue ? string.Empty : " " + ucls_NetworkCheck.Describe() + ".");
                writeToLog(error, false);
                return false;
            }

            if (response.IsSuccessStatusCode)
                return true;

            error = $"CARE Modalities API returned {(int)response.StatusCode} ({response.ReasonPhrase})";
            string detail = DescribeErrorBody(responseBody);
            if (!string.IsNullOrEmpty(detail))
                error += ": " + detail;
            writeToLog($"{error}. Response body: {ucls_CareWorklist.Truncate(responseBody, 1000)}", false);
            // What to check, for the banners that show this error
            int status = (int)response.StatusCode;
            if (status == 401 || status == 403)
                error += ". The staticAPIKey in App.config does not match CARE_RADIOLOGY_WEBHOOK_SECRET on the CARE server.";
            else if (status == 404)
                error += ". Check careBackendURL / modalitiesURL in App.config and the Facility Id.";
            else if (status >= 500)
                error += ". CARE server error; try again later.";

            // A 401/403 is nearly always the token not matching the server's CARE_RADIOLOGY_WEBHOOK_SECRET
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                writeToLog(
                    $"Authorization rejected by CARE. Token in use: {ucls_CareWorklist.DescribeToken(token)}. Verify it matches CARE_RADIOLOGY_WEBHOOK_SECRET on the server.", false);
            }
            return false;
        }

        /// <summary>
        /// A saved device or DICOM Enabler as readable rows of { section, field, value }, from its care_devices
        /// columns: General, Modality Machine (a DICOM device), Location, Device Status, and the Modality
        /// Worklist SCP and Store SCP (a DICOM Enabler). Dates are in local time and modalities are shown as
        /// CARE returns them (e.g. DX); an empty value is shown as NotAvailable ("N/A").
        /// </summary>
        public static List<string[]> ReadableDetails(CareDeviceRecord device)
        {
            var rows = new List<string[]>();
            if (device == null)
                return rows;
            bool isEnabler = device.DeviceType == CareTypeDicomEnabler;
            void Add(string section, string field, string value) =>
                rows.Add(new[] { section, field, string.IsNullOrWhiteSpace(value) ? NotAvailable : value.Trim() });

            // Each section starts with its IDs
            Add("General", "ID", device.DeviceId);
            Add("General", "Device Name", device.DeviceName);
            Add("General", "Device Type", isEnabler ? "DICOM Enabler" : device.DeviceType == CareTypeDicom ? "DICOM Device" : device.DeviceType);
            if (!isEnabler)
            {
                Add("Modality Machine", "DICOM Enabler ID", device.DicomEnablerId);
                Add("Modality Machine", "Modality", device.Modality);
                Add("Modality Machine", "Station AE Title", device.AeTitle);
                Add("Modality Machine", "IP Address", device.Ip);
            }
            else
            {
                Add("General", "IP Address", device.Ip);
            }
            Add("Location", "ID", device.LocationId);
            Add("Location", "Name", device.LocationName);
            Add("Device Status", "Status", Capitalize(device.Status));
            Add("Device Status", "Availability", Capitalize(device.AvailabilityStatus));
            Add("Device Status", "Assignment", Capitalize(device.AssignmentStatus));
            Add("Device Status", "Last Synced", DateTimeOffset.TryParse(device.LastSyncedAt, out DateTimeOffset synced)
                                                    ? synced.ToLocalTime().ToString("dd-MMM-yyyy HH:mm:ss") : NotAvailable);
            if (isEnabler)
            {
                Add("Modality Worklist SCP", "AE Title", device.ModalityScpAeTitle);
                Add("Modality Worklist SCP", "Host", device.ModalityScpHost);
                Add("Modality Worklist SCP", "Port", device.ModalityScpPort?.ToString());
                Add("Store SCP", "AE Title", device.StoreScpAeTitle);
                Add("Store SCP", "Host", device.StoreScpHost);
                Add("Store SCP", "Port", device.StoreScpPort?.ToString());
            }
            Add("Saved", "Saved On", device.UpdatedTime?.ToString("dd-MMM-yyyy HH:mm:ss"));
            return rows;
        }

        private static string Capitalize(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? text : char.ToUpperInvariant(text.Trim()[0]) + text.Trim().Substring(1);
        }

        /// <summary>
        /// The readable part of a CARE error body: the msg of each entry in errors (a string, or an object of
        /// field: message, as for a 400), or detail (as for a 401). Null when the body has neither.
        /// </summary>
        private static string DescribeErrorBody(string responseBody)
        {
            try
            {
                JObject body = JObject.Parse(responseBody);
                var messages = new List<string>();
                if (body["errors"] is JArray errors)
                {
                    foreach (JToken entry in errors)
                    {
                        JToken msg = entry?["msg"];
                        if (msg is JObject fields)
                            messages.AddRange(fields.Properties().Select(p => $"{p.Name}: {FlattenMessage(p.Value)}"));
                        else if (msg != null)
                            messages.Add(FlattenMessage(msg));
                    }
                }
                if (messages.Count == 0 && body["detail"] != null)
                    messages.Add(FlattenMessage(body["detail"]));
                return messages.Count == 0 ? null : string.Join("; ", messages);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string FlattenMessage(JToken message)
        {
            if (message is JArray list)
                return string.Join(" ", list.Select(FlattenMessage));
            return message.Type == JTokenType.String ? (string)message : message.ToString(Formatting.None);
        }
    }

    public class CareModalitiesResponse
    {
        /// <summary>Total results across all pages, not just this page's.</summary>
        public int count { get; set; }
        public List<CareModality> results { get; set; }
    }

    /// <summary>
    /// A DICOM Enabler or a DICOM device. device_type says which fields are set: ip for a DICOM Enabler,
    /// machine and dicom_enabler for a DICOM device.
    /// </summary>
    public class CareModality
    {
        public string id { get; set; }
        public string device_name { get; set; }
        public string device_type { get; set; }
        public CareModalityLocation location { get; set; }
        public CareModalityDeviceStatus device_status { get; set; }
        public string assignment_status { get; set; }
        public DateTimeOffset? last_synced_at { get; set; }

        /// <summary>DICOM Enabler only: the enabler's IP address.</summary>
        public string ip { get; set; }

        /// <summary>DICOM Enabler only, from the retrieve (not the list): its Store SCP endpoint.</summary>
        public CareDicomEndpoint store_scp { get; set; }

        /// <summary>DICOM Enabler only, from the retrieve (not the list): its Modality Worklist SCP endpoint.</summary>
        public CareDicomEndpoint modality_scp { get; set; }

        /// <summary>DICOM device only: the modality machine's AE title, modality and IP address.</summary>
        public CareModalityMachine machine { get; set; }

        /// <summary>DICOM device only: id of the DICOM Enabler the device is connected to.</summary>
        public string dicom_enabler { get; set; }

        [JsonIgnore]
        public bool IsDicomEnabler => device_type == ucls_CareModalities.CareTypeDicomEnabler;

        [JsonIgnore]
        public bool IsDicomDevice => device_type == ucls_CareModalities.CareTypeDicom;

        [JsonIgnore]
        public bool IsAssigned => assignment_status == ucls_CareModalities.AssignmentAssigned;
    }

    public class CareModalityLocation
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    public class CareModalityDeviceStatus
    {
        public string status { get; set; }
        public string availability_status { get; set; }
    }

    public class CareDicomEndpoint
    {
        public string ae_title { get; set; }
        public string host { get; set; }
        public int? port { get; set; }
    }

    public class CareModalityMachine
    {
        public string ae_title { get; set; }
        public string modality { get; set; }
        public string ip { get; set; }
    }
}
