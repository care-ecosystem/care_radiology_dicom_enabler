using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace Plexus.Common.Database
{
    /// <summary>
    /// Works out whether a CARE API call that got no response at all (connection failure or timeout)
    /// was caused by this machine's local network or by its internet connection, so the log says which
    /// one it was. A call that gets any HTTP response reached CARE, so it is not checked. Shared by the
    /// MWL and SCU services.
    /// </summary>
    public static class ucls_NetworkCheck
    {
        /// <summary>
        /// Describes the network state for the log: see HasNetworkIssue.
        /// </summary>
        public static string Describe()
        {
            HasNetworkIssue(out string description);
            return description;
        }

        /// <summary>
        /// True when there is a local network issue (no network adapter with a default gateway is up,
        /// or no gateway answers a ping) or an internet issue (the gateway answers but google.com does
        /// not). description says which one for the log.
        /// </summary>
        public static bool HasNetworkIssue(out string description)
        {
            try
            {
                var gateways = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                    .Select(g => g.Address)
                    .Where(a => !a.Equals(IPAddress.Any) && !a.Equals(IPAddress.IPv6Any))
                    .ToList();

                if (gateways.Count == 0)
                {
                    description = "Local network issue (no network adapter with a default gateway is up)";
                    return true;
                }
                if (!gateways.Any(gateway => PingSucceeds(gateway.ToString())))
                {
                    description = $"Local network issue (default gateway {string.Join(", ", gateways)} does not answer a ping)";
                    return true;
                }
                if (!PingSucceeds("google.com"))
                {
                    description = "Internet issue (google.com does not answer a ping)";
                    return true;
                }
                description = "CARE server unreachable (local network and internet are up)";
                return false;
            }
            catch (Exception ex)
            {
                description = "Network check failed: " + ex.Message;
                return false;
            }
        }

        private static bool PingSucceeds(string host)
        {
            try
            {
                using (var ping = new Ping())
                    return ping.Send(host, 3000).Status == IPStatus.Success;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
