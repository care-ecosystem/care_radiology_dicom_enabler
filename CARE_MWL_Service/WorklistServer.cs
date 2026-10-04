// Copyright (c) 2012-2022 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using FellowOakDicom;
using FellowOakDicom.Log;
using FellowOakDicom.Network;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Threading;
using Plexus.Common.Database;
using Serilog;

using Worklist_SCP.Model;


namespace Worklist_SCP
{
    public class WorklistServer
    {

        private static IDicomServer _server;
        private static Timer _itemsLoaderTimer;
        private static Serilog.ILogger _refreshLogger;
        private static ucls_DAL _refreshDal;


        protected WorklistServer()
        {
        }

        public static string AETitle { get; set; }


        public static IWorklistItemsSource CreateItemsSourceService => new WorklistItemsProvider();

        public static List<WorklistItem> CurrentWorklistItems { get; set; }

        public static void Start(int port, string aet)
        {
            AETitle = aet;
            _server = DicomServerFactory.Create<WorklistService>(port);
            // every 30 seconds the worklist source is queried and the current list of items is cached in _currentWorklistItems
            _itemsLoaderTimer = new Timer((state) =>
            {
                var newWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItems();
                CurrentWorklistItems = newWorklistItems;
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        /// <param name="aet"></param>
        /// <param name="backend"> 0 - List , 1- MySQL</param>

        public static void Start(int port, string aet,int backend)
        {
            try
            {
                AETitle = aet;

                new DicomSetupBuilder()
                    .RegisterServices(s => s.AddFellowOakDicom().AddLogManager<ConsoleLogManager>())
                    .Build();
                _server = DicomServerFactory.Create<WorklistService>(port);
                // The worklist source is first queried worklist_refresh_start_seconds after start, then every
                // worklist_refresh_interval_seconds, and the current list of items is cached in CurrentWorklistItems.
                int refreshStartSeconds = GetSecondsSetting("worklist_refresh_start_seconds", "worklistRefreshStartSeconds", 30, 0);
                int refreshIntervalSeconds = GetSecondsSetting("worklist_refresh_interval_seconds", "worklistRefreshIntervalSeconds", 30, 1);
                RefreshLogger.Information($"[REFRESH] Worklist refresh starts after {refreshStartSeconds}s, then every {refreshIntervalSeconds}s");
                _itemsLoaderTimer = new System.Threading.Timer((state) =>
                {
                    switch(backend)
                    {
                        case 0:

                            var newWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItems();
                            WorklistServer.CurrentWorklistItems = newWorklistItems;
                            break;
                        case 1:
                            var dbWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItemsFromDB();
                            WorklistServer.CurrentWorklistItems = dbWorklistItems;
                            break;
                        case 2:
                            // This refresh has no DICOM association; the Facility ID comes from the
                            // Configuration tab, the same one every calling AE uses.
                            string refreshFacilityId = ResolveFacilityIdForRefresh();
                            if (string.IsNullOrWhiteSpace(refreshFacilityId))
                            {
                                // Facility ID is mandatory. Skipping beats issuing an unfiltered
                                // request, which would overwrite the facility-scoped cache with items
                                // from every facility and mislead MPPS correlation.
                                RefreshLogger.Warning("[REFRESH] Skipping periodic CARE worklist fetch - no Facility ID resolved");
                                break;
                            }
                            var pellucidWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItemsFromCareAsync(refreshFacilityId);
                            WorklistServer.CurrentWorklistItems = pellucidWorklistItems;
                            break;

                    }

                }, null, TimeSpan.FromSeconds(refreshStartSeconds), TimeSpan.FromSeconds(refreshIntervalSeconds));
            }
            catch(Exception ex)
            {
                throw new Exception("WorklistServer.Start failed on port " + port + ": " + ex.Message, ex);
            }


        }

        /// <summary>
        /// Reads a whole number of seconds from care_config, or App.config when it is blank there,
        /// falling back to defaultValue when both are missing, not a number or below minValue.
        /// </summary>
        private static int GetSecondsSetting(string configKey, string appSettingKey, int defaultValue, int minValue)
        {
            string value = GetConfigSetting(configKey, appSettingKey);
            if (int.TryParse(value, out int parsed) && parsed >= minValue)
                return parsed;
            if (!string.IsNullOrWhiteSpace(value))
                RefreshLogger.Warning($"[REFRESH] {configKey}='{value}' is invalid (must be a whole number >= {minValue}) - using {defaultValue}");
            return defaultValue;
        }

        /// <summary>
        /// Reads a setting from care_config (Configuration tab). When it is blank there, or care_config
        /// cannot be read, the appSettingKey value from App.config is used (empty when appSettingKey is
        /// null). Opens its own DAL each call because the refresh timer, C-FIND, C-STORE and MPPS run
        /// concurrently and a ucls_DAL holds a single connection.
        /// </summary>
        public static string GetConfigSetting(string configKey, string appSettingKey)
        {
            string fallback = appSettingKey == null ? string.Empty : ConfigurationManager.AppSettings[appSettingKey] ?? string.Empty;
            string fallbackName = appSettingKey == null ? "the default" : "App.config " + appSettingKey;
            ucls_DAL dal = null;
            try
            {
                string errorString = string.Empty;
                dal = new ucls_DAL(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                string value = dal.GetConfigValue(configKey, fallback, ref errorString);
                if (!string.IsNullOrEmpty(errorString))
                    RefreshLogger.Error($"[CONFIG] {errorString} - using {fallbackName}");
                return value;
            }
            catch (Exception ex)
            {
                RefreshLogger.Error($"[CONFIG] Reading {configKey} failed with exception {ex.Message} - using {fallbackName}");
                return fallback;
            }
            finally
            {
                dal?.Dispose();
            }
        }

        /// <summary>
        /// Folder received DICOM files are saved to for the SCU service to upload: scp_folder in
        /// care_config, or SCP under the install folder when it is blank.
        /// </summary>
        public static string GetScpFolder()
        {
            string folder = GetConfigSetting("scp_folder", null);
            return string.IsNullOrWhiteSpace(folder)
                ? Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "SCP")
                : folder;
        }

       


        /// <summary>
        /// Logger for the periodic refresh. Separate from WorklistService.fileLogger, which only
        /// exists once a modality has opened an association - the timer can fire before that.
        /// </summary>
        private static Serilog.ILogger RefreshLogger
        {
            get
            {
                if (_refreshLogger == null)
                {
                    string logFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "logs/ModalitySCP.txt");
                    _refreshLogger = new LoggerConfiguration()
                        .WriteTo.File(logFilePath,
                            restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
                            shared: true,
                            retainedFileCountLimit: 3,
                            rollOnFileSizeLimit: true,
                            fileSizeLimitBytes: 5120)
                        .CreateLogger();
                }
                return _refreshLogger;
            }
        }

        /// <summary>
        /// Resolves the Facility ID for the periodic refresh from the Configuration tab. Returns empty
        /// when none is set or it cannot be read.
        /// </summary>
        private static string ResolveFacilityIdForRefresh()
        {
            string errorString = string.Empty;
            string resolvedFrom = string.Empty;
            try
            {
                if (_refreshDal == null)
                {
                    _refreshDal = new ucls_DAL(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                }

                string facilityId = _refreshDal.GetFacilityId(string.Empty, ref resolvedFrom, ref errorString);
                if (errorString != string.Empty)
                {
                    RefreshLogger.Error($"[FACILITY][REFRESH] Lookup failed: {errorString}");
                    return string.Empty;
                }
                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    RefreshLogger.Information($"[FACILITY][REFRESH] No Facility ID - {resolvedFrom}");
                    return string.Empty;
                }
                return facilityId;
            }
            catch (Exception ex)
            {
                RefreshLogger.Error($"[FACILITY][REFRESH] Lookup failed with exception {ex.Message}");
                return string.Empty;
            }
        }


        public static void Stop()
        {
            _itemsLoaderTimer?.Dispose();
            _server?.Dispose();
        }


    }
}
