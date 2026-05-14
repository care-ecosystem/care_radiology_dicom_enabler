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

namespace Test_SeriLog.logs
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
            return new LoggerConfiguration().
                WriteTo.File("logs/TestSERILOG.txt",
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
                shared: true,
                retainedFileCountLimit : 3,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 1024)
                .CreateLogger();
        }

        public void WriteToLog(string logString, bool bInfo)
        {
            //bool writeEventLog = Convert.ToBoolean(ConfigurationManager.AppSettings["eventlog"].ToString());

            //if (writeEventLog)
            //{
                
            //    EventLog objEventLog = new EventLog();
            //    objEventLog.Source = "Pellucid_MWL_SCP";
            //    objEventLog.WriteEntry(logString, bInfo ? EventLogEntryType.Information : EventLogEntryType.Error);
            //}
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
