using Serilog;
using System;
using System.IO;

namespace Test_SeriLog.logs
{
    public class ucls_ReadWriteLog
    {
        private static readonly ILogger _log;

        static ucls_ReadWriteLog()
        {
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            _log = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(logDir, "TestSERILOG.txt"), rollingInterval: RollingInterval.Day, shared: true)
                .CreateLogger();
        }

        public void WriteToLog(string message, bool isSuccess)
        {
            if (isSuccess)
                _log.Information(message);
            else
                _log.Error(message);
        }
    }
}
