using System.IO;
using System.Reflection;
using Serilog;

namespace Plexus_MWL_Service.logs
{
    public class ucls_ReadWriteLog
    {
        private readonly ILogger _logger;

        public ucls_ReadWriteLog()
        {
            string logPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                "logs", "WorklistItems.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            _logger = new LoggerConfiguration()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, shared: true)
                .CreateLogger();
        }

        // isInfo=true -> Information level; isInfo=false -> Error level
        public void WriteToLog(string message, bool isInfo)
        {
            if (isInfo)
                _logger.Information(message);
            else
                _logger.Error(message);
        }
    }
}
