
namespace Plexus_MWL_Service
{
    partial class ProjectInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.mwlSCPProcInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.mwlSCPInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // mwlSCPProcInstaller
            // 
            this.mwlSCPProcInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.mwlSCPProcInstaller.Password = null;
            this.mwlSCPProcInstaller.Username = null;
            // 
            // mwlSCPInstaller
            // 
            this.mwlSCPInstaller.Description = "CARE Modality SCP Service";
            this.mwlSCPInstaller.DisplayName = "CARE MWL Service";
            this.mwlSCPInstaller.ServiceName = "CARE_MWL_Service";
            this.mwlSCPInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.mwlSCPProcInstaller,
            this.mwlSCPInstaller});

        }

        #endregion

        private System.ServiceProcess.ServiceProcessInstaller mwlSCPProcInstaller;
        private System.ServiceProcess.ServiceInstaller mwlSCPInstaller;
    }
}