using System;
using System.Collections.Generic;
using System.Linq;
using Plexus.Common.Database;
using Plexus_DICOM_Enabler.Forms;

namespace Plexus_DICOM_Enabler
{
    /// <summary>
    /// What a sync check found: how CARE differs from what this enabler has saved. Nothing is changed by
    /// the check; Apply makes the changes once the user has confirmed them.
    /// </summary>
    public sealed class CareSyncCheck
    {
        public string FacilityId;
        public string EnablerId;
        public string EnablerName;
        /// <summary>Why CARE could not be checked; null when it was.</summary>
        public string Error;

        /// <summary>The DICOM devices CARE lists for the enabler, with their details: saved on Apply.</summary>
        public List<CareDeviceRecord> CareDevices = new List<CareDeviceRecord>();
        public List<string> DeviceChanges = new List<string>();
        public int DevicesAdded, DevicesUpdated, DevicesRemoved;

        /// <summary>Changes to the enabler's own entry in CARE (name, IP, location, status).</summary>
        public List<string> EnablerChanges = new List<string>();
        /// <summary>The enabler's care_devices row as saved, and with CARE's changes (saved on Apply).</summary>
        public CareDeviceRecord SavedEnabler;
        public CareDeviceRecord UpdatedEnabler;
        /// <summary>True when CARE returned the enabler (so its status can be set).</summary>
        public bool EnablerInCare;
        /// <summary>The ids of the devices saved in care_devices when the check ran.</summary>
        public HashSet<string> SavedDeviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Modality SCP / Store SCP settings in cfg/common.cfg that differ from the enabler's endpoints.</summary>
        public Dictionary<string, string> ScpSettingChanges = new Dictionary<string, string>();

        /// <summary>Things the user should know that Apply cannot fix, e.g. the enabler is no longer assigned.</summary>
        public List<string> Warnings = new List<string>();

        public bool Checked => Error == null;
        public bool HasChanges => DeviceChanges.Count > 0 || EnablerChanges.Count > 0 || ScpSettingChanges.Count > 0;

        /// <summary>The changes and warnings as lines for the confirmation window, grouped by what they change.</summary>
        public List<string> Describe()
        {
            var lines = new List<string>();
            if (DeviceChanges.Count > 0)
            {
                lines.Add($"Modality devices ({DevicesAdded} new, {DevicesUpdated} updated, {DevicesRemoved} removed) - saved to care_devices:");
                lines.AddRange(DeviceChanges.Select(change => "    " + change));
            }
            if (EnablerChanges.Count > 0)
            {
                lines.Add($"DICOM Enabler {EnablerName} - saved to care_devices:");
                lines.AddRange(EnablerChanges.Select(change => "    " + change));
            }
            if (ScpSettingChanges.Count > 0)
            {
                lines.Add("Modality SCP / Store SCP settings from the DICOM Enabler's endpoints - written to cfg/common.cfg (the services using them are restarted):");
                lines.AddRange(ScpSettingChanges.Values.Select(change => "    " + change));
            }
            if (Warnings.Count > 0)
            {
                lines.Add("Needs attention (not changed by this sync):");
                lines.AddRange(Warnings.Select(warning => "    " + warning));
            }
            return lines;
        }
    }

    /// <summary>
    /// Checks CARE for changes to the selected DICOM Enabler and its DICOM devices (modality machines),
    /// and to the Modality SCP / Store SCP settings that come from the enabler, then applies them once the
    /// user confirms. Used by the Devices tab's Sync button and by Start Service(s). Check makes no
    /// changes; nothing is written to the database or the settings without the user's confirmation.
    /// </summary>
    public static class cls_CareSync
    {
        private const string Area = "SYNC";
        private const string DicomEnablerIdKey = "dicom_enabler_id";

