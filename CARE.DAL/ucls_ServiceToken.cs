using System;
using System.Security.Cryptography;
using System.Text;

namespace Plexus.Common.Database
{
    /// <summary>What care_config holds for the service token, without decrypting it into the caller's hands.</summary>
    public enum ServiceTokenStatus
    {
        /// <summary>No token stored.</summary>
        NotSet,
        /// <summary>A token is stored and decrypts on this machine.</summary>
        Set,
        /// <summary>A value is stored but is not one this machine can decrypt (e.g. a database copied from another machine).</summary>
        Unreadable,
        /// <summary>care_config could not be read.</summary>
        Error
    }

    /// <summary>
    /// Initializer.bat asks for it (or the Configuration tab replaces it) and stores it in care_config (care_service_token), encrypted with Windows DPAPI for this
    /// machine. Only processes on this machine can decrypt it - the services (LocalSystem) and the UI (run as
    /// administrator) - and a copy of the database taken to another machine cannot. It is decrypted each time
    /// a request needs it and never kept or logged.
    ///
    /// Initializer.ps1 encrypts it the same way (Protect-ServiceToken); keep the two in step.
    /// </summary>
    public static class ucls_ServiceToken
    {
        /// <summary>The care_config setting holding the encrypted token.</summary>
        public const string ConfigKey = "care_service_token";

        /// <summary>Marks a value encrypted by this scheme, so a later scheme can be told apart.</summary>
        public const string ValuePrefix = "dpapi:";

        /// <summary>Authorization scheme CARE expects for a service-account token.</summary>
        public const string AuthScheme = "Token";

        // Extra input to DPAPI: other software on this machine calling DPAPI without it cannot decrypt the token
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CARE DICOM Enabler service token v1");

        /// <summary>
        /// The Authorization header value ("Token &lt;token&gt;") for a CARE request, read from care_config and
        /// decrypted now. False with error set (a message that says what to do) when the token is not set,
        /// cannot be read, or cannot be decrypted on this machine.
        /// </summary>
        public static bool TryGetAuthorizationHeader(ucls_DAL dal, out string header, out string error)
        {
            header = null;
            if (!TryGetToken(dal, out string token, out error))
                return false;
            header = AuthScheme + " " + token;
            return true;
        }

        /// <summary>As TryGetAuthorizationHeader, with its own database connection to the database configured under applicationPath.</summary>
        public static bool TryGetAuthorizationHeader(string applicationPath, out string header, out string error)
        {
            ucls_DAL dal = null;
            try
            {
                dal = new ucls_DAL(applicationPath);
                return TryGetAuthorizationHeader(dal, out header, out error);
            }
            catch (Exception ex)
            {
                header = null;
                error = "the CARE service token could not be read: " + ex.Message;
                return false;
            }
            finally
            {
                dal?.Dispose();
            }
        }

        /// <summary>
        /// Whether a usable token is stored. The token itself is decrypted only to check it and is not returned,
        /// so the Configuration tab can say a token is set without ever being able to show it.
        /// </summary>
        public static ServiceTokenStatus GetStatus(ucls_DAL dal, out string error)
        {
            error = null;
            string errorString = string.Empty;
            string stored = dal.GetConfigValue(ConfigKey, string.Empty, ref errorString);
            if (!string.IsNullOrEmpty(errorString))
            {
                error = $"the CARE service token could not be read: {errorString}";
                return ServiceTokenStatus.Error;
            }
            if (string.IsNullOrWhiteSpace(stored))
                return ServiceTokenStatus.NotSet;
            if (!TryGetToken(dal, out string token, out error))
                return ServiceTokenStatus.Unreadable;
            token = null;
            return ServiceTokenStatus.Set;
        }

