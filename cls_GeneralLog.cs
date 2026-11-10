using System;
using System.IO;
using System.Windows.Forms;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Plexus_DICOM_Enabler
{
    /// <summary>
    /// The DICOM Enabler application's own log, shown in View Logs > General. It is for anything the
    /// application does that is not a service's work: the CARE Modalities API calls (DICOM Enablers and
    /// DICOM devices), connection failures, assignment status changes, changes CARE reports for the
    /// devices, and configuration saves. Written to logs/yyyy-MM-dd/General.txt next to the EXE, in the
    /// same format and folders as the service logs, so the log viewer reads it the same way. Each entry
    /// starts with its area in brackets, e.g. "[MODALITIES]", so the log can be searched by area.
    /// </summary>
    public static class cls_GeneralLog
    {
        /// <summary>File name prefix; the log viewer finds the parts as General*.txt.</summary>
        public const string FileName = "General";

        private const string DateFormat = "yyyy-MM-dd";
        private static readonly object Lock = new object();
        private static Logger logger;
        private static DateTime loggerDate;

        public static void Info(string area, string message) => Write(LogEventLevel.Information, area, message);

        public static void Warning(string area, string message) => Write(LogEventLevel.Warning, area, message);

        public static void Error(string area, string message) => Write(LogEventLevel.Error, area, message);

        /// <summary>
        /// The writeToLog callback the CARE.DAL API clients take: isInfo true logs Information, false logs
        /// Error.
        /// </summary>
        public static Action<string, bool> For(string area)
        {
            return (message, isInfo) => Write(isInfo ? LogEventLevel.Information : LogEventLevel.Error, area, message);
        }

        private static void Write(LogEventLevel level, string area, string message)
        {
            try
            {
                lock (Lock)
                {
                    // A new date folder with the first entry of each day, as the services do; the
                    // services zip the earlier days' folders
                    DateTime today = DateTime.Now.Date;
                    if (logger == null || today != loggerDate)
                    {
                        logger?.Dispose();
                        loggerDate = today;
                        string logPath = Path.Combine(Application.StartupPath, "logs", today.ToString(DateFormat), FileName + ".txt");
                        logger = new LoggerConfiguration()
                            .MinimumLevel.Verbose()
                            .WriteTo.File(logPath,
                                rollOnFileSizeLimit: true,
                                fileSizeLimitBytes: 1024 * 1024,
                                retainedFileCountLimit: 10)
                            .CreateLogger();
                    }
                    // Written as a plain value so braces in a message (e.g. a JSON error body) are not
                    // read as a message template
                    logger.Write(level, "[{Area:l}] {Message:l}", area, message ?? string.Empty);
                }
            }
            catch (Exception)
            {
                // Logging must never break the application
            }
        }

        /// <summary>Flushes and closes the file; called when the application exits.</summary>
        public static void Close()
        {
            lock (Lock)
            {
                logger?.Dispose();
                logger = null;
            }
        }
    }
}
