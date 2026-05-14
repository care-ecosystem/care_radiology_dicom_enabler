using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using System.Diagnostics;

namespace Plexus_MWL_Service.logs
{
    class ucls_ReadWriteLog
    {

        public static Serilog.ILogger fileLogger = null;

        public ucls_ReadWriteLog()
        {
            fileLogger = GetFileLogger();
        }

        private Serilog.ILogger GetFileLogger()
        {
            //WriteToLog(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),true);
            string logFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "logs/ModalitySCP.txt");
            return new LoggerConfiguration().
                WriteTo.File(logFilePath,
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
                shared: true,
                retainedFileCountLimit: 3,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 5120)
                .CreateLogger();
        }

        public void WriteToLog(string logString, bool bInfo)
        {
            bool writeEventLog = Convert.ToBoolean(ConfigurationManager.AppSettings["eventlog"].ToString());

            if (writeEventLog)
            {
                
                EventLog objEventLog = new EventLog();
                objEventLog.Source = "Pellucid_MWL_SCP";
                objEventLog.WriteEntry(logString, bInfo ? EventLogEntryType.Information : EventLogEntryType.Error);
            }
            if (bInfo)
            {
                fileLogger.Information(logString);
            }
            else
            {
                fileLogger.Error(logString);
            }
        }
    }
}
