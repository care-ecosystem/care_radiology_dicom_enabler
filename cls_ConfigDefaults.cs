using Plexus.Common.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        /// The defaults Start Service(s) offers to store (with ucls_DAL.FillBlankConfig, once the user confirms):
        /// each Configuration tab setting that is blank in care_config and has a default, with that default.
        /// facility_id has no default. Null when care_config cannot be read (errorString is set).
        /// </summary>
        public static Dictionary<string, string> GetBlankDefaults(ucls_DAL objDAL, ref string errorString)
        {
            System.Data.DataSet config = objDAL.LoadConfig(ref errorString);
            if (config == null || config.Tables.Count == 0)
                return null;
            var current = new Dictionary<string, string>();
            foreach (System.Data.DataRow row in config.Tables[0].Rows)
                current[row["config_key"].ToString()] = row["config_value"]?.ToString() ?? string.Empty;

            var defaults = new Dictionary<string, string>();
            foreach (string configKey in ConfigKeys)
            {
                // Only settings whose care_config row exists and is blank: FillBlankConfig changes nothing else
                if (!current.TryGetValue(configKey, out string value) || !string.IsNullOrWhiteSpace(value))
                    continue;
                string configDefault = GetConfigDefault(configKey);
                if (!string.IsNullOrWhiteSpace(configDefault))
                    defaults[configKey] = configDefault;
            }
            return defaults;
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
        /// </summary>
        public static readonly string[] DicomEndpointKeys = { "mwlaetitle", "mwlhost", "mwlport", "sscpaetitle", "sscphost", "sscpport" };

        /// <summary>
        /// </summary>
        public static bool TryGetDicomEndpointSettings(string detailsJson, out Dictionary<string, string> settings, ref string errorString)
        {
            settings = null;
            if (!ucls_CareModalities.TryGetDicomEndpoints(detailsJson, out CareDicomEndpoint modalityScp, out CareDicomEndpoint storeScp, out string error))
            {
                errorString = error;
                return false;
            }
            settings = new Dictionary<string, string>
            {
                { "mwlaetitle", modalityScp.ae_title.Trim() },
                { "mwlhost", (modalityScp.host ?? string.Empty).Trim() },
                { "mwlport", modalityScp.port.Value.ToString() },
                { "sscpaetitle", storeScp.ae_title.Trim() },
                { "sscphost", (storeScp.host ?? string.Empty).Trim() },
                { "sscpport", storeScp.port.Value.ToString() },
            };
            return true;
        }

        /// <summary>
        /// Writes the DICOM Enabler's endpoints to cfg/common.cfg, where the MWL and Store SCP services read
        /// them when they start.
        /// </summary>
        public static bool ApplyDicomEndpointSettings(string detailsJson, Dictionary<string, string> changes, ref string errorString)
        {
            return UpdateDicomEndpointSettings(detailsJson, changes, true, ref errorString);
        }

        /// <summary>
        /// The changes ApplyDicomEndpointSettings would make to cfg/common.cfg, without making them:
        /// changes gets "key: old -> new" for each value that differs from the DICOM Enabler's endpoints.
        /// </summary>
        public static bool CheckDicomEndpointSettings(string detailsJson, Dictionary<string, string> changes, ref string errorString)
        {
            return UpdateDicomEndpointSettings(detailsJson, changes, false, ref errorString);
        }

        private static bool UpdateDicomEndpointSettings(string detailsJson, Dictionary<string, string> changes, bool write, ref string errorString)
        {
            if (!TryGetDicomEndpointSettings(detailsJson, out Dictionary<string, string> settings, ref errorString))
            {
                if (write)
                    cls_GeneralLog.Error("SCP SETTINGS", "The Modality SCP and Store SCP settings cannot be set from the DICOM Enabler: " + errorString);
                return false;
            }
            try
            {
                string cfgPath = Path.Combine(Global._applicationPath, "cfg", "common.cfg");
                var configDoc = new System.Xml.XmlDocument();
                configDoc.Load(cfgPath);
                System.Xml.XmlNode root = configDoc.SelectSingleNode("/configurations");
                if (root == null)
                {
                    errorString = cfgPath + " has no <configurations> element.";
                    return false;
                }

                bool changed = false;
                foreach (string key in DicomEndpointKeys)
                {
                    System.Xml.XmlNode node = root.SelectSingleNode(key);
                    string oldValue = node?.InnerText.Trim() ?? string.Empty;
                    if (oldValue == settings[key])
                        continue;
                    // An older common.cfg may not have the element (e.g. no mwlhost): add it
                    if (node == null)
                        node = root.AppendChild(configDoc.CreateElement(key));
                    node.InnerText = settings[key];
                    changes[key] = $"{key}: {(oldValue == string.Empty ? "(blank)" : oldValue)} -> {settings[key]}";
                    changed = true;
                }
                if (changed && write)
                {
                    configDoc.Save(cfgPath);
                    foreach (string key in DicomEndpointKeys.Where(changes.ContainsKey))
                        cls_GeneralLog.Info("SCP SETTINGS", "From the DICOM Enabler's endpoints in CARE - " + changes[key]);
                }
                return true;
            }
            catch (Exception ex)
            {
                errorString = (write ? "Writing the DICOM Enabler's endpoints to" : "Reading") + " cfg/common.cfg failed: " + ex.Message;
                cls_GeneralLog.Error("SCP SETTINGS", errorString);
                return false;
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
