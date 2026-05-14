
using Plexus_DICOM_Enabler.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Plexus_DICOM_Enabler.UserControls
{
    public partial class uctrl_ServerManager : UserControl
    {
        public uctrl_ServerManager()
        {
            InitializeComponent();
        }

        public void EnableDisableButtons()
        {
            try
            {
                if (Global.deployType == 1) 
                {
                    // Enable and Disable Install Uninstall Buttons
                    //if ( CheckIfServiceInstalled("Care_Store_SCP_Service") &&
                    //    CheckIfServiceInstalled("Care MWL SCP Service") && CheckIfServiceInstalled("Care StoreSCU Service") ) {
                    if (CheckIfServiceInstalled("Care Store SCP Service") &&
                        CheckIfServiceInstalled("Care MWL SCP Service") && CheckIfServiceInstalled("Care Store SCU Service"))
                    {
                        mtb_InstallService.Enabled = false;
                        mtb_UninstallService.Enabled = true;
                        if (CheckIfServiceStopped("Care Store SCP Service") &&
          CheckIfServiceStopped("Care MWL SCP Service") && CheckIfServiceStopped("Care Store SCU Service"))
                        {
                            mtb_StartService.Enabled = false;
                            mtb_StopService.Enabled = true;
                        }
                        else
                        {
                            mtb_StartService.Enabled = true;
                            mtb_StopService.Enabled = false;
                        }
                    }
                    else
                    {
                        mtb_InstallService.Enabled = true;
                        mtb_UninstallService.Enabled = false;
                        mtb_StartService.Enabled = false;
                        mtb_StopService.Enabled = false;
                    }
                }
                else if (Global.deployType == 2)
                {
                    if (CheckIfServiceInstalled("Care_Store_SCP_Service"))
                    {
                        mtb_InstallService.Enabled = false;
                        mtb_UninstallService.Enabled = true;
                        if (CheckIfServiceStopped("Care Store SCP Service"))
                        {
                            mtb_StartService.Enabled = false;
                            mtb_StopService.Enabled = true;
                        }
                        else
                        {
                            mtb_StartService.Enabled = true;
                            mtb_StopService.Enabled = false;
                        }
                    }
                    else
                    {
                        mtb_InstallService.Enabled = true;
                        mtb_UninstallService.Enabled = false;
                        mtb_StartService.Enabled = false;
                        mtb_StopService.Enabled = false;
                    }

                }
                else if (Global.deployType == 3)
                {
                    if (CheckIfServiceInstalled("Care_Store_SCP_Service") && CheckIfServiceInstalled("Care StoreSCU Service"))
                    {
                        mtb_InstallService.Enabled = false;
                        mtb_UninstallService.Enabled = true;
                        if (CheckIfServiceStopped("Care Store SCP Service") && CheckIfServiceStopped("Care StoreSCU Service"))
                        {
                            mtb_StartService.Enabled = false;
                            mtb_StopService.Enabled = true;
                        }
                        else
                        {
                            mtb_StartService.Enabled = true;
                            mtb_StopService.Enabled = false;
                        }
                    }
                    else
                    {
                        mtb_InstallService.Enabled = true;
                        mtb_UninstallService.Enabled = false;
                        mtb_StartService.Enabled = false;
                        mtb_StopService.Enabled = false;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error Enabling and Disabling Buttons " + ex.Message);
            }
        }

        private bool CheckIfServiceInstalled(string serviceName)
        {
            return ServiceController.GetServices().Any(serviceController => serviceController.ServiceName.Equals(serviceName));
        }


        private bool CheckIfServiceStopped(string serviceName)
        {
            bool bRetVal = false;
            ServiceController service = new ServiceController(serviceName);
            if (service != null)
            {
                if (service.Status.Equals(ServiceControllerStatus.StartPending) || service.Status.Equals(ServiceControllerStatus.Running))
                {
                    bRetVal = true;
                }
            }
            return bRetVal;
        }


        private void mtb_InstallService_Click(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
            try
            {
                if (Global.deployType == 1)
                {
                    //if (!CheckIfServiceInstalled("Plexus_Auth_Service"))
                    // InstallUnInstallServicebyType("AUTH", true); 
                    if (!CheckIfServiceInstalled("Care MWL SCP Service"))
                        InstallUnInstallServicebyType("MWLSCP", true);
                    if (!CheckIfServiceInstalled("Care Store SCP Service"))
                        InstallUnInstallServicebyType("STORESCP", true);
                    if (!CheckIfServiceInstalled("Care Store SCU Service"))
                        InstallUnInstallServicebyType("STORESCU", true);
                }
                else if (Global.deployType == 2)
                {
                    //if (!CheckIfServiceInstalled("Plexus_Auth_Service"))
                    //    InstallUnInstallServicebyType("AUTH", true);
                    if (!CheckIfServiceInstalled("Care_Store_SCP_Service"))
                        InstallUnInstallServicebyType("STORESCP", true);
                }
                else if (Global.deployType == 3)
                {
                    if (!CheckIfServiceInstalled("Care_Store_SCP_Service"))
                        InstallUnInstallServicebyType("STORESCP", true);
                    if (!CheckIfServiceInstalled("Care StoreSCU Service"))
                        InstallUnInstallServicebyType("STORESCU", true);

                }
                MessageBox.Show("Service Installed Successfully !!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error Installing Service " + ex.Message);
            }
            finally
            {
                this.Cursor = System.Windows.Forms.Cursors.Default;
                EnableDisableButtons();
            }
        }

        private void InstallUnInstallServicebyType(string serviceType, bool bInstall)
        {
            string installExe = "InstallUtil";
            string paramters = string.Empty;
            if (bInstall)
            {
                paramters = "-i " + Global._applicationPath;
            }
            else
            {
                paramters = " -u " + Global._applicationPath;
            }
            switch (serviceType)
            {
                case "AUTH":
                    paramters = Path.Combine(paramters,"Plexus_Auth_Service.exe");
                    break;
                case "MWLSCP":
                    installExe = @"C:\Windows\system32\sc.exe";
                    string mwlApp = Path.Combine(Global._applicationPath, "Care_MWL_Service.exe");
                    if (bInstall)
                    {
                        paramters = $" create \"Care MWL SCP Service\" binPath= \"{ mwlApp }\" start= auto";
                    }
                    else
                    {
                        paramters = " delete \"Care MWL SCP Service\"";
                    }
                    break;
                case "STORESCP":
                    installExe = @"C:\Windows\system32\sc.exe";
                    string storescp = Path.Combine(Global._applicationPath, "Care_StoreSCP_Service.exe");
                    if (bInstall)
                    {
                        paramters = $" create \"Care Store SCP Service\" binPath= \"{storescp}\" start= auto";
                    }
                    else
                    {
                        paramters = " delete \"Care Store SCP Service\"";
                    }
                    //paramters = Path.Combine(paramters, "Care_StoreSCP_Service.exe");
                    break;
                case "STORESCU":
                    installExe = @"C:\Windows\system32\sc.exe";
                    string storescu = Path.Combine(Global._applicationPath, "CARE_SCU_Service.exe");
                    if (bInstall)
                    {
                        paramters = $" create \"Care Store SCU Service\" binPath= \"{storescu}\" start= auto";
                    }
                    else
                    {
                        paramters = " delete \"Care Store SCU Service\"";
                    }

                    //paramters = Path.Combine(paramters,"Care_SCU_Service.exe");
                    break;
            }

            RunApplication(installExe,paramters);

        }

        private void RunApplication(string installExe, string paramters)
        {
            Process proc = new Process();
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.FileName = installExe;
            proc.StartInfo.WorkingDirectory = Global._applicationPath;
            proc.StartInfo.Arguments = paramters;
            proc.StartInfo.CreateNoWindow = true;
            proc.Start();
            proc.WaitForExit();
            //System.Diagnostics.Process.Start(installExe, paramters);
        }




        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mtb_StartService_Click(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
            try
            {
                if (Global.deployType == 1)
                {
                    //StartStopService("Plexus_Auth_Service", true);
                    
                    StartStopService("Care MWL SCP Service", true);
                    StartStopService("Care Store SCU Service", true);
                    StartStopService("Care Store SCP Service", true);
                }
                else if (Global.deployType == 2)
                {
                    //StartStopService("Plexus_Auth_Service", true);
                    StartStopService("Plexus_Store_SCP_Service", true);
                }
                else
                {
                    StartStopService("Plexus_Store_SCP_Service", true);
                    StartStopService("Plexus StoreSCU Service", true);
                }

                MessageBox.Show("Services Started Successfully !!!");

            }
            catch(Exception ex)
            {
                MessageBox.Show("Error Staring Services with exception : " + ex.Message);
            }
            finally
            {
                this.Cursor = System.Windows.Forms.Cursors.Default;
                EnableDisableButtons();
            }
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mtb_StopService_Click(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
            try
            {
                if (Global.deployType == 1)
                {
                    //StartStopService("Plexus_Auth_Service", false);
                    StartStopService("Care Store SCP Service", false);
                    StartStopService("Care MWL SCP Service", false);
                    StartStopService("Care Store SCU Service", false);
                }
                else
                {
                    //StartStopService("Plexus_Auth_Service", false);
                    StartStopService("Plexus_Store_SCP_Service", false);
                }
                MessageBox.Show("Services Stopped Successfully !!!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error Stopping Services with exception : " + ex.Message);
            }
            finally
            {
                this.Cursor = System.Windows.Forms.Cursors.Default;
                EnableDisableButtons();
            }

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceName"></param>
        private void StartStopService(string serviceName,bool bStart)
        {
            ServiceController service = new ServiceController(serviceName);
            if (bStart)
            {
                if ((service.Status.Equals(ServiceControllerStatus.Stopped)))
                {
                    TimeSpan timeout = TimeSpan.FromMilliseconds(5000);
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                }
            }
            else
            {
                if ((service.Status.Equals(ServiceControllerStatus.Running)))
                {
                    service.Stop();
                }
            }
        }

        private void mtb_UninstallService_Click(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
            try
            {
                if (Global.deployType == 1)
                {
                    //InstallUnInstallServicebyType("AUTH", false);
                    InstallUnInstallServicebyType("MWLSCP", false);
                    InstallUnInstallServicebyType("STORESCP", false);
                    InstallUnInstallServicebyType("STORESCU", false);
                }
                else
                {
                    //InstallUnInstallServicebyType("AUTH", false);
                    InstallUnInstallServicebyType("STORESCP", false);
                }
                MessageBox.Show("Service Uninstalled Successfully !!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error Uninstalling Service " + ex.Message);
            }
            finally
            {
                this.Cursor = System.Windows.Forms.Cursors.Default;
                EnableDisableButtons();
            }
        }
    }
}
