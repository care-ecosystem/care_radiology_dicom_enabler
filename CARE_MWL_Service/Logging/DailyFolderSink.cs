using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace Plexus_MWL_Service.logs
{
    /// <summary>
    /// Writes to logs/yyyy-MM-dd/fileName next to the service EXE, moving to a new date folder with the
    /// first event of each day. Within a day the file rolls at 5 KB; the newest 3 files stay as .txt and
    /// older ones are zipped into that day's archive folder by ZipOnDeleteHooks. Earlier days' folders
    /// are zipped to logs/yyyy-MM-dd.zip and deleted.
    /// </summary>
    public class DailyFolderSink : ILogEventSink, IDisposable
    {
        private const string DateFormat = "yyyy-MM-dd";
        // The MWL, StoreSCP and StoreSCU services share the logs folder, so only one of them archives at a time
        private const string ArchiveMutexName = @"Global\CARE_DICOM_Enabler_LogArchive";
        private static readonly string LogsDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "logs");

        private readonly string _fileName;
        private readonly object _lock = new object();
        private DateTime _currentDate;
        private Logger _fileLogger;

        public DailyFolderSink(string fileName)
        {
            _fileName = fileName;

            // Services build their logger at startup, so the logs folder exists before anything is written
            try
            {
                Directory.CreateDirectory(LogsDirectory);
            }
            catch (Exception ex)
            {
                // The file sink retries creating it on the first write
                SelfLog.WriteLine("Creating the logs folder {0} failed: {1}", LogsDirectory, ex);
            }
        }

        public void Emit(LogEvent logEvent)
        {
            lock (_lock)
            {
                // Serilog's file sink keeps one folder for its lifetime, so open a new one when the day changes
                DateTime date = logEvent.Timestamp.LocalDateTime.Date;
                if (_fileLogger == null || date != _currentDate)
                {
                    _fileLogger?.Dispose();
                    _currentDate = date;
                    string logPath = Path.Combine(LogsDirectory, date.ToString(DateFormat), _fileName);
                    _fileLogger = new LoggerConfiguration()
                        .MinimumLevel.Verbose()
                        .WriteTo.File(logPath,
                            shared: true,
                            retainedFileCountLimit: 3,
                            rollOnFileSizeLimit: true,
                            fileSizeLimitBytes: 5120,
                            hooks: new ZipOnDeleteHooks())
                        .CreateLogger();

                    // Off the logging thread; also catches days the service was stopped over midnight
                    Task.Run(() => ArchivePreviousDays(date));
                }
                _fileLogger.Write(logEvent);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _fileLogger?.Dispose();
                _fileLogger = null;
            }
        }

        private static void ArchivePreviousDays(DateTime today)
        {
            try
            {
                using (Mutex mutex = new Mutex(false, ArchiveMutexName))
                {
                    try
                    {
                        mutex.WaitOne();
                    }
                    catch (AbandonedMutexException)
                    {
                        // The previous owner exited mid-archive; we own the mutex now and the next pass redoes its work
                    }
                    try
                    {
                        foreach (string dayFolder in Directory.GetDirectories(LogsDirectory))
                        {
                            if (DateTime.TryParseExact(Path.GetFileName(dayFolder), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime day)
                                && day < today)
                                ArchiveDayFolder(dayFolder);
                        }
                    }
                    finally
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("Archiving earlier log folders failed: {0}", ex);
            }
        }

        /// <summary>
        /// Adds every file in the folder to yyyy-MM-dd.zip beside it, then deletes the folder. A file another
        /// logger still has open cannot be deleted; it stays and is zipped again, replacing its earlier copy,
        /// when that logger moves to the new day and archives again.
        /// </summary>
        private static void ArchiveDayFolder(string dayFolder)
        {
            using (FileStream zipStream = new FileStream(dayFolder + ".zip", FileMode.OpenOrCreate))
            using (ZipArchive zip = new ZipArchive(zipStream, ZipArchiveMode.Update))
            {
                foreach (string file in Directory.GetFiles(dayFolder, "*", SearchOption.AllDirectories))
                {
                    string entryName = file.Substring(dayFolder.Length + 1).Replace('\\', '/');
                    zip.GetEntry(entryName)?.Delete();
                    using (Stream entryStream = zip.CreateEntry(entryName, CompressionLevel.Optimal).Open())
                    using (FileStream logStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        logStream.CopyTo(entryStream);
                    }
                }
            }

            // Only delete once the zip has been written and closed
            foreach (string file in Directory.GetFiles(dayFolder, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // Still open in another logger
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            try
            {
                Directory.Delete(dayFolder, true);
            }
            catch (IOException)
            {
                // Files left above keep the folder until a later pass
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
