
namespace Plexus_DICOM_Enabler.UserControls
{
    partial class uctrl_ServerManager
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
            this.mtb_InstallService = new MaterialSkin.Controls.MaterialButton();
            this.mtb_UninstallService = new MaterialSkin.Controls.MaterialButton();
            this.mtb_StartService = new MaterialSkin.Controls.MaterialButton();
            this.mtb_StopService = new MaterialSkin.Controls.MaterialButton();
            this.SuspendLayout();
            // 
            // mtb_InstallService
            // 
            this.mtb_InstallService.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mtb_InstallService.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mtb_InstallService.Depth = 0;
            this.mtb_InstallService.HighEmphasis = true;
            this.mtb_InstallService.Icon = null;
            this.mtb_InstallService.Location = new System.Drawing.Point(199, 82);
            this.mtb_InstallService.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mtb_InstallService.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtb_InstallService.Name = "mtb_InstallService";
            this.mtb_InstallService.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mtb_InstallService.Size = new System.Drawing.Size(161, 36);
            this.mtb_InstallService.TabIndex = 0;
            this.mtb_InstallService.Text = "Install Service(s)";
            this.mtb_InstallService.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mtb_InstallService.UseAccentColor = false;
            this.mtb_InstallService.UseVisualStyleBackColor = true;
            this.mtb_InstallService.Click += new System.EventHandler(this.mtb_InstallService_Click);
            // 
            // mtb_UninstallService
            // 
            this.mtb_UninstallService.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mtb_UninstallService.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mtb_UninstallService.Depth = 0;
            this.mtb_UninstallService.HighEmphasis = true;
            this.mtb_UninstallService.Icon = null;
            this.mtb_UninstallService.Location = new System.Drawing.Point(199, 337);
            this.mtb_UninstallService.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mtb_UninstallService.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtb_UninstallService.Name = "mtb_UninstallService";
            this.mtb_UninstallService.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mtb_UninstallService.Size = new System.Drawing.Size(162, 36);
            this.mtb_UninstallService.TabIndex = 1;
            this.mtb_UninstallService.Text = "Uninstall Service";
            this.mtb_UninstallService.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mtb_UninstallService.UseAccentColor = false;
            this.mtb_UninstallService.UseVisualStyleBackColor = true;
            this.mtb_UninstallService.Click += new System.EventHandler(this.mtb_UninstallService_Click);
            // 
            // mtb_StartService
            // 
            this.mtb_StartService.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mtb_StartService.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mtb_StartService.Depth = 0;
            this.mtb_StartService.HighEmphasis = true;
            this.mtb_StartService.Icon = null;
            this.mtb_StartService.Location = new System.Drawing.Point(199, 167);
            this.mtb_StartService.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mtb_StartService.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtb_StartService.Name = "mtb_StartService";
            this.mtb_StartService.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mtb_StartService.Size = new System.Drawing.Size(148, 36);
            this.mtb_StartService.TabIndex = 2;
            this.mtb_StartService.Text = "Start Service(s)";
            this.mtb_StartService.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mtb_StartService.UseAccentColor = false;
            this.mtb_StartService.UseVisualStyleBackColor = true;
            this.mtb_StartService.Click += new System.EventHandler(this.mtb_StartService_Click);
            // 
            // mtb_StopService
            // 
            this.mtb_StopService.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mtb_StopService.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mtb_StopService.Depth = 0;
            this.mtb_StopService.HighEmphasis = true;
            this.mtb_StopService.Icon = null;
            this.mtb_StopService.Location = new System.Drawing.Point(199, 252);
            this.mtb_StopService.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mtb_StopService.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtb_StopService.Name = "mtb_StopService";
            this.mtb_StopService.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mtb_StopService.Size = new System.Drawing.Size(140, 36);
            this.mtb_StopService.TabIndex = 3;
            this.mtb_StopService.Text = "Stop Service(s)";
            this.mtb_StopService.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mtb_StopService.UseAccentColor = false;
            this.mtb_StopService.UseVisualStyleBackColor = true;
            this.mtb_StopService.Click += new System.EventHandler(this.mtb_StopService_Click);
            // 
            // uctrl_ServerManager
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.mtb_StopService);
            this.Controls.Add(this.mtb_StartService);
            this.Controls.Add(this.mtb_UninstallService);
            this.Controls.Add(this.mtb_InstallService);
            this.Name = "uctrl_ServerManager";
            this.Size = new System.Drawing.Size(640, 552);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private MaterialSkin.Controls.MaterialButton mtb_InstallService;
        private MaterialSkin.Controls.MaterialButton mtb_UninstallService;
        private MaterialSkin.Controls.MaterialButton mtb_StartService;
        private MaterialSkin.Controls.MaterialButton mtb_StopService;
    }
}
