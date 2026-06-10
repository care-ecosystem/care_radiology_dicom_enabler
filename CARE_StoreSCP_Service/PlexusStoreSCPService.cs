using FellowOakDicom;
using FellowOakDicom.Log;
using FellowOakDicom.Network;
using Plexus.Common.config;
using Plexus_StoreSCP_Service.Network;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Plexus_StoreSCP_Service
{
    public partial class PlexusStoreSCPService : ServiceBase
    {
        private static IDicomServer _server;
        private Serilog.ILogger _fileLogger;
        public PlexusStoreSCPService()
        {
            InitializeComponent();
            _fileLogger = GetFileLogger();
        }


        /// <summary>
        /// Get FIle Loger to Write to file
        /// </summary>
        /// <returns></returns>
        private Serilog.ILogger GetFileLogger()
        {
            //WriteToLog(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),true);
            string logFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "logs/StoreSCP.txt");
            return new LoggerConfiguration().
                WriteTo.File(logFilePath,
                shared: true,
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
                rollOnFileSizeLimit: false,
                fileSizeLimitBytes: 10240000)
                .CreateLogger();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                string applicationpath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                Global._storagePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "SCP"); ;
                Global._aeTitle = cls_PlexusConfig.ReadDetailsFromXML(applicationpath,@"/configurations/sscpaetitle");
                int port = Convert.ToInt32(cls_PlexusConfig.ReadDetailsFromXML(applicationpath, @"/configurations/sscpport"));

                new DicomSetupBuilder()
                    .RegisterServices(s => s.AddFellowOakDicom().AddLogManager<ConsoleLogManager>())
                    .Build();
                _server = DicomServerFactory.Create<CStoreSCP>(port);

                if (_server != null)
                {
                    WriteToLog("Store SCP Started Successfully !!!",true);
                }
                }
            catch (Exception ex)
            {
                WriteToLog("Error Starting Store SCP Service with exception : " + ex.Message,false);
            }
        }

        protected override void OnStop()
        {
            WriteToLog("Store SCP Stopped Successfully !!!",true);
        }

        /// <summary>
        /// Writelog in File and Event Log based on the configuration
        /// </summary>
        /// <param name="logString"></param>
        /// <param name="bInfo"></param>
        public void WriteToLog(string logString, bool bInfo)
        {
            if (bInfo)
                _fileLogger.Information(logString);
            else
                _fileLogger.Error(logString);
        }
    }
}
