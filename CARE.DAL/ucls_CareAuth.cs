using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Plexus.Common.Database
{
    /// <summary>
    /// CARE service account token support.
    /// </summary>
    public static class ucls_CareAuth
    {
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        private const int FacilityPageSize = 100;

        /// <summary>Encrypts a service account token for storing in App.config.</summary>
        public static string EncryptToken(string plainToken)
        {
            return ucls_EnDcryption.EncryptString(EncKey.encdeKey, (plainToken ?? string.Empty).Trim());
        }

        /// <summary>
        /// Decrypts the careServiceToken App.config value. Returns empty when it is blank or cannot be
        /// decrypted (not encrypted with this build's key); the reason is returned in errorString.
        /// </summary>
        public static string DecryptToken(string encryptedToken, ref string errorString)
        {
            if (string.IsNullOrWhiteSpace(encryptedToken))
                return string.Empty;

            try
            {
                return ucls_EnDcryption.DecryptString(EncKey.encdeKey, encryptedToken.Trim()).Trim();
            }
            catch (Exception ex)
            {
                errorString = "careServiceToken could not be decrypted (it must be the encrypted value written by the build): " + ex.Message;
                return string.Empty;
            }
        }

        /// <summary>
        /// Decrypts the careServiceToken App.config value, which is mandatory. Returns empty and sets
        /// errorString when it is blank or cannot be decrypted. The services call this on start and refuse
        /// to run without a token.
        /// </summary>
        public static string RequireServiceToken(string encryptedToken, ref string errorString)
        {
            string serviceToken = DecryptToken(encryptedToken, ref errorString);
            if (string.IsNullOrEmpty(serviceToken) && string.IsNullOrEmpty(errorString))
                errorString = "careServiceToken is not configured in App.config. The CARE service account token is mandatory: rebuild with the CARE_SERVICE_TOKEN secret set";
            return serviceToken;
        }

        /// <summary>
        /// Lists the facilities the service account can access (CARE facility list API, all pages).
        /// A name limits the list to facilities whose name contains it (CARE ?name= filter, case-insensitive);
        /// blank lists them all. Returns null and sets errorString when the token is rejected or CARE cannot be reached.
        /// </summary>
        public static List<CareFacilityInfo> FetchFacilities(string baseUrl, string serviceToken, string name, ref string errorString)
        {
            var facilities = new List<CareFacilityInfo>();
            try
            {
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    errorString = "CARE server URL is not configured.";
                    return null;
                }
                if (string.IsNullOrWhiteSpace(serviceToken))
                {
                    errorString = "No service account token is configured.";
                    return null;
                }

                int offset = 0;
                int count;
                do
                {
                    string url = baseUrl.TrimEnd('/') + "/api/v1/facility/?limit=" + FacilityPageSize + "&offset=" + offset;
                    if (!string.IsNullOrWhiteSpace(name))
                        url += "&name=" + Uri.EscapeDataString(name.Trim());
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Authorization", "Token " + serviceToken.Trim());

                    HttpResponseMessage response = Task.Run(() => httpClient.SendAsync(request)).GetAwaiter().GetResult();
                    string body = Task.Run(() => response.Content.ReadAsStringAsync()).GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        errorString = response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden
                            ? $"CARE rejected the service account token ({(int)response.StatusCode} {response.ReasonPhrase})."
                            : $"CARE facility list failed: {(int)response.StatusCode} {response.ReasonPhrase}.";
                        return null;
                    }

                    CareFacilityPage page = JsonConvert.DeserializeObject<CareFacilityPage>(body);
                    if (page?.results == null || page.results.Count == 0)
                        break;

                    facilities.AddRange(page.results);
                    offset += page.results.Count;
                    count = page.count;
                } while (offset < count);

                facilities.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.CurrentCultureIgnoreCase));
                return facilities;
            }
            catch (Exception ex)
            {
                errorString = "Could not get the facilities from CARE: " + ex.Message;
                return null;
            }
        }
    }

    public class CareFacilityInfo
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    public class CareFacilityPage
    {
        public int count { get; set; }
        public List<CareFacilityInfo> results { get; set; }
    }
}
