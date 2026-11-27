using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Serilog;
using System.Text.Json;
using System.Xml;
using System.Reflection;
using Plexus.Common;

namespace Plexus_Auth_Service
{
    public partial class PlexusAuthService : ServiceBase
    {
        public static Serilog.ILogger fileLogger = null;
        private string lastAuthFailure;
        Timer timer = new Timer(TimeSpan.FromHours(24).TotalMilliseconds);
        public PlexusAuthService()
        {
            InitializeComponent();

            fileLogger = GetFileLogger();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            try 
            { 
                if (fileLogger == null )
                {
                    fileLogger = GetFileLogger();
                }
                WriteToLog("Auth service started", true);
                timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
                timer.Interval = 5000; //
                timer.Enabled = true;
            }
            catch(Exception ex)
            {
                WriteToLog("Auth service failed to start: " + ex.GetBaseException().Message + ". Check the user details and restart the service", false);
            }
        }

        /// <summary>
        /// Get FIle Loger to Write to file
        /// </summary>
        /// <returns></returns>
        private Serilog.ILogger GetFileLogger()
        {
            //WriteToLog(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),true);
            string logFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "logs/authLogs.txt");

            // The logger is built at startup, so the logs folder exists before anything is written
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));
            }
            catch (Exception ex)
            {
                // The file sink retries creating it on the first write
                Serilog.Debugging.SelfLog.WriteLine("Creating the logs folder for {0} failed: {1}", logFilePath, ex);
            }

            // A Debug build writes every detail (Debug lines); a Release build Information and above only
#if DEBUG
            const Serilog.Events.LogEventLevel minimumLevel = Serilog.Events.LogEventLevel.Debug;
#else
            const Serilog.Events.LogEventLevel minimumLevel = Serilog.Events.LogEventLevel.Information;