        // The device fields compared with the saved ones; a change in any of them is a change to confirm
        private static readonly (string Name, Func<CareDeviceRecord, string> Value)[] DeviceFields =
        {
            ("Name", d => d.DeviceName),
            ("Modality", d => d.Modality),
            ("Station AE Title", d => d.AeTitle),
            ("IP", d => d.Ip),
            ("Location", d => d.LocationName),
            ("Status", d => d.Status),
            ("Availability", d => d.AvailabilityStatus),
            // assignment_status is not compared: Sync sets it to assigned in CARE (and in care_devices) itself
        };

        // The enabler's list entry fields compared with its saved row (the endpoints are only returned
        // while it is unassigned, so they cannot be checked again once it is assigned)
        private static readonly (string Name, Func<CareDeviceRecord, string> Value)[] EnablerFields =
        {
            ("Name", d => d.DeviceName),
            ("IP", d => d.Ip),
            ("Location", d => d.LocationName),
            ("Status", d => d.Status),
            ("Availability", d => d.AvailabilityStatus),
            // assignment_status is not compared: Sync sets it to assigned in CARE (and in care_devices) itself
        };

        // The enabler's DICOM endpoints, compared when CARE's retrieve API returned them
        private static readonly (string Name, Func<CareDeviceRecord, string> Value)[] EnablerEndpointFields =
        {
            ("Modality SCP AE Title", d => d.ModalityScpAeTitle),
            ("Modality SCP Host", d => d.ModalityScpHost),
            ("Modality SCP Port", d => d.ModalityScpPort?.ToString()),
            ("Store SCP AE Title", d => d.StoreScpAeTitle),
            ("Store SCP Host", d => d.StoreScpHost),
            ("Store SCP Port", d => d.StoreScpPort?.ToString()),
        };

