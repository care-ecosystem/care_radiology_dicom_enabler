

using System;
using System.Configuration;
using System.Diagnostics;
using System.ServiceProcess;
using Worklist_SCP;
using Plexus.Common.config;
using System.IO;
using System.Reflection;

namespace Plexus_MWL_Service
{
    public partial class PlexusMWLService : ServiceBase
    {
        EventLog _objEventLog;
        public PlexusMWLService()
        {
            InitializeComponent();
            _objEventLog = new EventLog();
            _objEventLog.Source = "Pellucid_MWL_SCP";
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                //_objEventLog.WriteEntry("Try Checking the Backend", EventLogEntryType.Information);
                string applicationPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                int backend = Convert.ToInt32(ConfigurationManager.AppSettings["backend"].ToString());
                WorklistServer.Start(Convert.ToInt32(cls_PlexusConfig.ReadDetailsFromXML(applicationPath, @"/configurations/mwlport")),
                   cls_PlexusConfig.ReadDetailsFromXML(applicationPath,@"/configurations/mwlaetitle"), backend);

                _objEventLog.WriteEntry("Plexus MWL SCP Service Started Successfully !!!", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                _objEventLog.WriteEntry("Starting Plexus MWL SCP Service failed with Exception :" + ex.Message, EventLogEntryType.Error);
            }
        }

        protected override void OnStop()
        {
            try
            {
                WorklistServer.Stop();
                _objEventLog.WriteEntry("Plexus MWL SCPService Stopped Successfully !!!", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                _objEventLog.WriteEntry("MWL SCPService Stopped Failed with error Message : " + ex.Message, EventLogEntryType.Error);
            }
        }
    }
}
