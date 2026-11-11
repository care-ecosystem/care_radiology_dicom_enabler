using System;
using System.Diagnostics;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace Plexus_MWL_Service
{
    /// <summary>
    /// Windows pre-shutdown for the MWL service. Windows tells services that accept it before the normal
    /// shutdown notification, and waits for them up to the pre-shutdown timeout, which is far longer than the
    /// few seconds a service gets at shutdown. The service then has time to unassign the DICOM Enabler in CARE.
    /// .NET's ServiceBase has no switch for this, so the accepted-controls flag is set through its private
    /// field; Windows then sends the pre-shutdown control to OnCustomCommand.
    /// </summary>
    internal static class ServicePreshutdown
    {
        /// <summary>The control code Windows sends to OnCustomCommand for pre-shutdown.</summary>
        public const int ControlPreshutdown = 0x0F;
        private const int AcceptPreshutdown = 0x100;
        private const int ServiceConfigPreshutdownInfo = 7;
        private const uint ScManagerConnect = 0x0001;
        private const uint ServiceChangeConfig = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct ServicePreshutdownInfo
        {
            public uint dwPreshutdownTimeout;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenSCManager(string machineName, string databaseName, uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenService(IntPtr scManager, string serviceName, uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, EntryPoint = "ChangeServiceConfig2W")]
        private static extern bool ChangeServiceConfig2(IntPtr service, int infoLevel, ref ServicePreshutdownInfo info);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CloseServiceHandle(IntPtr handle);

        /// <summary>
        /// Makes the service accept pre-shutdown. Call in the constructor, before the service starts, so
        /// Windows sees it in the controls the service reports. False (with error) when ServiceBase's
        /// field was not found; the service then only gets the normal shutdown notification.
        /// </summary>
        public static bool Accept(ServiceBase service, out string error)
        {
            error = null;
            FieldInfo field = typeof(ServiceBase).GetField("acceptedCommands", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                error = "ServiceBase.acceptedCommands not found";
                return false;
            }
            field.SetValue(service, (int)field.GetValue(service) | AcceptPreshutdown);
            return true;
        }

        /// <summary>
        /// Sets how long Windows waits for this service at pre-shutdown. The service's name is looked up by
        /// its process id, as it is installed under different names (CARE_MWL_Service, Care MWL SCP Service,
        /// CAREMWLci). Needs the right to change the service's configuration (the service runs as LocalSystem).
        /// Returns the service name, or null with error set.
        /// </summary>
        public static string SetTimeout(TimeSpan timeout, out string error)
        {
            error = null;
            string serviceName = FindOwnServiceName(out error);
            if (serviceName == null)
                return null;

            IntPtr scManager = OpenSCManager(null, null, ScManagerConnect);
            if (scManager == IntPtr.Zero)
            {
                error = "OpenSCManager failed with error " + Marshal.GetLastWin32Error();
                return null;
            }
            try
            {
                IntPtr service = OpenService(scManager, serviceName, ServiceChangeConfig);
                if (service == IntPtr.Zero)
                {
                    error = $"OpenService({serviceName}) failed with error {Marshal.GetLastWin32Error()}";
                    return null;
                }
                try
                {
                    var info = new ServicePreshutdownInfo { dwPreshutdownTimeout = (uint)timeout.TotalMilliseconds };
                    if (!ChangeServiceConfig2(service, ServiceConfigPreshutdownInfo, ref info))
                    {
                        error = $"Setting the pre-shutdown timeout of {serviceName} failed with error {Marshal.GetLastWin32Error()}";
                        return null;
                    }
                    return serviceName;
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scManager);
            }
        }

        private static string FindOwnServiceName(out string error)
        {
            error = null;
            try
            {
                int pid = Process.GetCurrentProcess().Id;
                using (var searcher = new ManagementObjectSearcher($"SELECT Name FROM Win32_Service WHERE ProcessId = {pid}"))
                {
                    foreach (ManagementObject service in searcher.Get())
                    {
                        using (service)
                            return service["Name"]?.ToString();
                    }
                }
                error = $"no Windows service runs in process {pid}";
                return null;
            }
            catch (Exception ex)
            {
                error = "Finding the service name failed with exception " + ex.Message;
                return null;
            }
        }
    }
}
