using System;
using System.Security.Cryptography;
using System.Text;

namespace Plexus.Common.Database
{
    /// <summary>
    /// Initializer.bat asks for it and stores it in care_config (care_service_token), encrypted with Windows DPAPI for this
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
                error = "the CARE service token is not set. Run Initializer.bat as administrator to enter it.";
                return false;
            }
            if (!stored.StartsWith(ValuePrefix, StringComparison.Ordinal))
            {
                error = "the CARE service token in care_config is not in a recognised format. Run Initializer.bat as administrator to enter it again.";
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
                        "Run Initializer.bat as administrator to enter it again.";
                return false;
            }
            if (token == string.Empty)
            {
                error = "the CARE service token is empty. Run Initializer.bat as administrator to enter it.";
                return false;
            }
            return true;
        }
    }
}
