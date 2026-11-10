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

        /// <summary>One page of the DICOM Enablers that are not assigned yet, for the user to pick from.</summary>
        public static bool TryFetchUnassignedDicomEnablersPage(string modalitiesURL, string token, int offset, Action<string, bool> writeToLog,
                                                               out CareModalitiesResponse page, out string error)
        {
            return TryFetchModalitiesPage(modalitiesURL, token, CareTypeDicomEnabler, AssignmentUnassigned, null, offset, writeToLog, out page, out error);
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
                AttachRawJson(page, responseBody);
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
        /// Sets each result's RawJson to its entry as CARE sent it (dates unparsed), so fields this client
        /// does not know about can still be shown.
        /// </summary>
        private static void AttachRawJson(CareModalitiesResponse page, string responseBody)
        {
            if (page?.results == null)
                return;
            using (var reader = new JsonTextReader(new System.IO.StringReader(responseBody)) { DateParseHandling = DateParseHandling.None })
            {
                if (!(JToken.ReadFrom(reader) is JObject body) || !(body["results"] is JArray rawResults) || rawResults.Count != page.results.Count)
                    return;
                for (int i = 0; i < rawResults.Count; i++)
                {
                    if (page.results[i] != null)
                        page.results[i].RawJson = rawResults[i].ToString(Formatting.None);
                }
            }
        }

        /// <summary>
        /// GET .../external/modalities/{device_id}/: the full details of a DICOM Enabler (including its
        /// DICOM endpoints) or a DICOM device. modality has the summary fields; detailsJson is the response
        /// as CARE sent it, so fields this client does not know about are kept too. A DICOM Enabler adds its
        /// DICOM endpoints (store_scp, modality_scp); a DICOM device is returned as in the list.
        /// Only devices that are active, available and unassigned can be retrieved (CARE returns 400 for
        /// any other, saying why), so a DICOM Enabler is retrieved before it is assigned.
        /// Returns false when the call fails or CARE rejects it; error then says why (it is also logged).
        /// </summary>
        public static bool TryRetrieveModality(string modalitiesURL, string token, string deviceId, Action<string, bool> writeToLog,
                                               out CareModality modality, out string detailsJson, out string error)
        {
            modality = null;
            detailsJson = null;
            if (!ValidateDeviceId(deviceId, writeToLog, out error))
                return false;

            string requestUrl = BuildDeviceURL(modalitiesURL, deviceId, null);
            if (!TrySend(HttpMethod.Get, requestUrl, token, null, writeToLog, out string responseBody, out error))
                return false;

            try
            {
                modality = JsonConvert.DeserializeObject<CareModality>(responseBody);
                detailsJson = (responseBody ?? string.Empty).Trim();
            }
            catch (JsonException ex)
            {
                modality = null;
                error = "CARE Modalities API returned details that could not be read: " + ex.Message;
                writeToLog($"{error}. Response body: {ucls_CareWorklist.Truncate(responseBody, 1000)}", false);
                return false;
            }
            writeToLog($"CARE Modalities API returned the details of {modality?.device_type ?? "device"} {deviceId} ({modality?.device_name})", true);
            return true;
        }

        /// <summary>
        /// The DICOM endpoints (modality_scp, store_scp) from a DICOM Enabler's details JSON (the retrieve
        /// response). These are the only source of the Modality Worklist SCP and Store SCP AE titles and
        /// ports. Returns false with error set when either is missing, has no AE title or a port outside
        /// 1-65535.
        /// </summary>
        public static bool TryGetDicomEndpoints(string detailsJson, out CareDicomEndpoint modalityScp, out CareDicomEndpoint storeScp, out string error)
        {
            modalityScp = null;
            storeScp = null;
            error = null;
            if (string.IsNullOrWhiteSpace(detailsJson))
            {
                error = "No DICOM Enabler details are saved.";
                return false;
            }

            CareModality enabler;
            try
            {
                enabler = JsonConvert.DeserializeObject<CareModality>(detailsJson);
            }
            catch (JsonException ex)
            {
                error = "The DICOM Enabler details could not be read: " + ex.Message;
                return false;
            }

            error = DescribeEndpointProblem("modality_scp", enabler?.modality_scp) ?? DescribeEndpointProblem("store_scp", enabler?.store_scp);
            if (error != null)
                return false;
            modalityScp = enabler.modality_scp;
            storeScp = enabler.store_scp;
            return true;
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
                                                     Action<string, bool> writeToLog, out string error)
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
            if (!TrySend(patch, requestUrl, token, payload, writeToLog, out _, out error))
                return false;

            writeToLog($"CARE Modalities API set assignment_status of {deviceId} to {assignmentStatus}", true);
            return true;
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
        private static bool TrySend(HttpMethod method, string requestUrl, string token, string jsonBody, Action<string, bool> writeToLog,
                                    out string responseBody, out string error)
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
                response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                error = "Could not connect to the CARE Modalities API: " + ex.Message;
                writeToLog(error + ". " + ucls_NetworkCheck.Describe(), false);
                return false;
            }

            if (response.IsSuccessStatusCode)
                return true;

            error = $"CARE Modalities API returned {(int)response.StatusCode} ({response.ReasonPhrase})";
            string detail = DescribeErrorBody(responseBody);
            if (!string.IsNullOrEmpty(detail))
                error += ": " + detail;
            writeToLog($"{error}. Response body: {ucls_CareWorklist.Truncate(responseBody, 1000)}", false);

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
        /// The modality code with its name for the common ones, e.g. "CT Scan (CT)", "X-Ray (DX)".
        /// Any other code is shown as it is.
        /// </summary>
        public static string DescribeModality(string modality)
        {
            if (string.IsNullOrWhiteSpace(modality))
                return string.Empty;
            string code = modality.Trim().ToUpperInvariant();
            string name;
            switch (code)
            {
                case "CT": name = "CT Scan"; break;
                case "CR":
                case "DX": name = "X-Ray"; break;
                case "MR": name = "MRI"; break;
                case "US": name = "Ultrasound"; break;
                case "MG": name = "Mammography"; break;
                case "NM": name = "Nuclear Medicine"; break;
                case "PT": name = "PET"; break;
                case "XA": name = "Angiography"; break;
                case "RF": name = "Fluoroscopy"; break;
                case "IO": name = "Dental X-Ray"; break;
                default: return modality.Trim();
            }
            return $"{name} ({code})";
        }

        /// <summary>
        /// A details JSON object (a device or DICOM Enabler as CARE returned it) as readable rows of
        /// { section, field, value }: top-level values under "General", each nested object (e.g.
        /// machine, store_scp) as its own section, with labels such as "AE Title" instead of field
        /// names, dates in local time and modality codes with their names. Fields this client does not
        /// know about are shown too. Empty when detailsJson is blank or not JSON.
        /// </summary>
        public static List<string[]> ReadableDetails(string detailsJson)
        {
            var rows = new List<string[]>();
            if (string.IsNullOrWhiteSpace(detailsJson))
                return rows;
            JToken root;
            try
            {
                // Dates are kept as CARE sent them and converted to local time below
                using (var reader = new JsonTextReader(new System.IO.StringReader(detailsJson)) { DateParseHandling = DateParseHandling.None })
                    root = JToken.ReadFrom(reader);
            }
            catch (JsonException)
            {
                return rows;
            }
            if (!(root is JObject obj))
                return rows;

            // General first, then each nested object in the order CARE sent it
            foreach (JProperty property in obj.Properties().Where(p => !(p.Value is JObject) && !(p.Value is JArray)))
                rows.Add(new[] { "General", ReadableLabel(property.Name), ReadableValue(property.Name, property.Value) });
            foreach (JProperty property in obj.Properties().Where(p => p.Value is JObject || p.Value is JArray))
                AppendReadable(property.Value, ReadableLabel(property.Name), string.Empty, rows);
            return rows;
        }

        private static void AppendReadable(JToken token, string section, string prefix, List<string[]> rows)
        {
            if (token is JObject obj)
            {
                if (!obj.HasValues)
                    rows.Add(new[] { section, prefix == string.Empty ? "-" : prefix, "-" });
                foreach (JProperty property in obj.Properties())
                {
                    string label = prefix == string.Empty ? ReadableLabel(property.Name) : prefix + " > " + ReadableLabel(property.Name);
                    if (property.Value is JObject || property.Value is JArray)
                        AppendReadable(property.Value, section, label, rows);
                    else
                        rows.Add(new[] { section, label, ReadableValue(property.Name, property.Value) });
                }
            }
            else if (token is JArray array)
            {
                if (array.Count == 0)
                    rows.Add(new[] { section, prefix == string.Empty ? "Items" : prefix, "None" });
                for (int i = 0; i < array.Count; i++)
                {
                    string label = (prefix == string.Empty ? string.Empty : prefix + " > ") + "Item " + (i + 1);
                    if (array[i] is JObject || array[i] is JArray)
                        AppendReadable(array[i], section, label, rows);
                    else
                        rows.Add(new[] { section, label, ReadableValue(null, array[i]) });
                }
            }
        }

        private static readonly Dictionary<string, string> ReadableLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "id", "ID" },
            { "ip", "IP Address" },
            { "ae_title", "AE Title" },
            { "device_status", "Device Status" },
            { "availability_status", "Availability" },
            { "assignment_status", "Assignment" },
            { "last_synced_at", "Last Synced" },
            { "dicom_enabler", "DICOM Enabler ID" },
            { "store_scp", "Store SCP" },
            { "modality_scp", "Modality Worklist SCP" },
            { "machine", "Modality Machine" },
        };

        private static readonly HashSet<string> Acronyms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id", "ip", "ae", "scp", "scu", "dicom", "uhid", "url", "uid", "mwl"
        };

        /// <summary>A field name as a label, e.g. "device_name" as "Device Name", "ae_title" as "AE Title".</summary>
        private static string ReadableLabel(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            if (ReadableLabels.TryGetValue(name, out string label))
                return label;
            return string.Join(" ", name.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => Acronyms.Contains(word) ? word.ToUpperInvariant() : char.ToUpperInvariant(word[0]) + word.Substring(1)));
        }

        /// <summary>A value as text: "-" for none, Yes / No, dates in local time, modality codes with their names.</summary>
        private static string ReadableValue(string name, JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
                return "-";
            if (value.Type == JTokenType.Boolean)
                return (bool)value ? "Yes" : "No";
            string text = value.Type == JTokenType.String ? (string)value : value.ToString(Formatting.None);
            if (string.IsNullOrWhiteSpace(text))
                return "-";
            if (string.Equals(name, "modality", StringComparison.OrdinalIgnoreCase))
                return DescribeModality(text);
            if (string.Equals(name, "device_type", StringComparison.OrdinalIgnoreCase))
                return text == CareTypeDicomEnabler ? "DICOM Enabler" : text == CareTypeDicom ? "DICOM Device" : text;
            if (value.Type == JTokenType.String && text.Length >= 19 && text[4] == '-' && text[10] == 'T' &&
                DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTimeOffset date))
                return date.ToLocalTime().ToString("dd-MMM-yyyy HH:mm:ss");
            if (value.Type == JTokenType.String && (name ?? string.Empty).EndsWith("status", StringComparison.OrdinalIgnoreCase))
                return char.ToUpperInvariant(text[0]) + text.Substring(1);
            return text;
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

        /// <summary>The list entry as CARE sent it (set for list results only).</summary>
        [JsonIgnore]
        public string RawJson { get; set; }

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
