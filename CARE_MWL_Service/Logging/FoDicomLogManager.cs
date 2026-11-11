using System;
using FellowOakDicom.Log;
using Serilog;

namespace Plexus_MWL_Service.logs
{
    /// <summary>
    /// Sends fo-dicom's own warnings and errors (e.g. a reply that could not be sent, a PDU that could not be
    /// read) to ModalitySCP.txt. The console logger used before is never seen in a Windows service.
    /// </summary>
    public class FoDicomLogManager : LogManager
    {
        private static readonly FoDicomLogger Instance = new FoDicomLogger();

        protected override Logger GetLoggerImpl(string name)
        {
            return Instance;
        }

        private class FoDicomLogger : Logger
        {
            private readonly Serilog.ILogger _fileLogger = new LoggerConfiguration()
                .WriteTo.Sink(DailyFolderSink.For("ModalitySCP.txt"), Serilog.Events.LogEventLevel.Information)
                .CreateLogger();

            public override void Log(LogLevel level, string msg, params object[] args)
            {
                // Debug and Info are per message and PDU: too much for the log file
                if (level < LogLevel.Warning)
                    return;
                string text;
                try
                {
                    text = args == null || args.Length == 0 ? msg : string.Format(NameFormatToPositionalFormat(msg), args);
                }
                catch (FormatException)
                {
                    text = msg + " " + string.Join(", ", args);
                }
                if (level == LogLevel.Warning)
                    _fileLogger.Warning("[fo-dicom] " + text);
                else
                    _fileLogger.Error("[fo-dicom] " + text);
            }
        }
    }
}