#endif
            return new LoggerConfiguration()
                .MinimumLevel.Is(minimumLevel).
                WriteTo.File(logFilePath,
                shared: true,
                restrictedToMinimumLevel: minimumLevel,
                //rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: false,
                fileSizeLimitBytes: 10240000)
                .CreateLogger();
        }

        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            //WriteToFile("Service is recall at " + DateTime.Now);
            WriteDebug("Try Authenticatin User");
            string errorString = string.Empty;
            if (CheckValidUser(ref errorString))
            {
                // Every check would repeat it: a Release build logs only a recovery after a failure
                if (lastAuthFailure != null)
                    WriteToLog("Authentication successful again (it failed before: " + lastAuthFailure + ")", true);
                else
                    WriteDebug("Authentication Successfull !!");
                lastAuthFailure = null;
            }
            else
            {
                
                if (errorString != string.Empty)
                {
                    // One line: the failure and what is done about it
                    if (lastAuthFailure != errorString)
                        WriteToLog("Authentication failed: " + errorString +
                                   " - Store SCP, MWL SCP and Store SCU services stopped. Check the network, the server url and the userName / password, then start the services again", false);
                    lastAuthFailure = errorString;

                    // Stop Pellucid Store SCP Service
                    WriteDebug("Stopping Plexus Store SCP Service");
                    StopService("Plexus Store SCP Service");
                    WriteDebug("Stopping Plexus Modality SCP Service");
                    StopService("Plexus MWL SCP Service");
                    WriteDebug("Stopping StoreSCU Service");
                    StopService("Plexus StoreSCU Service");

                    
                    return;
                }
                if (lastAuthFailure != "Invalid Username or Password.")
                    WriteToLog("Authentication failed: invalid username or password. Check the username and password", false);
                lastAuthFailure = "Invalid Username or Password.";
            }
        }

        private void StopService(string serviceName)
        {
            try
            {
                ServiceController service = new ServiceController(serviceName);
                if ((service.Status.Equals(ServiceControllerStatus.Running)))
                {
                    service.Stop();
                }
            }
            catch(Exception ex)
            {
                WriteToLog($"Stopping {serviceName} failed: {ex.Message}. Stop it in Windows Services", false);
            }
            
        }


        /// <summary>
        /// 
        /// </summary>
        /// 
        private bool CheckValidUser(ref string errorString)
        {

            string authURL = string.Empty, deviceName = string.Empty, userName = string.Empty, password = string.Empty;

            GetUserDetailsFromConfig(ref userName, ref password,ref deviceName,ref authURL);

            WriteDebug($"Authenticating username {userName} for device {deviceName}");

            try
            {
                //WriteToLog(authURL, true);
                WebRequest authReq = WebRequest.Create(authURL);
                authReq.Method = "POST";
                authReq.ContentType = "application/json";
                //WriteToLog(password, true);
                //    {
                //       "device": "ARaja",
                //       "name": "alagaraja",
                //       "password": "Plexus@123"
                //     }
                //string postData = string.Empty;//"{\"device\":\"" + device + "\",\"name\":\"" + mtb_Username.Text + "\",\"password\":\"" + mtxtb_Password.Text + "\"}";

                string postData = "{\"device\":\"" + deviceName + "\",\"name\":\"" + userName + "\",\"password\":\"" + password + "\"}";

                //WriteToLog(postData, true);

                using (var streamWriter = new StreamWriter(authReq.GetRequestStream()))
                {
                    streamWriter.Write(postData);
                    streamWriter.Flush();
                    streamWriter.Close();

                    var httpResponse = (HttpWebResponse)authReq.GetResponse();

                    using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        var resultData = streamReader.ReadToEnd();
                        //var serializer = new JavaScriptSerializer();
                        cls_UserDetail userDetails = (cls_UserDetail)JsonSerializer.Deserialize(resultData, typeof(cls_UserDetail));
                        if (userDetails != null)
                        {
                            if (userDetails.status == "A")
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                errorString = ex.Message;
                return false;
            }
            finally
            {
                //this.Cursor = System.Windows.Forms.Cursors.Default;
            }
        }


        /// <summary>
        /// Get User Details from Configuration
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <param name="deviceName"></param>
        /// <param name="authURL"></param>

        private void GetUserDetailsFromConfig(ref string userName, ref string password, ref string deviceName, ref string authURL)
        {
            try
            {
                string xmlPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "cfg/common.cfg");
                XmlDocument configDoc = new XmlDocument();
                configDoc.Load(xmlPath);
                // Check if the Password is stored in Config
                XmlNode spwdNode = configDoc.SelectSingleNode(@"/configurations/spwd");
                if (spwdNode != null)
                {
                    // Get Username from Configuration File
                    XmlNode userNode = configDoc.SelectSingleNode(@"/configurations/uname");
                    if (userNode != null)
                    {
                        userName = userNode.InnerText;
                        userName =  ucls_EnDcryption.DecryptString(EncKey.encdeKey, userName);
                    }

                    // Get Password from Configuration File
                    XmlNode pwdNode = configDoc.SelectSingleNode(@"/configurations/pwd");
                    if (pwdNode != null)
                    {
                        password = pwdNode.InnerText;
                        password = ucls_EnDcryption.DecryptString(EncKey.encdeKey, password);
                    }

                    // Get Device Name
                    XmlNode deviceNode = configDoc.SelectSingleNode(@"/configurations/deviceName");
                    if (deviceNode != null)
                    {
                        deviceName = deviceNode.InnerText;
                    }


                    // Get Auth URL
                    XmlNode authURLNode = configDoc.SelectSingleNode(@"/configurations/authURL");
                    if (authURLNode != null)
                    {
                        authURL = authURLNode.InnerText;
                    }
                }
                
            }
            catch(Exception ex)
            {
                WriteToLog("Reading the user details failed: " + ex.Message + ". Check that the username, password, device name and server url are set", false);
            }
        }

        protected override void OnStop()
        {
            WriteToLog("Auth service stopped", true);
        }


        /// <summary>Detail written to the file by a Debug build only, never to the event log.</summary>
        private void WriteDebug(string logString)
        {
            fileLogger?.Debug(logString);
        }

        /// <summary>
        /// Writelog in File and Event Log based on the configuration
        /// </summary>
        /// <param name="logString"></param>
        /// <param name="bInfo"></param>
        public void WriteToLog(string logString,bool bInfo)
        {
            bool writeEventLog = Convert.ToBoolean(ConfigurationManager.AppSettings["eventlog"].ToString());

            if (writeEventLog)
            {

                EventLog objEventLog = new EventLog();
                objEventLog.Source = "Plexus_Auth_Service";
                objEventLog.WriteEntry(logString, bInfo ? EventLogEntryType.Information : EventLogEntryType.Error);
            }
            if (bInfo)
            {
                fileLogger.Information(logString);
            }
            else
            {
                fileLogger.Error(logString);
            }
        }
    }
}
