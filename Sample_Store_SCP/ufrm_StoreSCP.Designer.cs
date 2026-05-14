
namespace Sample_Store_SCP
{
    partial class ufrm_StoreSCP
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ufrm_StoreSCP));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.materialLabel4 = new MaterialSkin.Controls.MaterialLabel();
            this.mtxtb_FolderPath = new MaterialSkin.Controls.MaterialTextBox();
            this.materialLabel3 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel2 = new MaterialSkin.Controls.MaterialLabel();
            this.mtxtb_StorePort = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxtb_StoreHost = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxtb_StoreAETitle = new MaterialSkin.Controls.MaterialTextBox();
            this.materialLabel1 = new MaterialSkin.Controls.MaterialLabel();
            this.mtb_Stop = new MaterialSkin.Controls.MaterialButton();
            this.mtb_StartAndListen = new MaterialSkin.Controls.MaterialButton();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.materialLabel9 = new MaterialSkin.Controls.MaterialLabel();
            this.mtxtb_PushFolder = new MaterialSkin.Controls.MaterialTextBox();
            this.mtb_CallingAETitle = new MaterialSkin.Controls.MaterialTextBox();
            this.materialLabel6 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel5 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel7 = new MaterialSkin.Controls.MaterialLabel();
            this.mtxt_Port1 = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxt_HostAddress1 = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxt_AETitle1 = new MaterialSkin.Controls.MaterialTextBox();
            this.materialLabel8 = new MaterialSkin.Controls.MaterialLabel();
            this.mtbtn_Push = new MaterialSkin.Controls.MaterialButton();
            this.mtpg_ProgressUpload = new MaterialSkin.Controls.MaterialProgressBar();
            this.pgbar_upload = new System.Windows.Forms.ProgressBar();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.materialLabel4);
            this.groupBox1.Controls.Add(this.mtxtb_FolderPath);
            this.groupBox1.Controls.Add(this.materialLabel3);
            this.groupBox1.Controls.Add(this.materialLabel2);
            this.groupBox1.Controls.Add(this.mtxtb_StorePort);
            this.groupBox1.Controls.Add(this.mtxtb_StoreHost);
            this.groupBox1.Controls.Add(this.mtxtb_StoreAETitle);
            this.groupBox1.Controls.Add(this.materialLabel1);
            this.groupBox1.Font = new System.Drawing.Font("Microsoft Tai Le", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox1.Location = new System.Drawing.Point(12, 22);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(652, 383);
            this.groupBox1.TabIndex = 8;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Store SCP Settings";
            // 
            // materialLabel4
            // 
            this.materialLabel4.AutoSize = true;
            this.materialLabel4.Depth = 0;
            this.materialLabel4.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel4.Location = new System.Drawing.Point(76, 311);
            this.materialLabel4.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel4.Name = "materialLabel4";
            this.materialLabel4.Size = new System.Drawing.Size(82, 19);
            this.materialLabel4.TabIndex = 13;
            this.materialLabel4.Text = "Folder Path";
            // 
            // mtxtb_FolderPath
            // 
            this.mtxtb_FolderPath.AnimateReadOnly = false;
            this.mtxtb_FolderPath.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxtb_FolderPath.Depth = 0;
            this.mtxtb_FolderPath.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxtb_FolderPath.LeadingIcon = null;
            this.mtxtb_FolderPath.Location = new System.Drawing.Point(241, 292);
            this.mtxtb_FolderPath.MaxLength = 50;
            this.mtxtb_FolderPath.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_FolderPath.Multiline = false;
            this.mtxtb_FolderPath.Name = "mtxtb_FolderPath";
            this.mtxtb_FolderPath.Size = new System.Drawing.Size(352, 50);
            this.mtxtb_FolderPath.TabIndex = 12;
            this.mtxtb_FolderPath.Text = "C:\\\\Temp\\\\Test";
            this.mtxtb_FolderPath.TrailingIcon = null;
            // 
            // materialLabel3
            // 
            this.materialLabel3.AutoSize = true;
            this.materialLabel3.Depth = 0;
            this.materialLabel3.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel3.Location = new System.Drawing.Point(71, 230);
            this.materialLabel3.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel3.Name = "materialLabel3";
            this.materialLabel3.Size = new System.Drawing.Size(90, 19);
            this.materialLabel3.TabIndex = 11;
            this.materialLabel3.Text = "Port Number";
            // 
            // materialLabel2
            // 
            this.materialLabel2.AutoSize = true;
            this.materialLabel2.Depth = 0;
            this.materialLabel2.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel2.Location = new System.Drawing.Point(71, 141);
            this.materialLabel2.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel2.Name = "materialLabel2";
            this.materialLabel2.Size = new System.Drawing.Size(95, 19);
            this.materialLabel2.TabIndex = 10;
            this.materialLabel2.Text = "Host Address";
            // 
            // mtxtb_StorePort
            // 
            this.mtxtb_StorePort.AnimateReadOnly = false;
            this.mtxtb_StorePort.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxtb_StorePort.Depth = 0;
            this.mtxtb_StorePort.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxtb_StorePort.LeadingIcon = null;
            this.mtxtb_StorePort.Location = new System.Drawing.Point(236, 211);
            this.mtxtb_StorePort.MaxLength = 50;
            this.mtxtb_StorePort.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_StorePort.Multiline = false;
            this.mtxtb_StorePort.Name = "mtxtb_StorePort";
            this.mtxtb_StorePort.Size = new System.Drawing.Size(95, 50);
            this.mtxtb_StorePort.TabIndex = 9;
            this.mtxtb_StorePort.Text = "2007";
            this.mtxtb_StorePort.TrailingIcon = null;
            // 
            // mtxtb_StoreHost
            // 
            this.mtxtb_StoreHost.AnimateReadOnly = false;
            this.mtxtb_StoreHost.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxtb_StoreHost.Depth = 0;
            this.mtxtb_StoreHost.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxtb_StoreHost.LeadingIcon = null;
            this.mtxtb_StoreHost.Location = new System.Drawing.Point(236, 123);
            this.mtxtb_StoreHost.MaxLength = 50;
            this.mtxtb_StoreHost.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_StoreHost.Multiline = false;
            this.mtxtb_StoreHost.Name = "mtxtb_StoreHost";
            this.mtxtb_StoreHost.Size = new System.Drawing.Size(357, 50);
            this.mtxtb_StoreHost.TabIndex = 8;
            this.mtxtb_StoreHost.Text = "127.0.0.1";
            this.mtxtb_StoreHost.TrailingIcon = null;
            // 
            // mtxtb_StoreAETitle
            // 
            this.mtxtb_StoreAETitle.AnimateReadOnly = false;
            this.mtxtb_StoreAETitle.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxtb_StoreAETitle.Depth = 0;
            this.mtxtb_StoreAETitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxtb_StoreAETitle.LeadingIcon = null;
            this.mtxtb_StoreAETitle.Location = new System.Drawing.Point(236, 47);
            this.mtxtb_StoreAETitle.MaxLength = 50;
            this.mtxtb_StoreAETitle.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_StoreAETitle.Multiline = false;
            this.mtxtb_StoreAETitle.Name = "mtxtb_StoreAETitle";
            this.mtxtb_StoreAETitle.Size = new System.Drawing.Size(357, 50);
            this.mtxtb_StoreAETitle.TabIndex = 7;
            this.mtxtb_StoreAETitle.Text = "STORESERVER";
            this.mtxtb_StoreAETitle.TrailingIcon = null;
            // 
            // materialLabel1
            // 
            this.materialLabel1.AutoSize = true;
            this.materialLabel1.Depth = 0;
            this.materialLabel1.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel1.Location = new System.Drawing.Point(71, 61);
            this.materialLabel1.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel1.Name = "materialLabel1";
            this.materialLabel1.Size = new System.Drawing.Size(55, 19);
            this.materialLabel1.TabIndex = 6;
            this.materialLabel1.Text = "AE Title";
            // 
            // mtb_Stop
            // 
            this.mtb_Stop.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mtb_Stop.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mtb_Stop.Depth = 0;
            this.mtb_Stop.Enabled = false;
            this.mtb_Stop.HighEmphasis = true;
            this.mtb_Stop.Icon = null;
            this.mtb_Stop.Location = new System.Drawing.Point(556, 427);
            this.mtb_Stop.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mtb_Stop.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtb_Stop.Name = "mtb_Stop";
            this.mtb_Stop.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mtb_Stop.Size = new System.Drawing.Size(64, 36);
            this.mtb_Stop.TabIndex = 13;
            this.mtb_Stop.Text = "Stop";
            this.mtb_Stop.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mtb_Stop.UseAccentColor = false;
            this.mtb_Stop.UseVisualStyleBackColor = true;
            this.mtb_Stop.Click += new System.EventHandler(this.mtb_Stop_Click);
            // 
            // mtb_StartAndListen
            // 
            this.mtb_StartAndListen.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mtb_StartAndListen.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mtb_StartAndListen.Depth = 0;
            this.mtb_StartAndListen.HighEmphasis = true;
            this.mtb_StartAndListen.Icon = null;
            this.mtb_StartAndListen.Location = new System.Drawing.Point(470, 427);
            this.mtb_StartAndListen.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mtb_StartAndListen.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtb_StartAndListen.Name = "mtb_StartAndListen";
            this.mtb_StartAndListen.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mtb_StartAndListen.Size = new System.Drawing.Size(67, 36);
            this.mtb_StartAndListen.TabIndex = 12;
            this.mtb_StartAndListen.Text = "Start";
            this.mtb_StartAndListen.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mtb_StartAndListen.UseAccentColor = false;
            this.mtb_StartAndListen.UseVisualStyleBackColor = true;
            this.mtb_StartAndListen.Click += new System.EventHandler(this.mtb_StartAndListen_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.materialLabel9);
            this.groupBox2.Controls.Add(this.mtxtb_PushFolder);
            this.groupBox2.Controls.Add(this.mtb_CallingAETitle);
            this.groupBox2.Controls.Add(this.materialLabel6);
            this.groupBox2.Controls.Add(this.materialLabel5);
            this.groupBox2.Controls.Add(this.materialLabel7);
            this.groupBox2.Controls.Add(this.mtxt_Port1);
            this.groupBox2.Controls.Add(this.mtxt_HostAddress1);
            this.groupBox2.Controls.Add(this.mtxt_AETitle1);
            this.groupBox2.Controls.Add(this.materialLabel8);
            this.groupBox2.Font = new System.Drawing.Font("Microsoft Tai Le", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(686, 22);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(652, 453);
            this.groupBox2.TabIndex = 14;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Store SCU Settings";
            // 
            // materialLabel9
            // 
            this.materialLabel9.AutoSize = true;
            this.materialLabel9.Depth = 0;
            this.materialLabel9.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel9.Location = new System.Drawing.Point(71, 392);
            this.materialLabel9.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel9.Name = "materialLabel9";
            this.materialLabel9.Size = new System.Drawing.Size(82, 19);
            this.materialLabel9.TabIndex = 15;
            this.materialLabel9.Text = "Folder Path";
            // 
            // mtxtb_PushFolder
            // 
            this.mtxtb_PushFolder.AnimateReadOnly = false;
            this.mtxtb_PushFolder.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxtb_PushFolder.Depth = 0;
            this.mtxtb_PushFolder.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxtb_PushFolder.LeadingIcon = null;
            this.mtxtb_PushFolder.Location = new System.Drawing.Point(236, 373);
            this.mtxtb_PushFolder.MaxLength = 50;
            this.mtxtb_PushFolder.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_PushFolder.Multiline = false;
            this.mtxtb_PushFolder.Name = "mtxtb_PushFolder";
            this.mtxtb_PushFolder.Size = new System.Drawing.Size(352, 50);
            this.mtxtb_PushFolder.TabIndex = 14;
            this.mtxtb_PushFolder.Text = "C:\\\\Temp\\\\TestP";
            this.mtxtb_PushFolder.TrailingIcon = null;
            // 
            // mtb_CallingAETitle
            // 
            this.mtb_CallingAETitle.AnimateReadOnly = false;
            this.mtb_CallingAETitle.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtb_CallingAETitle.Depth = 0;
            this.mtb_CallingAETitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtb_CallingAETitle.LeadingIcon = null;
            this.mtb_CallingAETitle.Location = new System.Drawing.Point(236, 297);
            this.mtb_CallingAETitle.MaxLength = 50;
            this.mtb_CallingAETitle.MouseState = MaterialSkin.MouseState.OUT;
            this.mtb_CallingAETitle.Multiline = false;
            this.mtb_CallingAETitle.Name = "mtb_CallingAETitle";
            this.mtb_CallingAETitle.Size = new System.Drawing.Size(357, 50);
            this.mtb_CallingAETitle.TabIndex = 15;
            this.mtb_CallingAETitle.Text = "STORESCU";
            this.mtb_CallingAETitle.TrailingIcon = null;
            // 
            // materialLabel6
            // 
            this.materialLabel6.AutoSize = true;
            this.materialLabel6.Depth = 0;
            this.materialLabel6.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel6.Location = new System.Drawing.Point(71, 230);
            this.materialLabel6.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel6.Name = "materialLabel6";
            this.materialLabel6.Size = new System.Drawing.Size(90, 19);
            this.materialLabel6.TabIndex = 11;
            this.materialLabel6.Text = "Port Number";
            // 
            // materialLabel5
            // 
            this.materialLabel5.AutoSize = true;
            this.materialLabel5.Depth = 0;
            this.materialLabel5.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel5.Location = new System.Drawing.Point(71, 311);
            this.materialLabel5.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel5.Name = "materialLabel5";
            this.materialLabel5.Size = new System.Drawing.Size(108, 19);
            this.materialLabel5.TabIndex = 14;
            this.materialLabel5.Text = "Calling AE Title";
            // 
            // materialLabel7
            // 
            this.materialLabel7.AutoSize = true;
            this.materialLabel7.Depth = 0;
            this.materialLabel7.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel7.Location = new System.Drawing.Point(71, 141);
            this.materialLabel7.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel7.Name = "materialLabel7";
            this.materialLabel7.Size = new System.Drawing.Size(95, 19);
            this.materialLabel7.TabIndex = 10;
            this.materialLabel7.Text = "Host Address";
            // 
            // mtxt_Port1
            // 
            this.mtxt_Port1.AnimateReadOnly = false;
            this.mtxt_Port1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxt_Port1.Depth = 0;
            this.mtxt_Port1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxt_Port1.LeadingIcon = null;
            this.mtxt_Port1.Location = new System.Drawing.Point(236, 211);
            this.mtxt_Port1.MaxLength = 50;
            this.mtxt_Port1.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxt_Port1.Multiline = false;
            this.mtxt_Port1.Name = "mtxt_Port1";
            this.mtxt_Port1.Size = new System.Drawing.Size(95, 50);
            this.mtxt_Port1.TabIndex = 9;
            this.mtxt_Port1.Text = "2007";
            this.mtxt_Port1.TrailingIcon = null;
            // 
            // mtxt_HostAddress1
            // 
            this.mtxt_HostAddress1.AnimateReadOnly = false;
            this.mtxt_HostAddress1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxt_HostAddress1.Depth = 0;
            this.mtxt_HostAddress1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxt_HostAddress1.LeadingIcon = null;
            this.mtxt_HostAddress1.Location = new System.Drawing.Point(236, 123);
            this.mtxt_HostAddress1.MaxLength = 50;
            this.mtxt_HostAddress1.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxt_HostAddress1.Multiline = false;
            this.mtxt_HostAddress1.Name = "mtxt_HostAddress1";
            this.mtxt_HostAddress1.Size = new System.Drawing.Size(357, 50);
            this.mtxt_HostAddress1.TabIndex = 8;
            this.mtxt_HostAddress1.Text = "127.0.0.1";
            this.mtxt_HostAddress1.TrailingIcon = null;
            // 
            // mtxt_AETitle1
            // 
            this.mtxt_AETitle1.AnimateReadOnly = false;
            this.mtxt_AETitle1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxt_AETitle1.Depth = 0;
            this.mtxt_AETitle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxt_AETitle1.LeadingIcon = null;
            this.mtxt_AETitle1.Location = new System.Drawing.Point(236, 47);
            this.mtxt_AETitle1.MaxLength = 50;
            this.mtxt_AETitle1.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxt_AETitle1.Multiline = false;
            this.mtxt_AETitle1.Name = "mtxt_AETitle1";
            this.mtxt_AETitle1.Size = new System.Drawing.Size(357, 50);
            this.mtxt_AETitle1.TabIndex = 7;
            this.mtxt_AETitle1.Text = "STORESERVER";
            this.mtxt_AETitle1.TrailingIcon = null;
            // 
            // materialLabel8
            // 
            this.materialLabel8.AutoSize = true;
            this.materialLabel8.Depth = 0;
            this.materialLabel8.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel8.Location = new System.Drawing.Point(71, 61);
            this.materialLabel8.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel8.Name = "materialLabel8";
            this.materialLabel8.Size = new System.Drawing.Size(55, 19);
            this.materialLabel8.TabIndex = 6;
            this.materialLabel8.Text = "AE Title";
            // 
            // mtbtn_Push
            // 
            this.mtbtn_Push.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mtbtn_Push.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mtbtn_Push.Depth = 0;
            this.mtbtn_Push.HighEmphasis = true;
            this.mtbtn_Push.Icon = null;
            this.mtbtn_Push.Location = new System.Drawing.Point(1218, 498);
            this.mtbtn_Push.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mtbtn_Push.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtbtn_Push.Name = "mtbtn_Push";
            this.mtbtn_Push.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mtbtn_Push.Size = new System.Drawing.Size(64, 36);
            this.mtbtn_Push.TabIndex = 15;
            this.mtbtn_Push.Text = "Push";
            this.mtbtn_Push.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mtbtn_Push.UseAccentColor = false;
            this.mtbtn_Push.UseVisualStyleBackColor = true;
            this.mtbtn_Push.Click += new System.EventHandler(this.mtbtn_Push_Click);
            // 
            // mtpg_ProgressUpload
            // 
            this.mtpg_ProgressUpload.Depth = 0;
            this.mtpg_ProgressUpload.Location = new System.Drawing.Point(34, 543);
            this.mtpg_ProgressUpload.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtpg_ProgressUpload.Name = "mtpg_ProgressUpload";
            this.mtpg_ProgressUpload.Size = new System.Drawing.Size(1304, 5);
            this.mtpg_ProgressUpload.Step = 1;
            this.mtpg_ProgressUpload.TabIndex = 16;
            this.mtpg_ProgressUpload.Visible = false;
            // 
            // pgbar_upload
            // 
            this.pgbar_upload.Location = new System.Drawing.Point(34, 565);
            this.pgbar_upload.Name = "pgbar_upload";
            this.pgbar_upload.Size = new System.Drawing.Size(1304, 10);
            this.pgbar_upload.TabIndex = 17;
            this.pgbar_upload.Visible = false;
            // 
            // ufrm_StoreSCP
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1359, 624);
            this.Controls.Add(this.pgbar_upload);
            this.Controls.Add(this.mtpg_ProgressUpload);
            this.Controls.Add(this.mtbtn_Push);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.mtb_Stop);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.mtb_StartAndListen);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "ufrm_StoreSCP";
            this.Text = "Storage SCP and Store SCU";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ufrm_StoreSCP_FormClosing);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private MaterialSkin.Controls.MaterialLabel materialLabel3;
        private MaterialSkin.Controls.MaterialLabel materialLabel2;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_StorePort;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_StoreHost;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_StoreAETitle;
        private MaterialSkin.Controls.MaterialLabel materialLabel1;
        private MaterialSkin.Controls.MaterialButton mtb_Stop;
        private MaterialSkin.Controls.MaterialButton mtb_StartAndListen;
        private MaterialSkin.Controls.MaterialLabel materialLabel4;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_FolderPath;
        private System.Windows.Forms.GroupBox groupBox2;
        private MaterialSkin.Controls.MaterialLabel materialLabel6;
        private MaterialSkin.Controls.MaterialLabel materialLabel7;
        private MaterialSkin.Controls.MaterialTextBox mtxt_Port1;
        private MaterialSkin.Controls.MaterialTextBox mtxt_HostAddress1;
        private MaterialSkin.Controls.MaterialTextBox mtxt_AETitle1;
        private MaterialSkin.Controls.MaterialLabel materialLabel8;
        private MaterialSkin.Controls.MaterialButton mtbtn_Push;
        private MaterialSkin.Controls.MaterialTextBox mtb_CallingAETitle;
        private MaterialSkin.Controls.MaterialLabel materialLabel5;
        private MaterialSkin.Controls.MaterialLabel materialLabel9;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_PushFolder;
        private MaterialSkin.Controls.MaterialProgressBar mtpg_ProgressUpload;
        private System.Windows.Forms.ProgressBar pgbar_upload;
    }
}

