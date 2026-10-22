using Plexus.Common.Database;
using System;
using System.Collections.Generic;
using System.IO;

namespace Plexus_DICOM_Enabler.Forms
{
    /// <summary>
    /// Default values for the Configuration tab settings, stored in the database
    /// when the services are started so the user sees and can change them.
    /// </summary>
    public static class cls_ConfigDefaults
    {
        /// <summary>
        /// The care_config settings shown in the Configuration tab
        /// </summary>
        public static readonly string[] ConfigKeys =
        {
            "facility_id",
            "care_modality",
            "care_from_date",
            "scu_poll_interval_seconds",
            "worklist_refresh_start_seconds",
            "worklist_refresh_interval_seconds",
            "max_upload_retries",
            "upload_retry_delay_minutes",
            "scp_folder",
            "failed_scp_folder",
        };

        /// <summary>
        /// Stores the default of each blank Configuration tab setting in care_config. facility_id has no default.
        /// </summary>
        public static bool ApplyDefaults(ucls_DAL objDAL, ref string errorString)
        {
            var configDefaults = new Dictionary<string, string>();
            foreach (string configKey in ConfigKeys)
            {
                string value = GetConfigDefault(configKey);
                if (!string.IsNullOrWhiteSpace(value))
                    configDefaults[configKey] = value;
            }
            return objDAL.FillBlankConfig(configDefaults, ref errorString);
        }

        /// <summary>
        /// The value the services use when a care_config setting is blank: their App.config value, or
        /// the built-in default when App.config has none. Mirrors the fallbacks in the MWL, SCU and
        /// Store SCP services. facility_id has no default and must be entered by the user.
        /// </summary>
        public static string GetConfigDefault(string configKey)
        {
            switch (configKey)
            {
                case "care_modality":
                    return CombineServiceDefaults(ReadServiceSetting("CARE_MWL_Service", "careModality"), ReadServiceSetting("CARE_SCU_Service", "careModality"));
                case "care_from_date":
                    return CombineServiceDefaults(ReadServiceSetting("CARE_MWL_Service", "careFromDate"), ReadServiceSetting("CARE_SCU_Service", "careFromDate"));
                case "worklist_refresh_start_seconds":
                    return WholeNumberOrDefault(ReadServiceSetting("CARE_MWL_Service", "worklistRefreshStartSeconds"), 30, 0);
                case "worklist_refresh_interval_seconds":
                    return WholeNumberOrDefault(ReadServiceSetting("CARE_MWL_Service", "worklistRefreshIntervalSeconds"), 30, 1);
                case "scu_poll_interval_seconds":
                    return "5";
                case "scp_folder":
                    return Path.Combine(Global._applicationPath, "SCP");
                case "failed_scp_folder":
                    return Path.Combine(Global._applicationPath, "FailedSCP");
                case "max_upload_retries":
                    return WholeNumberOrDefault(ReadServiceSetting("CARE_SCU_Service", "maxUploadRetries"), 10, 1);
                case "upload_retry_delay_minutes":
                    return "2";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Reads an appSettings value from a service's .exe.config installed next to this application.
        /// Returns null when the file or the key is missing.
        /// </summary>
        private static string ReadServiceSetting(string serviceAssembly, string key)
        {
            try
            {
                string configPath = Path.Combine(Global._applicationPath, serviceAssembly + ".exe.config");
                if (!File.Exists(configPath))
                    return null;

                var configDoc = new System.Xml.XmlDocument();
                configDoc.Load(configPath);
                foreach (System.Xml.XmlNode node in configDoc.SelectNodes("/configuration/appSettings/add"))
                {
                    if (node.Attributes?["key"]?.Value == key)
                        return node.Attributes["value"]?.Value ?? string.Empty;
                }
            }
            catch (Exception)
            {
                // Unreadable config - shown as not found
            }
            return null;
        }

        /// <summary>
        /// The value when the MWL and SCU services agree. Empty when they differ or App.config has none,
        /// as no single value can be shown in the box.
        /// </summary>
        private static string CombineServiceDefaults(string mwlValue, string scuValue)
        {
            return mwlValue == scuValue ? (mwlValue ?? string.Empty).Trim() : string.Empty;
        }

        /// <summary>
        /// The App.config value when it is a whole number >= minValue, otherwise the built-in default,
        /// as the services do.
        /// </summary>
        private static string WholeNumberOrDefault(string appConfigValue, int builtInDefault, int minValue)
        {
            if (int.TryParse(appConfigValue, out int parsed) && parsed >= minValue)
                return parsed.ToString();
            return builtInDefault.ToString();
        }
    }
}
