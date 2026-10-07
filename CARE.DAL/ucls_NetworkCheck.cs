using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;

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

        // Short timeout so a check against an unreachable server does not hold up the upload cycle
        private static readonly HttpClient probeClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        /// <summary>
        /// True when the CARE server at baseUrl answers an HTTP request. Any response counts, even a
        /// 404, except 502/503/504: those come from a proxy in front of CARE when CARE itself is down.
        /// When it is not reachable, description says why for the log: a local network issue, an
        /// internet issue, or the CARE server itself being unreachable or unavailable.
        /// </summary>
        public static bool IsCareReachable(string baseUrl, out string description)
        {
            try
            {
                using (HttpResponseMessage response = probeClient.GetAsync(baseUrl, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                {
                    if (!IsCareUnavailableStatus(response.StatusCode))
                    {
                        description = "CARE server is reachable";
                        return true;
                    }
                    description = $"CARE server unavailable (HTTP {(int)response.StatusCode} {response.ReasonPhrase})";
                    return false;
                }
            }
            catch (Exception)
            {
                // No response at all: find out whether the local network or the internet is the cause
                HasNetworkIssue(out description);
                return false;
            }
        }

        /// <summary>
        /// 502/503/504: a proxy in front of CARE answered, but CARE itself is down or not responding.
        /// </summary>
        public static bool IsCareUnavailableStatus(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.BadGateway ||
                   statusCode == HttpStatusCode.ServiceUnavailable ||
                   statusCode == HttpStatusCode.GatewayTimeout;
        }

        // Named event the SCU service sets when it finds CARE reachable again after an outage, so the MWL
        // service refreshes its worklist straight away. Global so it works across the services, which all
        // run as LocalSystem.
        private const string CareReachableEventName = @"Global\CARE_DICOM_Enabler_CareReachable";

        /// <summary>
        /// Opens the CARE reachable event, creating it when no service has it open yet. Auto-reset: once
        /// set it stays set until the MWL service picks it up, even if MWL is busy at that moment.
        /// </summary>
        public static EventWaitHandle OpenCareReachableEvent()
        {
            return new EventWaitHandle(false, EventResetMode.AutoReset, CareReachableEventName);
        }

        /// <summary>
        /// Tells the MWL service that CARE is reachable again. False, with error for the log, when the
        /// event could not be set.
        /// </summary>
        public static bool SignalCareReachable(out string error)
        {
            error = null;
            try
            {
                using (EventWaitHandle careReachableEvent = OpenCareReachableEvent())
                    careReachableEvent.Set();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
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
