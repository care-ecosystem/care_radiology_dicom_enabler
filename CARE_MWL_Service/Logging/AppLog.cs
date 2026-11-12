using System;
using System.IO;
using System.Reflection;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Plexus_MWL_Service.logs
{
    /// <summary>
    /// Writes to the DICOM Enabler application's App Log (logs/yyyy-MM-dd/App.txt next to the EXE, shown in
    /// View Logs > App Log), for the service's work that belongs with the application's: setting the DICOM
    /// Enabler and its devices to assigned / unassigned in CARE when the service starts and stops. The
    /// application writes the same file at the same time, so it is opened shared, and the file settings and
    /// line format must stay the same as the application's cls_AppLog. Each entry starts with its area in
    /// brackets, e.g. "[ASSIGNMENT]".
    /// </summary>
    public static class AppLog
    {
        private const string FileName = "App";
        private const string ArchiveFolder = "AppLog";
        private const string DateFormat = "yyyy-MM-dd";
        private static readonly string LogsDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "logs");
        private static readonly object Lock = new object();
        private static Logger logger;
        private static DateTime loggerDate;

        public static void Info(string area, string message) => Write(LogEventLevel.Information, area, message);

        public static void Warning(string area, string message) => Write(LogEventLevel.Warning, area, message);

        public static void Error(string area, string message) => Write(LogEventLevel.Error, area, message);

        private static void Write(LogEventLevel level, string area, string message)
        {
            try
            {
                lock (Lock)
                {
                    // A new date folder with the first entry of each day, as the application does
                    DateTime today = DateTime.Now.Date;
                    if (logger == null || today != loggerDate)
                    {
                        logger?.Dispose();
                        loggerDate = today;
                        string logPath = Path.Combine(LogsDirectory, today.ToString(DateFormat), FileName + ".txt");
                        logger = new LoggerConfiguration()
                            .MinimumLevel.Verbose()
                            .WriteTo.File(logPath,
                                rollOnFileSizeLimit: true,
                                fileSizeLimitBytes: 10 * 1024 * 1024,
                                retainedFileCountLimit: 3,
                                shared: true,
                                hooks: new ZipOnDeleteHooks(ArchiveFolder))
                            .CreateLogger();
                    }
                    // Written as a plain value so braces in a message (e.g. a JSON error body) are not
                    // read as a message template
                    logger.Write(level, "[{Area:l}] {Message:l}", area, message ?? string.Empty);
                }
            }
            catch (Exception)
            {
                // Logging must never break the service
            }
        }
    }
}