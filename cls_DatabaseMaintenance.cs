using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using Plexus.Common.Database;
using Plexus_DICOM_Enabler.Forms;

namespace Plexus_DICOM_Enabler
{
    /// <summary>
    /// Database backup and restore, and service stop / start, for switching from the whole facility's
    /// worklist back to the DICOM Enabler's worklist. The backup is made like Initializer.bat makes it: a
    /// full mysqldump of the database to a new C:\ProgramData\CARE DICOM Enabler\Backups\&lt;yyyy-MM-dd_HH-mm-ss&gt;\
    /// folder (Administrators and SYSTEM only), so Initializer.bat can also restore it. Blocking (processes,
    /// services, database); call it off the UI thread, except the administrator checks.
    /// </summary>
    public static class cls_DatabaseMaintenance
    {
        private const string Area = "WORKLIST MODE";
        private const string DefaultDatabase = "plexus_mi2";

        /// <summary>The folder Initializer.bat and Reset.bat keep their backups in.</summary>
        public static string BackupRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CARE DICOM Enabler", "Backups");

        // ---------------------------------------------------------------- administrator

        /// <summary>True when the application runs as Administrator (needed to write to the Backups folder and to stop services).</summary>
        public static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Starts the application again as Administrator (Windows asks for permission). Returns false with
        /// error set when it could not be started, e.g. the user declined the Windows prompt.
        /// </summary>
        public static bool RestartAsAdministrator(out string error)
        {
            error = null;
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true, Verb = "runas" });
                return true;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                error = "Running as Administrator was declined in the Windows prompt.";
            }
            catch (Exception ex)
            {
                error = "The application could not be started as Administrator: " + ex.Message;
            }
            return false;
        }

        // ---------------------------------------------------------------- services

        /// <summary>
        /// Stops each of serviceNames that is installed and running, and returns the ones it stopped (to start
        /// them again later). On failure the ones already stopped are started again and error is set.
        /// </summary>
        public static List<string> StopServices(IEnumerable<string> serviceNames, out string error)
        {
            error = null;
            var stopped = new List<string>();
            try
            {
                var installed = new HashSet<string>(ServiceController.GetServices().Select(s => s.ServiceName), StringComparer.OrdinalIgnoreCase);
                foreach (string name in serviceNames.Where(installed.Contains))
                {
                    using (var service = new ServiceController(name))
                    {
                        if (service.Status == ServiceControllerStatus.Stopped)
                            continue;
                        if (service.Status != ServiceControllerStatus.StopPending)
                            service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
                        stopped.Add(name);
                        cls_AppLog.Info(Area, name + " stopped");
                    }
                }
                return stopped;
            }
            catch (Exception ex)
            {
                error = "The services could not be stopped (" + ex.Message + "). Stopping services needs the application to run as Administrator.";
                cls_AppLog.Error(Area, error);
                StartServices(stopped, out _);
                return null;
            }
        }

        /// <summary>Starts each of serviceNames. Returns false with error listing the ones that did not start.</summary>
        public static bool StartServices(IEnumerable<string> serviceNames, out string error)
        {
            var failed = new List<string>();
            foreach (string name in serviceNames ?? Enumerable.Empty<string>())
            {
                try
                {
                    using (var service = new ServiceController(name))
                    {
                        if (service.Status != ServiceControllerStatus.Running && service.Status != ServiceControllerStatus.StartPending)
                            service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60));
                        cls_AppLog.Info(Area, name + " started");
                    }
                }
                catch (Exception ex)
                {
                    failed.Add($"{name} ({ex.Message})");
                }
            }
            error = failed.Count == 0 ? null : "Not started: " + string.Join("; ", failed) + ". Start them from Server Manager.";
            if (error != null)
                cls_AppLog.Error(Area, error);
            return failed.Count == 0;
        }

        // ---------------------------------------------------------------- MySQL tools

        /// <summary>
        /// mysql.exe as Initializer.bat finds it: DICOM_MYSQL_BIN, then PATH, then under Program Files\MySQL.
        /// Null when it is not found.
        /// </summary>
        public static string FindMysqlExe()
        {
            string fromEnv = Environment.GetEnvironmentVariable("DICOM_MYSQL_BIN");
            if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
                return fromEnv;
            string onPath = FindOnPath("mysql.exe");
            if (onPath != null)
                return onPath;
            foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
            {
                string found = FindUnder(Path.Combine(root ?? string.Empty, "MySQL"), "mysql.exe");
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>mysqldump.exe next to mysql.exe, else on PATH. Null when it is not found.</summary>
        public static string FindMysqlDumpExe(string mysqlExe)
        {
            if (mysqlExe != null)
            {
                string beside = Path.Combine(Path.GetDirectoryName(mysqlExe), "mysqldump.exe");
                if (File.Exists(beside))
                    return beside;
            }
            return FindOnPath("mysqldump.exe");
        }

        private static string FindOnPath(string fileName)
        {
            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim().Trim('"'), fileName);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                    // an invalid PATH entry is skipped
                }
            }
            return null;
        }

        private static string FindUnder(string folder, string fileName)
        {
            if (!Directory.Exists(folder))
                return null;
            var pending = new Stack<string>();
            pending.Push(folder);
            while (pending.Count > 0)
            {
                string dir = pending.Pop();
                try
                {
                    string candidate = Path.Combine(dir, fileName);
                    if (File.Exists(candidate))
                        return candidate;
                    foreach (string sub in Directory.GetDirectories(dir))
                        pending.Push(sub);
                }
                catch
                {
                    // a folder that cannot be read is skipped
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- backup / restore

        /// <summary>
        /// Full mysqldump of the database (every table and row, routines, triggers), like Initializer.bat's,
        /// to a new Backups\&lt;yyyy-MM-dd_HH-mm-ss&gt;\&lt;database&gt;.sql. Written as .partial and renamed only once
        /// complete. Returns the backup file, or null with error set.
        /// </summary>
        public static string BackupDatabase(out string error)
        {
            error = null;
            string mysqlExe = FindMysqlExe();
            string dumpExe = FindMysqlDumpExe(mysqlExe);
            if (dumpExe == null)
            {
                error = "mysqldump.exe was not found (next to mysql.exe, on PATH or under Program Files\\MySQL), so the database cannot be backed up.";
                return null;
            }
            if (!TryGetConnection(out MySqlConnectionStringBuilder conn, out error))
                return null;

            string backupDir;
            try
            {
                backupDir = CreateBackupDir();
            }
            catch (Exception ex)
            {
                error = "The backup folder could not be created: " + ex.Message;
                return null;
            }

            string database = string.IsNullOrWhiteSpace(conn.Database) ? DefaultDatabase : conn.Database;
            string dumpFile = Path.Combine(backupDir, database + ".sql");
            string partialFile = dumpFile + ".partial";
            // Same options as Initializer.bat: --databases adds CREATE DATABASE / USE so it restores after the
            // database is dropped; --set-gtid-purged=OFF leaves out GTID_PURGED, which fails a restore otherwise
            string args = ConnectionArgs(conn) + " --single-transaction --routines --triggers --set-gtid-purged=OFF " +
                          $"--result-file=\"{partialFile}\" --databases {database}";
            if (!RunTool(dumpExe, args, conn.Password, null, out string output, out int exitCode))
            {
                error = "mysqldump could not be run: " + output;
                return null;
            }
            if (exitCode != 0 || !EndsWithDumpCompleted(partialFile))
            {
                error = $"mysqldump failed (exit code {exitCode}) or the backup is incomplete: {Truncate(output, 500)}";
                return null;
            }
            try
            {
                File.Move(partialFile, dumpFile);
            }
            catch (Exception ex)
            {
                error = $"The backup was taken but could not be renamed to {dumpFile}: {ex.Message}";
                return null;
            }
            cls_AppLog.Info(Area, $"Database '{database}' backed up ({new FileInfo(dumpFile).Length / 1024} KB) to {dumpFile}");
            return dumpFile;
        }

        /// <summary>
        /// Restores a backup made by BackupDatabase (or Initializer.bat / Reset.bat) with mysql.exe: its tables
        /// replace the current ones. Returns false with error set.
        /// </summary>
        public static bool RestoreDatabase(string backupFile, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(backupFile) || !File.Exists(backupFile))
            {
                error = "The backup file was not found: " + backupFile;
                return false;
            }
            string mysqlExe = FindMysqlExe();
            if (mysqlExe == null)
            {
                error = "mysql.exe was not found (DICOM_MYSQL_BIN, PATH or under Program Files\\MySQL), so the backup cannot be restored.";
                return false;
            }
            if (!TryGetConnection(out MySqlConnectionStringBuilder conn, out error))
                return false;
            if (!RunTool(mysqlExe, ConnectionArgs(conn) + " --default-character-set=utf8mb4", conn.Password, backupFile, out string output, out int exitCode))
            {
                error = "mysql could not be run: " + output;
                return false;
            }
            if (exitCode != 0)
            {
                error = $"Restoring the backup failed (exit code {exitCode}): {Truncate(output, 500)}";
                return false;
            }
            cls_AppLog.Info(Area, "Database restored from " + backupFile);
            return true;
        }

        private static bool TryGetConnection(out MySqlConnectionStringBuilder conn, out string error)
        {
            conn = null;
            error = null;
            var dal = new ucls_DAL(Global._applicationPath);
            try
            {
                conn = new MySqlConnectionStringBuilder(dal.GetPlainConnectionString());
                return true;
            }
            catch (Exception ex)
            {
                error = "The database connection settings could not be read from cfg/common.cfg: " + ex.Message;
                return false;
            }
            finally
            {
                dal.Dispose();
            }
        }

        private static string ConnectionArgs(MySqlConnectionStringBuilder conn)
        {
            string host = string.IsNullOrWhiteSpace(conn.Server) ? "localhost" : conn.Server.Split(',')[0].Trim();
            return $"--host={host} --port={conn.Port} --user=\"{conn.UserID}\"";
        }

        // The Backups root gets Full Control for Administrators and SYSTEM only, with no inherited entries,
        // as Initializer.bat and Reset.bat set it, so every backup folder inherits that
        private static string CreateBackupDir()
        {
            Directory.CreateDirectory(BackupRoot);
            var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var acl = new DirectorySecurity();
            acl.SetAccessRuleProtection(true, false);
            acl.SetOwner(admins);
            foreach (SecurityIdentifier sid in new[] { admins, system })
                acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.SetAccessControl(BackupRoot, acl);

            string dir = Path.Combine(BackupRoot, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        // Runs a MySQL tool with the password in MYSQL_PWD (not on the command line), optionally feeding
        // inputFile to its standard input. output is its error output (or the reason it could not run).
        private static bool RunTool(string exe, string args, string password, string inputFile, out string output, out int exitCode)
        {
            exitCode = -1;
            try
            {
                var info = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = inputFile != null,
                };
                info.EnvironmentVariables["MYSQL_PWD"] = password ?? string.Empty;
                using (Process process = Process.Start(info))
                {
                    var stderr = process.StandardError.ReadToEndAsync();
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    if (inputFile != null)
                    {
                        using (FileStream input = File.OpenRead(inputFile))
                            input.CopyTo(process.StandardInput.BaseStream);
                        process.StandardInput.Close();
                    }
                    if (!process.WaitForExit((int)TimeSpan.FromMinutes(30).TotalMilliseconds))
                    {
                        try { process.Kill(); } catch { }
                        output = "it did not finish within 30 minutes";
                        return false;
                    }
                    exitCode = process.ExitCode;
                    // The password warning mysql prints is not an error
                    output = string.Join(" ", (stderr.Result + " " + stdout.Result)
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(line => !line.Contains("Using a password on the command line"))).Trim();
                    return true;
                }
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return false;
            }
        }

        private static bool EndsWithDumpCompleted(string file)
        {
            try
            {
                if (!File.Exists(file))
                    return false;
                using (var stream = File.OpenRead(file))
                {
                    long length = stream.Length;
                    int tail = (int)Math.Min(512, length);
                    stream.Seek(length - tail, SeekOrigin.Begin);
                    var buffer = new byte[tail];
                    int read = stream.Read(buffer, 0, tail);
                    return Encoding.UTF8.GetString(buffer, 0, read).Contains("Dump completed");
                }
            }
            catch
            {
                return false;
            }
        }

        private static string Truncate(string text, int max)
        {
            text = text ?? string.Empty;
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }
    }
}
