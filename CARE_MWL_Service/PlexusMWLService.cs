

using System;
using System.Configuration;
using System.Diagnostics;
using System.ServiceProcess;
using Worklist_SCP;
using Plexus_MWL_Service.logs;
using Plexus.Common.config;
using System.IO;
using System.Reflection;
using Serilog;

namespace Plexus_MWL_Service
{
    public partial class PlexusMWLService : ServiceBase
    {
        private static Serilog.ILogger _fileLogger;
        public PlexusMWLService()
        {
            InitializeComponent();
            _fileLogger = new LoggerConfiguration()
                .WriteTo.Sink(new DailyFolderSink("ModalitySCP.txt"), Serilog.Events.LogEventLevel.Information)
                .CreateLogger();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                string applicationPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                _fileLogger.Information("MWL Service OnStart. ApplicationPath: " + applicationPath);
                // The Configuration tab restarts the service with the changed settings as start parameters
                foreach (string change in args)
                    _fileLogger.Information($"Restarted after a Configuration tab change: {change}");
                int backend = Convert.ToInt32(ConfigurationManager.AppSettings["backend"].ToString());
                string mwlPort = cls_PlexusConfig.ReadDetailsFromXML(applicationPath, @"/configurations/mwlport");
                string mwlAet = cls_PlexusConfig.ReadDetailsFromXML(applicationPath, @"/configurations/mwlaetitle");
                _fileLogger.Information($"Starting MWL DICOM server on port {mwlPort}, AET={mwlAet}, backend={backend}");
                WorklistServer.Start(Convert.ToInt32(mwlPort), mwlAet, backend);
                _fileLogger.Information("WorklistServer.Start completed.");
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
        }
    }
}
