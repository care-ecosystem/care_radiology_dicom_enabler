
namespace Plexus_Auth_Service
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
            this.plexusauthprocInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.plexusathInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // plexusauthprocInstaller
            // 
            // Code Added by Prayash
            this.plexusauthprocInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.plexusauthprocInstaller.Password = null;
            this.plexusauthprocInstaller.Username = null;
            // 
            // plexusathInstaller
            // 
            this.plexusathInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            this.plexusathInstaller.Description = "Plexus Authentication Service";
            this.plexusathInstaller.DisplayName = "Plexus_Auth_Service";
            this.plexusathInstaller.ServiceName = "Plexus_Auth_Service";
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.plexusauthprocInstaller,
            this.plexusathInstaller});

        }

        #endregion

        private System.ServiceProcess.ServiceProcessInstaller plexusauthprocInstaller;
        private System.ServiceProcess.ServiceInstaller plexusathInstaller;
    }
}