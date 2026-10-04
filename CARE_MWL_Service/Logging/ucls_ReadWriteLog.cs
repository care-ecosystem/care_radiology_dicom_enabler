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
            _logger = new LoggerConfiguration()
                .WriteTo.Sink(new DailyFolderSink("ModalitySCP.txt"), Serilog.Events.LogEventLevel.Information)
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
