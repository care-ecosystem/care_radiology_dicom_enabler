using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
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

        /// <summary>
        /// Decrypts a careServiceToken App.config value (the build's AES encryption). Returns empty when it is
        /// blank or cannot be decrypted (not encrypted with this build's key); the reason is returned in errorString.
        /// </summary>
        private static string DecryptToken(string encryptedToken, ref string errorString)
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

        // ---- Token storage on the device (Windows DPAPI) ----------------------------------------------
        //
        // The build bakes the token into the MWL and SCU service .exe.config files encrypted with the AES key
        // in EncKey.cs. That key ships in the source and the binaries, so the baked value is only protected
        // until it reaches a device. The first process that finds it (either service, or the Configuration
        // tab) re-encrypts it with DPAPI into cfg\care_service_token.dat and blanks the baked value.
        //
        // DPAPI uses LocalMachine scope: the services run as SYSTEM and the Configuration tab as the logged-in
        // user, and both must decrypt it (CurrentUser scope would only let the account that wrote it decrypt it).
        // A LocalMachine value only decrypts on the Windows install that wrote it, so a copied install folder or
        // config file is useless on another machine. Who can read the file on this machine is limited by its
        // ACL: the user that wrote it and Administrators only (SYSTEM, which runs the services, is a member of
        // Administrators), with no inherited access.
        //
        // Installing a new build (a rotated token) bakes a value again, which replaces the stored token.

        private const string ServiceTokenFileName = "care_service_token.dat";
        private static readonly string[] ServiceConfigFiles = { "CARE_MWL_Service.exe.config", "CARE_SCU_Service.exe.config" };

        // Extra input DPAPI requires on decrypt, so other software calling DPAPI on the machine with no
        // entropy cannot decrypt the file by accident. It is not a secret.
        private static readonly byte[] DpapiEntropy = Encoding.UTF8.GetBytes("CARE DICOM Enabler service account token v1");

        private static readonly object serviceTokenLock = new object();
        private static string cachedServiceToken;

        /// <summary>
        /// The CARE service account token for this device: moves a token baked into the service configs into
        /// DPAPI storage first, then returns the stored token. Returns empty and sets errorString when there is
        /// none or it cannot be read. A token that was read is cached for the life of the process (installing a
        /// new build restarts the services).
        /// </summary>
        public static string LoadServiceToken(Action<string, bool> writeToLog, ref string errorString)
        {
            lock (serviceTokenLock)
            {
                if (!string.IsNullOrEmpty(cachedServiceToken))
                    return cachedServiceToken;

                string installFolder = AppDomain.CurrentDomain.BaseDirectory;
                string tokenPath = Path.Combine(installFolder, "cfg", ServiceTokenFileName);

                MoveBakedTokenToStore(installFolder, tokenPath, writeToLog);

                if (!File.Exists(tokenPath))
                {
                    errorString = "No CARE service account token on this device: careServiceToken is not set in the service configs and " +
                                  tokenPath + " does not exist. Rebuild with the CARE_SERVICE_TOKEN secret set and reinstall";
                    return string.Empty;
                }

                try
                {
                    byte[] plain = ProtectedData.Unprotect(ReadAllBytesWithRetry(tokenPath), DpapiEntropy, DataProtectionScope.LocalMachine);
                    string token = Encoding.UTF8.GetString(plain).Trim();
                    Array.Clear(plain, 0, plain.Length);
                    if (string.IsNullOrEmpty(token))
                    {
                        errorString = tokenPath + " holds an empty token. Reinstall the build to store the token again";
                        return string.Empty;
                    }
                    cachedServiceToken = token;
                    return token;
                }
                catch (UnauthorizedAccessException)
                {
                    errorString = "Access to " + tokenPath + " is denied. Only the user that stored the token and Administrators can read it: run the application as Administrator";
                }
                catch (CryptographicException ex)
                {
                    errorString = tokenPath + " cannot be decrypted on this machine (it was written on another Windows install, or is damaged): " +
                                  ex.Message + ". Reinstall the build to store the token again";
                }
                catch (Exception ex)
                {
                    errorString = "Reading the CARE service account token from " + tokenPath + " failed: " + ex.Message;
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// The CARE service account token, which is mandatory. Returns empty and sets errorString when there is
        /// none or it cannot be read. The services call this on start and refuse to run without a token.
        /// </summary>
        public static string RequireServiceToken(Action<string, bool> writeToLog, ref string errorString)
        {
            string serviceToken = LoadServiceToken(writeToLog, ref errorString);
            if (string.IsNullOrEmpty(serviceToken) && string.IsNullOrEmpty(errorString))
                errorString = "The CARE service account token is mandatory: rebuild with the CARE_SERVICE_TOKEN secret set and reinstall";
            return serviceToken;
        }

        /// <summary>
        /// Moves a token baked into the service configs into DPAPI storage and blanks it there. A config that cannot
        /// be decrypted or written is left as it is and logged; the next process with access completes the move.
        /// </summary>
        private static void MoveBakedTokenToStore(string installFolder, string tokenPath, Action<string, bool> writeToLog)
        {
            string storedToken = null;
            foreach (string configFile in ServiceConfigFiles)
            {
                string configPath = Path.Combine(installFolder, configFile);
                try
                {
                    if (!File.Exists(configPath))
                        continue;

                    var configDoc = new XmlDocument { PreserveWhitespace = true };
                    configDoc.Load(configPath);
                    var node = configDoc.SelectSingleNode("/configuration/appSettings/add[@key='careServiceToken']") as XmlElement;
                    string baked = node?.GetAttribute("value");
                    if (string.IsNullOrWhiteSpace(baked))
                        continue;

                    string decryptError = string.Empty;
                    string token = DecryptToken(baked, ref decryptError);
                    if (string.IsNullOrEmpty(token))
                    {
                        writeToLog?.Invoke($"{configFile}: {(string.IsNullOrEmpty(decryptError) ? "careServiceToken is empty" : decryptError)}. Left in place", false);
                        continue;
                    }

                    if (storedToken == null)
                    {
                        WriteProtectedToken(tokenPath, token);
                        storedToken = token;
                        writeToLog?.Invoke($"CARE service account token from {configFile} stored with DPAPI in {tokenPath}", true);
                    }
                    else if (storedToken != token)
                    {
                        writeToLog?.Invoke($"{configFile} has a different careServiceToken from {ServiceConfigFiles[0]}; the first one was stored. Reinstall the build so both match", false);
                    }

                    node.SetAttribute("value", string.Empty);
                    configDoc.Save(configPath);
                    writeToLog?.Invoke($"careServiceToken blanked in {configFile}", true);
                }
                catch (Exception ex)
                {
                    writeToLog?.Invoke($"Moving careServiceToken from {configFile} to DPAPI storage failed: {ex.Message}. It is retried on the next start", false);
                }
            }
        }

        /// <summary>
        /// Writes the DPAPI-encrypted token to a temporary file created with an ACL of the current user and
        /// Administrators only (no inherited access), then swaps it into place, so the token is never in a file
        /// anyone else can read.
        /// </summary>
        private static void WriteProtectedToken(string tokenPath, string token)
        {
            byte[] plain = Encoding.UTF8.GetBytes(token);
            byte[] encrypted;
            try
            {
                encrypted = ProtectedData.Protect(plain, DpapiEntropy, DataProtectionScope.LocalMachine);
            }
            finally
            {
                Array.Clear(plain, 0, plain.Length);
            }

            var security = new FileSecurity();
            security.SetAccessRuleProtection(true, false);   // no inherited access from the install folder
            using (WindowsIdentity currentUser = WindowsIdentity.GetCurrent())
                security.AddAccessRule(new FileSystemAccessRule(currentUser.User, FileSystemRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, AccessControlType.Allow));

            Directory.CreateDirectory(Path.GetDirectoryName(tokenPath));
            string tempPath = tokenPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileSystemRights.Write, FileShare.None, 4096, FileOptions.WriteThrough, security))
                    stream.Write(encrypted, 0, encrypted.Length);

                // Both services can start at once and store the same token; the last swap wins
                for (int attempt = 1; ; attempt++)
                {
                    try
                    {
                        if (File.Exists(tokenPath))
                            File.Replace(tempPath, tokenPath, null);
                        else
                            File.Move(tempPath, tokenPath);
                        break;
                    }
                    catch (IOException) when (attempt < 5)
                    {
                        Thread.Sleep(200);
                    }
                }
                File.SetAccessControl(tokenPath, security);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        private static byte[] ReadAllBytesWithRetry(string path)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return File.ReadAllBytes(path);
                }
                catch (IOException) when (attempt < 5)
                {
                    Thread.Sleep(200);
                }
            }
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
