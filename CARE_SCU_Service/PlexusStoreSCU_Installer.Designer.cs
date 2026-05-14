
namespace Plexus_SCU_Service
{
    partial class CAREStoreSCU_Installer
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
            this.careSCUProcInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.careSCUInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // careSCUProcInstaller
            // 
            this.careSCUProcInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.careSCUProcInstaller.Password = null;
            this.careSCUProcInstaller.Username = null;
            // 
            // careSCUInstaller
            // 
            this.careSCUInstaller.Description = "CARE StoreSCU Service";
            this.careSCUInstaller.DisplayName = "CARE StoreSCU Service";
            this.careSCUInstaller.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.careSCUProcInstaller});
            this.careSCUInstaller.ServiceName = "CARE StoreSCU Service";
            this.careSCUInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            // 
            // CAREStoreSCU_Installer
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.careSCUInstaller});

        }

        #endregion

        private System.ServiceProcess.ServiceProcessInstaller careSCUProcInstaller;
        private System.ServiceProcess.ServiceInstaller careSCUInstaller;
    }
}