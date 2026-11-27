using System;
using System.Collections.Concurrent;
using Serilog;
using Serilog.Events;

namespace Plexus_MWL_Service.logs
{
    public class ucls_ReadWriteLog
    {
        private static readonly TimeSpan ThrottleInterval = TimeSpan.FromHours(1);
        private static readonly ConcurrentDictionary<string, DateTime> LastWrittenUtc = new ConcurrentDictionary<string, DateTime>(StringComparer.Ordinal);

        private readonly ILogger _logger;

        public ucls_ReadWriteLog()
        {
            _logger = DailyFolderSink.CreateLogger("ModalitySCP.txt");
        }

        // isInfo=true -> Information level; isInfo=false -> Error level
        public void WriteToLog(string message, bool isInfo)
        {
            if (isInfo)
                _logger.Information(message);
            else
                _logger.Error(message);
        }

        /// <summary>Detail written by a Debug build only.</summary>
        public void Debug(string message)
        {
            _logger.Debug(message);
        }

        /// <summary>
        /// Writes a problem that repeats on every refresh at most once an hour for the same text, so it
        /// stays visible without filling the log; the repeats in between are Debug detail.
        /// </summary>
        public void WriteThrottled(LogEventLevel level, string message)
        {
            DateTime now = DateTime.UtcNow;
            bool write = false;
            LastWrittenUtc.AddOrUpdate(message,
                key => { write = true; return now; },
                (key, last) =>
                {
                    if (now - last < ThrottleInterval)
                        return last;
                    write = true;
                    return now;
                });
            if (LastWrittenUtc.Count > 1000)
                LastWrittenUtc.Clear();
            _logger.Write(write ? level : LogEventLevel.Debug, message);
        }
    }
}
