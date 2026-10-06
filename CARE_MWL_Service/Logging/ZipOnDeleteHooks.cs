using System.IO;
using System.IO.Compression;
using Serilog.Sinks.File;

namespace Plexus_MWL_Service.logs
{
    /// <summary>
    /// Zips a log file into logs/archive before Serilog's retention policy deletes it, so old logs
    /// are kept compressed instead of lost.
    /// </summary>
    public class ZipOnDeleteHooks : FileLifecycleHooks
    {
        public override void OnFileDeleting(string path)
        {
            // Several loggers share the file, so another one may already have archived and deleted it
            if (!File.Exists(path))
                return;

            string archiveDirectory = Path.Combine(Path.GetDirectoryName(path), "archive");
            Directory.CreateDirectory(archiveDirectory);
            // The last-write time keeps names unique when Serilog reuses a sequence number, and gives
            // loggers racing on the same file the same name, so only the first one creates the zip
            string zipPath = Path.Combine(archiveDirectory,
                Path.GetFileNameWithoutExtension(path) + "_" + File.GetLastWriteTime(path).ToString("yyyyMMdd-HHmmss") + ".zip");

            // CreateNew throws if the zip exists; Serilog then skips this delete and the file stays
            using (FileStream zipStream = new FileStream(zipPath, FileMode.CreateNew))
            {
                try
                {
                    using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                    using (Stream entryStream = archive.CreateEntry(Path.GetFileName(path), CompressionLevel.Optimal).Open())
                    // Other loggers may still hold the file open for writing
                    using (FileStream logStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        logStream.CopyTo(entryStream);
                    }
                }
                catch
                {
                    // Don't leave a broken zip behind; rethrowing makes Serilog keep the log file
                    zipStream.Dispose();
                    File.Delete(zipPath);
                    throw;
                }
            }
        }
    }
}
