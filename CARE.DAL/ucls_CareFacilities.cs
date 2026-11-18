using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;

namespace Plexus.Common.Database
{
    /// <summary>
    /// Calls the CARE facility list API, which returns the facilities the service account (the token from
    /// Initializer.bat, see ucls_ServiceToken) has access to. The Configuration tab lists them by name; the
    /// selected one's id is saved as care_config facility_id, which the worklist, modalities, DICOM upload
    /// and scan status calls use.
    /// </summary>
    public static class ucls_CareFacilities
    {
        /// <summary>The facility list API path under careBackendURL.</summary>
        public const string DefaultFacilitiesPath = "/api/v1/facility/";

        /// <summary>Page size sent as limit.</summary>
        public const int PageSize = 100;

        private const string ApiName = "CARE Facilities API";

        /// <summary>The facility list API URL: facilitiesPath (DefaultFacilitiesPath when empty) under baseUrl.</summary>
        public static string BuildFacilitiesURL(string baseUrl, string facilitiesPath)
        {
            string path = string.IsNullOrWhiteSpace(facilitiesPath) ? DefaultFacilitiesPath : facilitiesPath.Trim();
            return (baseUrl ?? string.Empty).TrimEnd('/') + path;
        }

        /// <summary>
        /// Fetches every page of the facilities the service account can access, sorted by name. Returns false
        /// when the call fails or CARE rejects it; error then says why (it is also logged).
        /// </summary>
        public static bool TryFetchFacilities(string facilitiesURL, string token, Action<string, bool> writeToLog,
                                              out List<CareFacility> facilities, out string error)
        {
            facilities = null;
            error = null;
            var all = new List<CareFacility>();
            int offset = 0;
            try
            {
                while (true)
                {
                    string separator = facilitiesURL.Contains("?") ? "&" : "?";
                    string requestUrl = $"{facilitiesURL}{separator}limit={PageSize}&offset={offset}";
                    if (!ucls_CareModalities.TrySend(HttpMethod.Get, requestUrl, token, null, writeToLog,
                                                     out string responseBody, out error, apiName: ApiName))
                        return false;

                    CareFacilitiesResponse page;
                    try
                    {
                        page = JsonConvert.DeserializeObject<CareFacilitiesResponse>(responseBody);
                    }
                    catch (JsonException ex)
                    {
                        error = $"{ApiName} returned a response that could not be read: " + ex.Message;
                        writeToLog(error, false);
                        return false;
                    }
                    if (page == null)
                    {
                        error = $"{ApiName} returned an empty response.";
                        writeToLog(error, false);
                        return false;
                    }

                    int pageCount = page.results?.Count ?? 0;
                    if (pageCount > 0)
                        all.AddRange(page.results.Where(f => f != null && !string.IsNullOrWhiteSpace(f.id)));

                    offset += pageCount;
                    // An empty page ends the loop even if count says more, so a count that changed
                    // between calls cannot keep it requesting pages forever
                    if (pageCount == 0 || offset >= page.count)
                        break;
                }
            }
            catch (Exception ex)
            {
                error = "Fetching facilities from CARE failed with exception " + ex.Message;
                writeToLog(error, false);
                return false;
            }

            facilities = all
                .GroupBy(f => f.id.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(f => f.name ?? f.id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            writeToLog($"{ApiName} returned {facilities.Count} facilit{(facilities.Count == 1 ? "y" : "ies")} the service account can access", true);
            return true;
        }
    }

    public class CareFacilitiesResponse
    {
        /// <summary>Total results across all pages, not just this page's.</summary>
        public int count { get; set; }
        public List<CareFacility> results { get; set; }
    }

    /// <summary>The fields of a CARE facility the Configuration tab uses; the rest of the response is ignored.</summary>
    public class CareFacility
    {
        public string id { get; set; }
        public string name { get; set; }
        public string facility_type { get; set; }
    }
}
