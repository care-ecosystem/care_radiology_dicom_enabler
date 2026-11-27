using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Plexus.Common.Database
{
    /// <summary>
    /// Turns a CARE API error response into the one-line reason the logs show, and holds the detail logging
    /// that only a Debug build writes. The CARE radiology APIs answer an error with
    /// {"errors": [{"type": ..., "msg": ...}]} (emr_exception_handler: 400 validation errors, 404 not found)
    /// or {"detail": ...} (Django REST framework: 401 invalid or missing service token, 403 no permission,
    /// 429 throttled).
    /// </summary>
    public static class ucls_CareApiError
    {
        /// <summary>
        /// "CARE worklist API returned 401 (Unauthorized): Invalid token.", followed by what the operator should
        /// check to fix it. The body is not repeated; Debug builds log it with Detail.
        /// </summary>
        public static string Describe(string apiName, HttpStatusCode statusCode, string reasonPhrase, string responseBody)
        {
            int status = (int)statusCode;
            string reason = $"{apiName} returned {status} ({reasonPhrase})";
            string message = DescribeBody(responseBody);
            if (!string.IsNullOrEmpty(message))
                reason += ": " + message;
            string hint = Hint(status, responseBody);
            return hint == null ? reason : reason + ". " + hint;
        }

        private const string EnterToken = "enter it under Configuration > CARE Settings or run Initializer.bat as administrator";
        private const string AskForRole = "Ask the CARE admin to give the service account a role in this facility's organization with the permission ";

        // The CARE radiology backend's own error messages and error types (care_radiology security/authentication.py,
        // api/viewsets/external.py, services/modality_service.py, services/dicom_service.py; DRF token
        // authentication), each with what the operator should check. Matched in order, case-insensitively,
        // against the response body, before the status fallback in Hint.
        private static readonly (string Key, string Hint)[] KnownErrors =
        {
            // Token (401 on the radiology APIs, 403 on /api/v1/facility/)
            ("Service account token is required", "No service-account token was sent: " + EnterToken),
            ("Invalid token header", "The saved service-account token is malformed: enter it again under Configuration > CARE Settings or run Initializer.bat as administrator"),
            ("Invalid token.", "CARE does not know this service-account token (regenerated, revoked or mistyped): get the current token from the CARE admin and " + EnterToken),
            ("User inactive or deleted", "The service account is deactivated or deleted in CARE: ask the CARE admin to activate it, or to issue a new service-account token"),
            ("Invalid service account token", "The token is not a service account's (e.g. a normal user's token) or the account is deleted: ask the CARE admin for a service-account token and " + EnterToken),
            ("Authentication credentials were not provided", "No service-account token was sent: " + EnterToken),

            // Permissions (403)
            ("permission to read worklist", AskForRole + "\"Can Read DICOM Worklist\" (by default Facility Admin, Admin, Doctor, Nurse or Staff)"),
            ("permission to upload DICOM studies", AskForRole + "\"Can Upload DICOM Study\" (by default Facility Admin or Admin)"),
            ("permission to update imaging study status", AskForRole + "\"Can Update DICOM Study and Modality Status\" (by default Facility Admin or Admin)"),
            ("permission to update the status of this device", AskForRole + "\"Can Update DICOM Study and Modality Status\", in an organization that manages this device"),
            ("permission to read this device", "Ask the CARE admin to give the service account a role that can list devices in the organization of this device or its location"),

            // Facility, DICOM Enabler and devices
            ("No Facility matches", "The facility selected in the Configuration tab is not in CARE (deleted, or another CARE server's): select the facility again in the Configuration tab and save"),
            ("Must be a valid DICOM Enabler id", "Select the DICOM Enabler again in the Configuration tab"),
            ("No DICOM Enabler with this id", "The saved DICOM Enabler is not a DICOM Enabler of this facility in CARE (deleted, another facility's, or a modality's id): select the DICOM Enabler again in the Configuration tab"),
            ("No DICOM Enabler or DICOM device with this id", "The device was deleted in CARE or is not in this facility: click Sync on the Devices tab"),
            ("No Device matches", "The device was deleted in CARE or is not in this facility: click Sync on the Devices tab"),
            ("not active and available", "The device is not Active and Available in CARE: click Sync on the Devices tab to try again; if it still fails, contact the CARE team"),

            // Worklist and scan status
            ("Must be a date", "Check care_from_date in the Configuration tab: it must be YYYY-MM-DD or YYYY-MM-DD HH:MM:SS"),
            ("No ServiceRequest matches", "The order was deleted in CARE or belongs to another facility: check the order in CARE and the facility selected in the Configuration tab"),
            ("No ImagingStudy matches", "The order is not a radiology (imaging) order or was created without an encounter: ask CARE staff to check the order"),

            // Upload (error type, then message)
            ("accession_number_unreadable", "The file is not a valid DICOM file or is incomplete: send the study again from the modality"),
            ("accession_number_missing", "The modality sent no Accession Number: select the patient from the worklist on the modality so it is filled in"),
            ("accession_number_unmatched", "No open order in this facility has this Accession Number: the order may be cancelled or deleted in CARE, or the number was changed on the modality"),
            ("study_uid_unreadable", "The image has no readable Study Instance UID: check the modality's DICOM export"),
            ("study_uid_missing", "The image has no Study Instance UID: check the modality's DICOM export"),
            ("study_link_conflict", "This study is already linked to another patient, encounter or order in CARE (the modality reused the study or the wrong worklist entry was picked): ask CARE staff to fix the link; retrying will not help"),
            ("Duplicate: File already uploaded", "The image is already in CARE"),
            ("still being removed from the PACS", "Temporary: it is retried"),
            ("study changed during the upload", "Temporary: it is retried"),
            ("Upload succeeded but failed to fetch", "The image is stored in the PACS but CARE could not read the study back: report it to the CARE server admin"),
            ("DCM4CHE", "CARE could not store the image in its PACS (dcm4chee): report it to the CARE server admin"),
            ("Exception occurred", "CARE could not store the image (its PACS may be down): report it to the CARE server admin"),

            // Request values only a version mismatch can cause
            ("Input should be", "The DICOM Enabler and CARE versions do not match: update the DICOM Enabler"),

            // Rate limit (per IP: worklist 100/min, modalities 50/min, scan status 200/min, upload 2000/min)
            ("Too many requests", "CARE is limiting requests from this IP; it is retried later. If it keeps happening, increase the worklist refresh interval, or check whether several DICOM Enablers share one IP"),
        };

        /// <summary>
        /// What the operator should check for a CARE API error: from CARE's own message or error type when it is
        /// a known one, otherwise from the status. Null when there is no usual cause.
        /// </summary>
        public static string Hint(int status, string responseBody = null)
        {
            if (!string.IsNullOrEmpty(responseBody))
            {
                foreach (var known in KnownErrors)
                {
                    if (responseBody.IndexOf(known.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                        return known.Hint;
                }
            }
            if (status == 401)
                return "CARE rejected the service-account token: check it is this facility's and still active, then " + EnterToken;
            if (status == 403)
                return "The service account has no permission for this in CARE: ask the CARE admin to check its role in this facility";
            if (status == 404)
                return "Check the CARE server url and the facility selected in the Configuration tab";
            if (status == 413)
                return "The file is larger than the CARE server accepts: ask the CARE server admin to raise the upload size limit";
            if (status == 429)
                return "CARE is limiting requests; it is retried later";
            if (status >= 500)
                return "CARE server error: it is retried later; if it continues, report it to the CARE server admin";
            return null;
        }

        /// <summary>
        /// The readable part of a CARE error body: the msg of each entry in errors (a string, or an object of
        /// field: message, as for a 400), or detail (as for a 401). Null when the body has neither, e.g. an
        /// HTML error page from a proxy.
        /// </summary>
        public static string DescribeBody(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return null;
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
                        else if (entry is JValue)
                            messages.Add(FlattenMessage(entry));
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

        /// <summary>
        /// Writes detail (URLs, response bodies, each step) to writeToLog as information. The call is
        /// compiled in a Debug build only, so a Release build's logs keep to the summary and the errors.
        /// </summary>
        [Conditional("DEBUG")]
        public static void Detail(Action<string, bool> writeToLog, string message)
        {
            writeToLog?.Invoke(message, true);
        }

        /// <summary>
        /// Writes a summary line to writeToLog as information in a Release build only, where it stands in for
        /// the Detail lines (URLs, each step) that a Debug build writes instead.
        /// </summary>
        public static void ReleaseSummary(Action<string, bool> writeToLog, string message)
        {
#if !DEBUG
            writeToLog?.Invoke(message, true);
#endif
        }

        /// <summary>
        /// A CARE API URL shortened for a Release log: the path after .../external/ (or after /api/), with each
        /// UUID shown as {id} and query parameters whose value is a UUID shown by name only; limit and offset=0
        /// are left out. E.g. modalities/?care_type=dicom&amp;device_id or modalities/{id}/status/.
        /// </summary>
        public static string DescribeEndpoint(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri uri))
                return string.Join("/", url.Trim().Split('/').Select(segment => IsId(segment) ? "{id}" : segment));

            string path = uri.AbsolutePath;
            int start = path.IndexOf("/external/", StringComparison.OrdinalIgnoreCase);
            if (start >= 0)
                path = path.Substring(start + "/external/".Length);
            else if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                path = path.Substring("/api/".Length);
            path = string.Join("/", path.Split('/').Select(segment => IsId(Uri.UnescapeDataString(segment)) ? "{id}" : segment));

            var parameters = new List<string>();
            foreach (string pair in uri.Query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int equals = pair.IndexOf('=');
                string name = Uri.UnescapeDataString(equals < 0 ? pair : pair.Substring(0, equals));
                string value = equals < 0 ? string.Empty : Uri.UnescapeDataString(pair.Substring(equals + 1).Replace('+', ' '));
                if (name == "limit" || (name == "offset" && value == "0"))
                    continue;
                parameters.Add(value.Length == 0 || IsId(value) ? name : name + "=" + value);
            }
            return parameters.Count == 0 ? path : path + "?" + string.Join("&", parameters);
        }

        /// <summary>
        /// The fields of a JSON request body for a Release log, e.g. "assignment_status=assigned"; fields whose
        /// value is a UUID are shown by name only. Empty when there is no body or it is not a JSON object.
        /// </summary>
        public static string DescribePayload(string jsonBody)
        {
            if (string.IsNullOrWhiteSpace(jsonBody))
                return string.Empty;
            try
            {
                return string.Join(", ", JObject.Parse(jsonBody).Properties().Select(p =>
                {
                    string value = p.Value.Type == JTokenType.String ? (string)p.Value : p.Value.ToString(Formatting.None);
                    return IsId(value) ? p.Name : p.Name + "=" + value;
                }));
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        private static bool IsId(string value)
        {
            return !string.IsNullOrEmpty(value) && Guid.TryParse(value, out _);
        }

        private static string FlattenMessage(JToken message)
        {
            if (message is JArray list)
                return string.Join(" ", list.Select(FlattenMessage));
            return message.Type == JTokenType.String ? (string)message : message.ToString(Formatting.None);
        }
    }

    /// <summary>
    /// A CARE API call that failed, whose Message is already the whole reason for the log (the status,
    /// CARE's own message and what to check, or why CARE could not be reached), so it is logged as is.
    /// </summary>
    public class CareApiException : Exception
    {
        public CareApiException(string message) : base(message)
        {
        }

        public CareApiException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
