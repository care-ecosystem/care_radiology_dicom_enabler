

using System;
using System.Configuration;
using System.Diagnostics;
using System.ServiceProcess;
using Worklist_SCP;
using Plexus_MWL_Service.logs;
using Plexus.Common.config;
using Plexus.Common.Database;
using System.IO;
using System.Reflection;
using Serilog;

namespace Plexus_MWL_Service
{
    public partial class PlexusMWLService : ServiceBase
    {
        private static Serilog.ILogger _fileLogger;

        // How long Windows waits for this service at pre-shutdown, and how long the unassign may take then
        private static readonly TimeSpan PreshutdownTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ShutdownUnassignTime = TimeSpan.FromSeconds(20);
        // Each CARE call while Windows shuts down
        private static readonly TimeSpan ShutdownRequestTimeout = TimeSpan.FromSeconds(5);
        // Set when Windows is shutting down: the unassign is then quick (short timeouts, a deadline)
        private volatile bool _windowsShuttingDown;

        public PlexusMWLService()
        {
            InitializeComponent();
            CanShutdown = true;
            _fileLogger = new LoggerConfiguration()
                .WriteTo.Sink(DailyFolderSink.For("ModalitySCP.txt"), Serilog.Events.LogEventLevel.Information)
                .CreateLogger();
            // Pre-shutdown (OnCustomCommand) gives time to unassign the DICOM Enabler when Windows shuts down;
            // OnShutdown is the fallback when it is not available
            if (!ServicePreshutdown.Accept(this, out string preshutdownError))
                _fileLogger.Warning("[SHUTDOWN] Pre-shutdown not available (" + preshutdownError + "); only the shutdown notification is used");
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                string applicationPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                _fileLogger.Information("MWL Service OnStart. ApplicationPath: " + applicationPath);
                string serviceName = ServicePreshutdown.SetTimeout(PreshutdownTimeout, out string timeoutError);
                if (serviceName != null)
                    _fileLogger.Information($"[SHUTDOWN] Windows waits up to {PreshutdownTimeout.TotalSeconds:0}s at pre-shutdown for {serviceName}");
                else
                    _fileLogger.Warning("[SHUTDOWN] The pre-shutdown timeout could not be set (Windows' default is used): " + timeoutError);
                // The Configuration tab restarts the service with the changed settings as start parameters
                foreach (string change in args)
                    _fileLogger.Information($"Restarted after a Configuration tab change: {change}");
                int backend = Convert.ToInt32(ConfigurationManager.AppSettings["backend"].ToString());
                string mwlPort = cls_PlexusConfig.ReadDetailsFromXML(applicationPath, @"/configurations/mwlport");
                string mwlAet = cls_PlexusConfig.ReadDetailsFromXML(applicationPath, @"/configurations/mwlaetitle");
                _fileLogger.Information($"Starting MWL DICOM server on port {mwlPort}, AET={mwlAet}, backend={backend}");
                WorklistServer.Start(Convert.ToInt32(mwlPort), mwlAet, backend);
                _fileLogger.Information("WorklistServer.Start completed.");
                System.Threading.Tasks.Task.Run(() => SetAssignmentInCare(ucls_CareModalities.AssignmentAssigned, null, null));
            }
            catch (Exception ex)
            {
                _fileLogger.Error("Starting MWL SCP Service failed: " + ex.ToString());
            }
        }

        protected override void OnStop()
        {
            try
            {
                WorklistServer.Stop();
                _fileLogger.Information("MWL SCP Service stopped successfully.");
            }
            catch (Exception ex)
            {
                _fileLogger.Error("MWL SCP Service stop failed: " + ex.Message);
            }
            if (_windowsShuttingDown)
            {
                // Quick: the enabler first, 5 seconds per call, and the devices only while time is left
                // (they are set to assigned again when the service starts)
                SetAssignmentInCare(ucls_CareModalities.AssignmentUnassigned, ShutdownRequestTimeout, DateTime.UtcNow + ShutdownUnassignTime);
            }
            else
            {
                RequestAdditionalTime(60000);
                SetAssignmentInCare(ucls_CareModalities.AssignmentUnassigned, null, null);
            }
        }

        /// <summary>
        /// Windows' pre-shutdown, sent before the shutdown notification while Windows waits for the service
        /// (up to PreshutdownTimeout): the service stops, unassigning the DICOM Enabler (OnStop).
        /// </summary>
        protected override void OnCustomCommand(int command)
        {
            if (command != ServicePreshutdown.ControlPreshutdown)
            {
                base.OnCustomCommand(command);
                return;
            }
            _fileLogger.Information("[SHUTDOWN] Windows is shutting down (pre-shutdown): stopping the MWL SCP Service.");
            _windowsShuttingDown = true;
            Stop();
        }

        /// <summary>Windows is shutting down and pre-shutdown was not available: Windows allows only a few seconds.</summary>
        protected override void OnShutdown()
        {
            _fileLogger.Information("[SHUTDOWN] Windows is shutting down: stopping the MWL SCP Service.");
            _windowsShuttingDown = true;
            OnStop();
        }

        /// <summary>
        /// PATCHes the saved DICOM Enabler and its saved devices to assignmentStatus in CARE (see
        /// ucls_CareModalities.TrySetEnablerAssignment), logging to ModalitySCP.txt. requestTimeout and
        /// deadline limit it while Windows shuts down (null otherwise). Never throws.
        /// </summary>
        private static void SetAssignmentInCare(string assignmentStatus, TimeSpan? requestTimeout, DateTime? deadline)
        {
            ucls_DAL dal = null;
            try
            {
                dal = new ucls_DAL(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                ucls_CareModalities.TrySetEnablerAssignment(dal,
                    ConfigurationManager.AppSettings["careBaseUrl"] ?? string.Empty,
                    ConfigurationManager.AppSettings["careToken"] ?? string.Empty,
                    ConfigurationManager.AppSettings["modalitiesURL"],
                    assignmentStatus,
                    (message, success) =>
                    {
                        if (success)
                            _fileLogger.Information("[ASSIGNMENT] " + message);
                        else
                            _fileLogger.Error("[ASSIGNMENT] " + message);
                    },
                    out _, requestTimeout, deadline);
            }
            catch (Exception ex)
            {
                _fileLogger.Error($"[ASSIGNMENT] Setting the DICOM Enabler to {assignmentStatus} in CARE failed with exception {ex.Message}");
            }
            finally
            {
                dal?.Dispose();
            }
        }
    }
}