        /// <summary>
        /// The facility's modalities API URL and the static API key from App.config. False with error set
        /// when the facility, careBackendURL or staticAPIKey is missing.
        /// </summary>
        public static bool TryGetModalitiesApi(string facilityId, out string modalitiesURL, out string apiKey, out string error)
        {
            modalitiesURL = null;
            error = null;
            string baseUrl = (System.Configuration.ConfigurationManager.AppSettings["careBackendURL"] ?? string.Empty).Trim();
            apiKey = System.Configuration.ConfigurationManager.AppSettings["staticAPIKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(facilityId))
                error = "the Facility Id is not set.";
            else if (baseUrl == string.Empty)
                error = "careBackendURL is not set in App.config.";
            else if (string.IsNullOrWhiteSpace(apiKey))
                error = "staticAPIKey is not set in App.config.";
            if (error != null)
                return false;
            modalitiesURL = ucls_CareModalities.BuildModalitiesURL(baseUrl, System.Configuration.ConfigurationManager.AppSettings["modalitiesURL"], facilityId);
            return true;
        }

        /// <summary>
        /// Calls the CARE Modalities APIs and compares their responses with what is saved: the enabler's
        /// DICOM devices with care_devices, the enabler's entry with its saved details, and the Modality SCP /
        /// Store SCP settings in cfg/common.cfg with the enabler's endpoints. Changes nothing. Blocking (HTTP
        /// and database); call it off the UI thread. why is logged with the result, e.g. "Sync button".
        /// </summary>
        public static CareSyncCheck Check(string why)
        {
            var check = new CareSyncCheck();
            var dal = new ucls_DAL(Global._applicationPath);
            try
            {
                string errorString = string.Empty;
                check.FacilityId = dal.GetConfigValue("facility_id", string.Empty, ref errorString).Trim();
                check.EnablerId = dal.GetConfigValue(DicomEnablerIdKey, string.Empty, ref errorString).Trim();
                if (!string.IsNullOrEmpty(errorString))
                    return Fail(check, "Could not read the configuration: " + errorString);
                if (check.EnablerId == string.Empty)
                    return Fail(check, "No DICOM Enabler is selected. Select one in the Configuration tab and save it.");
                check.SavedEnabler = dal.GetCareDevice(check.EnablerId, ref errorString);
                if (!string.IsNullOrEmpty(errorString))
                    return Fail(check, "Could not read the saved DICOM Enabler: " + errorString);
                if (check.SavedEnabler == null)
                    return Fail(check, "The selected DICOM Enabler is not saved in care_devices. Select it again in the Configuration tab and save it.");
                check.EnablerName = string.IsNullOrWhiteSpace(check.SavedEnabler.DeviceName) ? check.EnablerId : check.SavedEnabler.DeviceName.Trim();
                if (!TryGetModalitiesApi(check.FacilityId, out string modalitiesURL, out string apiKey, out string apiError))
                    return Fail(check, "CARE cannot be checked: " + apiError);

                cls_AppLog.Info(Area, $"Checking CARE for changes to DICOM Enabler {check.EnablerName} ({check.EnablerId}) and its devices ({why})");
                Action<string, bool> log = cls_AppLog.For("MODALITIES");

                // 1. The enabler's DICOM devices
                if (!ucls_CareModalities.TryFetchDicomDevices(modalitiesURL, apiKey, check.EnablerId, log, out CareModalitiesResponse devices, out string error))
                    return Fail(check, "Could not get the devices from CARE: " + error);
                foreach (CareModality device in devices.results)
                    check.CareDevices.Add(ToRecord(check, device, modalitiesURL, apiKey, log));

                List<CareDeviceRecord> saved = dal.GetCareDevices(check.EnablerId, ref errorString);
                if (!string.IsNullOrEmpty(errorString))
                    return Fail(check, "Could not read the saved devices: " + errorString);
                CompareDevices(check, saved);

                // 2. The enabler's own entry: retrieved (GET .../{id}/, with its DICOM endpoints), or from the list
                //    of the facility's enablers when CARE does not return it
                if (ucls_CareModalities.TryRetrieveModality(modalitiesURL, apiKey, check.EnablerId, log, out CareModality enablerDetails, out string retrieveError))
                {
                    CompareEnabler(check, enablerDetails, true);
                }
                else
                {
                    if (!ucls_CareModalities.TryFetchModalities(modalitiesURL, apiKey, ucls_CareModalities.CareTypeDicomEnabler, null, null, log,
                                                                out CareModalitiesResponse enablers, out error))
                        return Fail(check, "Could not get the DICOM Enabler from CARE: " + error);
                    CareModality listed = enablers.results.FirstOrDefault(e => string.Equals(e.id, check.EnablerId, StringComparison.OrdinalIgnoreCase));
                    if (listed != null)
                        check.Warnings.Add($"The details of DICOM Enabler {check.EnablerName} could not be retrieved from CARE ({retrieveError}); " +
                                           "its list entry was used and its DICOM endpoints were not checked.");
                    CompareEnabler(check, listed, false);
                }

                // 3. The Modality SCP / Store SCP settings the services listen with
                string endpointError = string.Empty;
                if (!cls_ConfigDefaults.CheckDicomEndpointSettings(check.UpdatedEnabler ?? check.SavedEnabler, check.ScpSettingChanges, ref endpointError))
                    check.Warnings.Add("The Modality SCP / Store SCP settings cannot be checked: " + endpointError);

                if (check.HasChanges)
                {
                    cls_AppLog.Warning(Area, $"CARE has changes not yet applied ({why}):");
                    foreach (string line in check.Describe())
                        cls_AppLog.Warning(Area, line.Trim());
                }
                else
                {
                    cls_AppLog.Info(Area, $"No changes in CARE: {check.CareDevices.Count} device(s) and the DICOM Enabler match what is saved ({why})");
                    foreach (string warning in check.Warnings)
                        cls_AppLog.Warning(Area, warning);
                }
                return check;
            }
            catch (Exception ex)
            {
                return Fail(check, "Checking CARE failed with exception " + ex.Message);
            }
            finally
            {
                dal.Dispose();
            }
        }

        /// <summary>
        /// Sets the DICOM Enabler and its DICOM devices to assigned in CARE (PATCH .../{device_id}/status/
        /// {"assignment_status": "assigned"}), which also updates their last_synced_at in CARE. Called by the
        /// Devices tab's Sync once the devices are synced: includeNewDevices true when CARE's devices are
        /// (about to be) saved, i.e. there were no changes or the user confirmed them; false when the user
        /// declined them, so only the devices already saved are set. Each one set is marked assigned in the
        /// check's records (so Apply saves it so) and in its care_devices row. Blocking (HTTP and database);
        /// call it off the UI thread. Failures are logged and added to check.Warnings; they do not stop the sync.
        /// </summary>
        public static void AssignInCare(CareSyncCheck check, bool includeNewDevices)
        {
            if (check == null || !check.Checked)
                return;
            if (!TryGetModalitiesApi(check.FacilityId, out string modalitiesURL, out string apiKey, out string apiError))
            {
                cls_AppLog.Error(Area, "Assignment status not sent to CARE: " + apiError);
                check.Warnings.Add("The assignment status could not be updated in CARE: " + apiError);
                return;
            }
            Action<string, bool> log = cls_AppLog.For("MODALITIES");
            string assigned = ucls_CareModalities.AssignmentAssigned;

            var targets = new List<(string Id, string Name)>();
            if (check.EnablerInCare)
                targets.Add((check.EnablerId, "DICOM Enabler " + check.EnablerName));
            targets.AddRange(check.CareDevices
                .Where(d => !string.IsNullOrWhiteSpace(d.DeviceId) && (includeNewDevices || check.SavedDeviceIds.Contains(d.DeviceId)))
                .Select(d => (d.DeviceId, "device " + (d.DeviceName ?? d.DeviceId))));

            var done = new List<string>();
            var failed = new List<string>();
            foreach (var (id, name) in targets)
            {
                if (ucls_CareModalities.TryUpdateAssignmentStatus(modalitiesURL, apiKey, id, assigned, log, out string error))
                    done.Add(id);
                else
                    failed.Add($"{name}: {error}");
            }

            // The records Apply saves, and the rows already saved, show what CARE now has
            var doneIds = new HashSet<string>(done, StringComparer.OrdinalIgnoreCase);
            foreach (CareDeviceRecord device in check.CareDevices.Where(d => d.DeviceId != null && doneIds.Contains(d.DeviceId)))
                device.AssignmentStatus = assigned;
            if (check.UpdatedEnabler != null && doneIds.Contains(check.EnablerId))
                check.UpdatedEnabler.AssignmentStatus = assigned;
            if (done.Count > 0)
            {
                var dal = new ucls_DAL(Global._applicationPath);
                try
                {
                    int updated = 0;
                    string errorString = string.Empty;
                    if (dal.SetCareDevicesAssignmentStatus(done, assigned, ref updated, ref errorString))
                    {
                        if (updated > 0)
                            cls_AppLog.Info(Area, $"care_devices: assignment_status set to {assigned} for {updated} row(s), as in CARE");
                    }
                    else
                    {
                        cls_AppLog.Error(Area, errorString);
                        check.Warnings.Add("The assignment status could not be saved in care_devices: " + errorString);
                    }
                }
                finally
                {
                    dal.Dispose();
                }
            }

            cls_AppLog.Info(Area, $"Set to {assigned} in CARE (last sync time updated): {done.Count} of {targets.Count} (DICOM Enabler and devices)");
            foreach (string failure in failed)
            {
                cls_AppLog.Error(Area, "Could not set to assigned in CARE: " + failure);
                check.Warnings.Add("Could not be set to assigned in CARE: " + failure);
            }
        }

        /// <summary>
        /// Makes the changes a check found, after the user confirmed them: saves CARE's devices to
        /// care_devices, the enabler's updated entry to care_config, and the SCP settings to cfg/common.cfg.
        /// scpChanges gets the SCP settings changed, so the services using them can be restarted.
        /// Returns false with error set when any of them fails.
        /// </summary>
        public static bool Apply(CareSyncCheck check, Dictionary<string, string> scpChanges, out string error)
        {
            error = null;
            if (check == null || !check.Checked || !check.HasChanges)
                return true;
            var dal = new ucls_DAL(Global._applicationPath);
            var errors = new List<string>();
            try
            {
                string errorString = string.Empty;
                if (check.DeviceChanges.Count > 0)
                {
                    int saved = 0, deleted = 0;
                    if (dal.SyncCareDevices(check.EnablerId, check.CareDevices, ref saved, ref deleted, ref errorString))
                        cls_AppLog.Info(Area, $"care_devices updated: {saved} device(s) saved, {deleted} removed ({check.DevicesAdded} new, {check.DevicesUpdated} updated)");
                    else
                        errors.Add(errorString);
                }

                if (check.EnablerChanges.Count > 0)
                {
                    errorString = string.Empty;
                    if (dal.SaveCareDevice(check.UpdatedEnabler, ref errorString))
                        cls_AppLog.Info(Area, $"DICOM Enabler {check.EnablerName} updated in care_devices: {string.Join("; ", check.EnablerChanges)}");
                    else
                        errors.Add(errorString);
                }

                if (check.ScpSettingChanges.Count > 0)
                {
                    errorString = string.Empty;
                    if (!cls_ConfigDefaults.ApplyDicomEndpointSettings(check.UpdatedEnabler ?? check.SavedEnabler, scpChanges ?? new Dictionary<string, string>(), ref errorString))
                        errors.Add(errorString);
                }
            }
            catch (Exception ex)
            {
                errors.Add("Applying the changes failed with exception " + ex.Message);
            }
            finally
            {
                dal.Dispose();
            }

            if (errors.Count == 0)
                return true;
            error = string.Join(" ", errors);
            cls_AppLog.Error(Area, "Not every change from CARE was applied: " + error);
            return false;
        }

        /// <summary>Logs that the user chose not to apply the changes a check found.</summary>
        public static void LogDeclined(CareSyncCheck check, string why)
        {
            cls_AppLog.Warning(Area, $"Changes from CARE not applied - declined by the user ({why}). " +
                                         "The saved devices and settings are kept; Sync again to apply them.");
        }

        private static CareSyncCheck Fail(CareSyncCheck check, string error)
        {
            check.Error = error;
            cls_AppLog.Error(Area, error);
            return check;
        }

        /// <summary>
        /// A device as a care_devices row. An unassigned device is retrieved for its full details (CARE
        /// returns 400 for an assigned one, whose details are its list entry).
        /// </summary>
        /// <summary>
        /// A listed device with its details retrieved from CARE (GET .../{device_id}/). When CARE does not
        /// return them, the list entry is used and a warning says so.
        /// </summary>
        private static CareDeviceRecord ToRecord(CareSyncCheck check, CareModality device, string modalitiesURL, string apiKey, Action<string, bool> log)
        {
            CareModality details = device;
            if (ucls_CareModalities.TryRetrieveModality(modalitiesURL, apiKey, device.id, log, out CareModality retrieved, out string error))
                details = retrieved;
            else
                check.Warnings.Add($"The details of device {device.device_name ?? device.id} could not be retrieved from CARE ({error}); its list entry was used.");
            return ucls_CareModalities.ToDeviceRecord(details, check.FacilityId, check.EnablerId);
        }

        private static void CompareDevices(CareSyncCheck check, List<CareDeviceRecord> saved)
        {
            foreach (CareDeviceRecord device in saved.Where(d => !string.IsNullOrEmpty(d.DeviceId)))
                check.SavedDeviceIds.Add(device.DeviceId);
            var before = saved.Where(d => !string.IsNullOrEmpty(d.DeviceId)).GroupBy(d => d.DeviceId).ToDictionary(g => g.Key, g => g.First());
            var current = check.CareDevices.Where(d => !string.IsNullOrEmpty(d.DeviceId)).GroupBy(d => d.DeviceId).ToDictionary(g => g.Key, g => g.First());

            foreach (CareDeviceRecord device in current.Values.OrderBy(d => d.Modality).ThenBy(d => d.DeviceName))
            {
                if (!before.TryGetValue(device.DeviceId, out CareDeviceRecord old))
                {
                    check.DevicesAdded++;
                    check.DeviceChanges.Add($"New: {device.DeviceName} - modality {device.Modality ?? "N/A"}, Station AE Title {device.AeTitle ?? "N/A"}, " +
                                            $"IP {device.Ip ?? "N/A"}, location {device.LocationName ?? "N/A"}");
                    continue;
                }
                var differences = DeviceFields
                    .Where(field => (field.Value(old) ?? string.Empty) != (field.Value(device) ?? string.Empty))
                    .Select(field => $"{field.Name} {field.Value(old) ?? "N/A"} -> {field.Value(device) ?? "N/A"}")
                    .ToList();
                if (differences.Count > 0)
                {
                    check.DevicesUpdated++;
                    check.DeviceChanges.Add($"Updated: {device.DeviceName} - {string.Join("; ", differences)}");
                }
            }
            foreach (CareDeviceRecord old in before.Values.Where(d => !current.ContainsKey(d.DeviceId)))
            {
                check.DevicesRemoved++;
                check.DeviceChanges.Add($"Removed (no longer connected to this DICOM Enabler in CARE): {old.DeviceName} - modality {old.Modality ?? "N/A"}, Station AE Title {old.AeTitle ?? "N/A"}");
            }
        }

        /// <summary>
        /// Compares CARE's entry for the enabler with its saved row. withEndpoints: the entry was retrieved,
        /// so its DICOM endpoints are compared too (and saved when they changed and are valid); otherwise the
        /// saved endpoints are kept.
        /// </summary>
        private static void CompareEnabler(CareSyncCheck check, CareModality entry, bool withEndpoints)
        {
            if (entry == null)
            {
                check.Warnings.Add($"DICOM Enabler {check.EnablerName} is no longer listed in CARE (removed, or not active / available). " +
                                   "Select another DICOM Enabler in the Configuration tab.");
                return;
            }
            check.EnablerInCare = true;
            if (!entry.IsAssigned)
                cls_AppLog.Warning(Area, $"DICOM Enabler {check.EnablerName} is {entry.assignment_status ?? "not assigned"} in CARE; Sync sets it back to assigned");

            CareDeviceRecord current = ucls_CareModalities.ToDeviceRecord(entry, check.SavedEnabler.FacilityId ?? check.FacilityId, null);
            if (withEndpoints && !ucls_CareModalities.ValidateDicomEndpoints(entry.modality_scp, entry.store_scp, out string endpointError))
            {
                check.Warnings.Add($"The DICOM endpoints CARE returned for DICOM Enabler {check.EnablerName} cannot be used ({endpointError}); the saved ones are kept.");
                withEndpoints = false;
            }
            var fields = withEndpoints ? EnablerFields.Concat(EnablerEndpointFields) : EnablerFields;
            foreach (var (name, value) in fields)
            {
                string before = value(check.SavedEnabler) ?? string.Empty;
                string after = value(current) ?? string.Empty;
                if (before != after)
                    check.EnablerChanges.Add($"{name} {(before == string.Empty ? "N/A" : before)} -> {(after == string.Empty ? "N/A" : after)}");
            }
            if (check.EnablerChanges.Count == 0)
                return;

            if (withEndpoints)
            {
                check.UpdatedEnabler = current;
                return;
            }
            // CARE's list entry over the saved row; the endpoints are kept (the list does not return them)
            current.StoreScpAeTitle = check.SavedEnabler.StoreScpAeTitle;
            current.StoreScpHost = check.SavedEnabler.StoreScpHost;
            current.StoreScpPort = check.SavedEnabler.StoreScpPort;
            current.ModalityScpAeTitle = check.SavedEnabler.ModalityScpAeTitle;
            current.ModalityScpHost = check.SavedEnabler.ModalityScpHost;
            current.ModalityScpPort = check.SavedEnabler.ModalityScpPort;
            check.UpdatedEnabler = current;
        }
    }
}
