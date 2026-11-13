using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Windows.Forms;

namespace Plexus_DICOM_Enabler
{
    /// <summary>
    /// Administrator check and service stop / start, for switching from the whole facility's worklist back to
    /// the DICOM Enabler's worklist: the services are stopped while its data is cleaned and started again after.
    /// Blocking (services); call it off the UI thread, except the administrator checks.
    /// </summary>
    public static class cls_ServiceControl
    {
        private const string Area = "WORKLIST MODE";

        // ---------------------------------------------------------------- administrator

        /// <summary>True when the application runs as Administrator (needed to stop and start the services).</summary>
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

        // How long a service may take to stop. The MWL service sets its DICOM Enabler and every saved device to
        // unassigned in CARE while it stops (one request each), so it can take well over a minute.
        private static readonly TimeSpan StopTimeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Stops each of serviceNames that is installed and running, and returns the ones it stopped (to start
        /// them again later). A service is recorded as soon as its stop is requested, so on failure (e.g. one
        /// does not stop within StopTimeout, or stopping is not allowed) every service whose stop was requested
        /// is recovered: once it has finished stopping it is started again. Returns null with error set then.
        /// </summary>
        public static List<string> StopServices(IEnumerable<string> serviceNames, out string error)
        {
            error = null;
            var requested = new List<string>();
            try
            {
                var installed = new HashSet<string>(ServiceController.GetServices().Select(s => s.ServiceName), StringComparer.OrdinalIgnoreCase);
                foreach (string name in serviceNames.Where(installed.Contains))
                {
                    using (var service = new ServiceController(name))
                    {
                        if (service.Status == ServiceControllerStatus.Stopped)
                            continue;
                        requested.Add(name);
                        if (service.Status != ServiceControllerStatus.StopPending)
                            service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, StopTimeout);
                        cls_AppLog.Info(Area, name + " stopped");
                    }
                }
                return requested;
            }
            catch (Exception ex)
            {
                error = "The services could not be stopped (" + ex.Message + "). Stopping services needs the application to run as Administrator.";
                cls_AppLog.Error(Area, error);
                string recoveryError = RecoverServices(requested);
                if (recoveryError != null)
                    error += " " + recoveryError;
                return null;
            }
        }

        // After a failed stop: waits for each service whose stop was requested to finish stopping (or to be found
        // still running), then starts the ones that stopped, so none is left offline. Returns null, or what could
        // not be recovered.
        private static string RecoverServices(IEnumerable<string> serviceNames)
        {
            var toStart = new List<string>();
            var stuck = new List<string>();
            foreach (string name in serviceNames)
            {
                try
                {
                    using (var service = new ServiceController(name))
                    {
                        var deadline = DateTime.UtcNow + StopTimeout;
                        service.Refresh();
                        while (service.Status == ServiceControllerStatus.StopPending && DateTime.UtcNow < deadline)
                        {
                            System.Threading.Thread.Sleep(1000);
                            service.Refresh();
                        }
                        if (service.Status == ServiceControllerStatus.Stopped)
                            toStart.Add(name);
                        else if (service.Status == ServiceControllerStatus.StopPending)
                            stuck.Add(name);
                        // Running (its stop was refused) or starting: nothing to recover
                    }
                }
                catch (Exception ex)
                {
                    stuck.Add($"{name} ({ex.Message})");
                }
            }
            var problems = new List<string>();
            if (toStart.Count > 0 && !StartServices(toStart, out string startError))
                problems.Add(startError);
            if (stuck.Count > 0)
                problems.Add("Still stopping, so not started again: " + string.Join("; ", stuck) + ". Start them from Server Manager once they have stopped.");
            string result = problems.Count == 0 ? null : string.Join(" ", problems);
            if (result != null)
                cls_AppLog.Error(Area, result);
            else if (toStart.Count > 0)
                cls_AppLog.Info(Area, "Started again after the failed stop: " + string.Join(", ", toStart));
            return result;
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

    }
}