        /// <summary>
        /// The token as it is stored, from what the user entered: trimmed, without a pasted "Token " scheme.
        /// False with error set when it is not one word of printable ASCII characters. Same rules as
        /// ConvertTo-ServiceToken in Initializer.ps1.
        /// </summary>
        public static bool TryNormalize(string text, out string token, out string error)
        {
            token = null;
            error = null;
            string value = (text ?? string.Empty).Trim();
            string schemePrefix = AuthScheme + " ";
            if (value.StartsWith(schemePrefix, StringComparison.OrdinalIgnoreCase))
                value = value.Substring(schemePrefix.Length).Trim();
            if (value == string.Empty)
            {
                error = "Enter the service account token.";
                return false;
            }
            foreach (char c in value)
            {
                if (c < '\x21' || c > '\x7E')
                {
                    error = "The token must be one word of printable characters, without spaces.";
                    return false;
                }
            }
            token = value;
            return true;
        }

        /// <summary>
        /// Encrypts token for this machine and stores it in care_config, replacing any token already set, then
        /// checks it reads back. token must already be normalised (TryNormalize). The services use it from their
        /// next CARE call; nothing needs restarting.
        /// </summary>
        public static bool TrySave(ucls_DAL dal, string token, out string error)
        {
            error = null;
            byte[] plain = Encoding.UTF8.GetBytes(token);
            string stored;
            try
            {
                stored = ValuePrefix + Convert.ToBase64String(ProtectedData.Protect(plain, Entropy, DataProtectionScope.LocalMachine));
            }
            catch (CryptographicException ex)
            {
                error = "the token could not be encrypted on this machine: " + ex.Message;
                return false;
            }
            finally
            {
                Array.Clear(plain, 0, plain.Length);
            }

            string errorString = string.Empty;
            if (!dal.SaveConfig(new System.Collections.Generic.Dictionary<string, string> { { ConfigKey, stored } }, ref errorString))
            {
                error = errorString;
                return false;
            }

            // SaveConfig only updates an existing row: a database without the care_service_token row (not
            // upgraded by Initializer.bat) would silently keep nothing
            string readBack = dal.GetConfigValue(ConfigKey, string.Empty, ref errorString);
            if (!string.IsNullOrEmpty(errorString))
            {
                error = "the token was saved but could not be read back: " + errorString;
                return false;
            }
            if (readBack != stored)
            {
                error = "care_config has no care_service_token setting, so the token was not stored. Run Initializer.bat as administrator to update the database.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Describes a token without revealing it: its length plus a short SHA-256 fingerprint, the same one
        /// Initializer.bat shows and the services log when CARE rejects the token.
        /// </summary>
        public static string Describe(string token)
        {
            return ucls_CareWorklist.DescribeToken(token);
        }

        private static bool TryGetToken(ucls_DAL dal, out string token, out string error)
        {
            token = null;
            error = null;
            string errorString = string.Empty;
            string stored = dal.GetConfigValue(ConfigKey, string.Empty, ref errorString);
            if (!string.IsNullOrEmpty(errorString))
            {
                error = $"the CARE service token could not be read: {errorString}";
                return false;
            }
            if (string.IsNullOrWhiteSpace(stored))
            {
                error = "the CARE service token is not set. Enter it under Configuration > CARE Settings, or run Initializer.bat as administrator.";
                return false;
            }
            if (!stored.StartsWith(ValuePrefix, StringComparison.Ordinal))
            {
                error = "the CARE service token in care_config is not in a recognised format. Enter it again under Configuration > CARE Settings, or run Initializer.bat as administrator.";
                return false;
            }
            try
            {
                byte[] cipher = Convert.FromBase64String(stored.Substring(ValuePrefix.Length));
                byte[] plain = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.LocalMachine);
                try
                {
                    token = Encoding.UTF8.GetString(plain).Trim();
                }
                finally
                {
                    Array.Clear(plain, 0, plain.Length);
                }
            }
            catch (Exception ex) when (ex is CryptographicException || ex is FormatException)
            {
                // Typically a database restored from another machine: DPAPI ties the value to the machine that encrypted it
                error = "the CARE service token could not be decrypted on this machine (was the database copied from another machine?). " +
                        "Enter it again under Configuration > CARE Settings, or run Initializer.bat as administrator.";
                return false;
            }
            if (token == string.Empty)
            {
                error = "the CARE service token is empty. Enter it under Configuration > CARE Settings, or run Initializer.bat as administrator.";
                return false;
            }
            return true;
        }
    }
}
