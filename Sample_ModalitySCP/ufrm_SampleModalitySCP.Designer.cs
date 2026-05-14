
namespace Sample_ModalitySCP
{
    partial class ufrm_SampleModalitySCP
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ufrm_SampleModalitySCP));
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.mcb_Backend = new MaterialSkin.Controls.MaterialComboBox();
            this.materialLabel1 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel4 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel5 = new MaterialSkin.Controls.MaterialLabel();
            this.mtxtb_ModalityPort = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxtb_ModalityHost = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxtb_ModalityAETitle = new MaterialSkin.Controls.MaterialTextBox();
            this.materialLabel6 = new MaterialSkin.Controls.MaterialLabel();
            this.mtb_StartAndListen = new MaterialSkin.Controls.MaterialButton();
            this.mtb_Stop = new MaterialSkin.Controls.MaterialButton();
            this.mtb_testDBConnection = new MaterialSkin.Controls.MaterialButton();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.mcb_Backend);
            this.groupBox2.Controls.Add(this.materialLabel1);
            this.groupBox2.Controls.Add(this.materialLabel4);
            this.groupBox2.Controls.Add(this.materialLabel5);
            this.groupBox2.Controls.Add(this.mtxtb_ModalityPort);
            this.groupBox2.Controls.Add(this.mtxtb_ModalityHost);
            this.groupBox2.Controls.Add(this.mtxtb_ModalityAETitle);
            this.groupBox2.Controls.Add(this.materialLabel6);
            this.groupBox2.Font = new System.Drawing.Font("Microsoft Tai Le", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(12, 12);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(697, 354);
            this.groupBox2.TabIndex = 9;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Modality SCP Settings";
            // 
            // mcb_Backend
            // 
            this.mcb_Backend.AutoResize = false;
            this.mcb_Backend.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.mcb_Backend.Depth = 0;
            this.mcb_Backend.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawVariable;
            this.mcb_Backend.DropDownHeight = 174;
            this.mcb_Backend.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.mcb_Backend.DropDownWidth = 121;
            this.mcb_Backend.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            this.mcb_Backend.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(222)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.mcb_Backend.FormattingEnabled = true;
            this.mcb_Backend.IntegralHeight = false;
            this.mcb_Backend.ItemHeight = 43;
            this.mcb_Backend.Items.AddRange(new object[] {
            "List",
            "Plexus Database",
            "Pellucid Database"});
            this.mcb_Backend.Location = new System.Drawing.Point(222, 278);
            this.mcb_Backend.MaxDropDownItems = 4;
            this.mcb_Backend.MouseState = MaterialSkin.MouseState.OUT;
            this.mcb_Backend.Name = "mcb_Backend";
            this.mcb_Backend.Size = new System.Drawing.Size(446, 49);
            this.mcb_Backend.StartIndex = 0;
            this.mcb_Backend.TabIndex = 13;
            this.mcb_Backend.SelectedIndexChanged += new System.EventHandler(this.mcb_Backend_SelectedIndexChanged);
            // 
            // materialLabel1
            // 
            this.materialLabel1.AutoSize = true;
            this.materialLabel1.Depth = 0;
            this.materialLabel1.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel1.Location = new System.Drawing.Point(62, 292);
            this.materialLabel1.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel1.Name = "materialLabel1";
            this.materialLabel1.Size = new System.Drawing.Size(62, 19);
            this.materialLabel1.TabIndex = 12;
            this.materialLabel1.Text = "Backend";
            // 
            // materialLabel4
            // 
            this.materialLabel4.AutoSize = true;
            this.materialLabel4.Depth = 0;
            this.materialLabel4.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel4.Location = new System.Drawing.Point(57, 211);
            this.materialLabel4.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel4.Name = "materialLabel4";
            this.materialLabel4.Size = new System.Drawing.Size(90, 19);
            this.materialLabel4.TabIndex = 11;
            this.materialLabel4.Text = "Port Number";
            // 
            // materialLabel5
            // 
            this.materialLabel5.AutoSize = true;
            this.materialLabel5.Depth = 0;
            this.materialLabel5.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel5.Location = new System.Drawing.Point(57, 140);
            this.materialLabel5.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel5.Name = "materialLabel5";
            this.materialLabel5.Size = new System.Drawing.Size(95, 19);
            this.materialLabel5.TabIndex = 10;
            this.materialLabel5.Text = "Host Address";
            // 
            // mtxtb_ModalityPort
            // 
            this.mtxtb_ModalityPort.AnimateReadOnly = false;
            this.mtxtb_ModalityPort.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxtb_ModalityPort.Depth = 0;
            this.mtxtb_ModalityPort.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxtb_ModalityPort.LeadingIcon = null;
            this.mtxtb_ModalityPort.Location = new System.Drawing.Point(222, 192);
            this.mtxtb_ModalityPort.MaxLength = 50;
            this.mtxtb_ModalityPort.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_ModalityPort.Multiline = false;
            this.mtxtb_ModalityPort.Name = "mtxtb_ModalityPort";
            this.mtxtb_ModalityPort.Size = new System.Drawing.Size(95, 50);
            this.mtxtb_ModalityPort.TabIndex = 9;
            this.mtxtb_ModalityPort.Text = "2025";
            this.mtxtb_ModalityPort.TrailingIcon = null;
            // 
            // mtxtb_ModalityHost
            // 
            this.mtxtb_ModalityHost.AnimateReadOnly = false;
            this.mtxtb_ModalityHost.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxtb_ModalityHost.Depth = 0;
            this.mtxtb_ModalityHost.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxtb_ModalityHost.LeadingIcon = null;
            this.mtxtb_ModalityHost.Location = new System.Drawing.Point(222, 122);
            this.mtxtb_ModalityHost.MaxLength = 50;
            this.mtxtb_ModalityHost.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_ModalityHost.Multiline = false;
            this.mtxtb_ModalityHost.Name = "mtxtb_ModalityHost";
            this.mtxtb_ModalityHost.Size = new System.Drawing.Size(446, 50);
            this.mtxtb_ModalityHost.TabIndex = 8;
            this.mtxtb_ModalityHost.Text = "127.0.0.1";
            this.mtxtb_ModalityHost.TrailingIcon = null;
            // 
            // mtxtb_ModalityAETitle
            // 
            this.mtxtb_ModalityAETitle.AnimateReadOnly = false;
            this.mtxtb_ModalityAETitle.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxtb_ModalityAETitle.Depth = 0;
            this.mtxtb_ModalityAETitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxtb_ModalityAETitle.LeadingIcon = null;
            this.mtxtb_ModalityAETitle.Location = new System.Drawing.Point(222, 53);
            this.mtxtb_ModalityAETitle.MaxLength = 50;
            this.mtxtb_ModalityAETitle.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_ModalityAETitle.Multiline = false;
            this.mtxtb_ModalityAETitle.Name = "mtxtb_ModalityAETitle";
            this.mtxtb_ModalityAETitle.Size = new System.Drawing.Size(446, 50);
            this.mtxtb_ModalityAETitle.TabIndex = 7;
            this.mtxtb_ModalityAETitle.Text = "MODALITYSCP";
            this.mtxtb_ModalityAETitle.TrailingIcon = null;
            // 
            // materialLabel6
            // 
            this.materialLabel6.AutoSize = true;
            this.materialLabel6.Depth = 0;
            this.materialLabel6.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel6.Location = new System.Drawing.Point(57, 67);
            this.materialLabel6.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel6.Name = "materialLabel6";
            this.materialLabel6.Size = new System.Drawing.Size(55, 19);
            this.materialLabel6.TabIndex = 6;
            this.materialLabel6.Text = "AE Title";
            // 
            // mtb_StartAndListen
            // 
            this.mtb_StartAndListen.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mtb_StartAndListen.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mtb_StartAndListen.Depth = 0;
            this.mtb_StartAndListen.HighEmphasis = true;
            this.mtb_StartAndListen.Icon = null;
            this.mtb_StartAndListen.Location = new System.Drawing.Point(516, 398);
            this.mtb_StartAndListen.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mtb_StartAndListen.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtb_StartAndListen.Name = "mtb_StartAndListen";
            this.mtb_StartAndListen.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mtb_StartAndListen.Size = new System.Drawing.Size(67, 36);
            this.mtb_StartAndListen.TabIndex = 10;
            this.mtb_StartAndListen.Text = "Start";
            this.mtb_StartAndListen.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mtb_StartAndListen.UseAccentColor = false;
            this.mtb_StartAndListen.UseVisualStyleBackColor = true;
            this.mtb_StartAndListen.Click += new System.EventHandler(this.mtb_StartAndListen_Click);
            // 
            // mtb_Stop
            // 
            this.mtb_Stop.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mtb_Stop.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mtb_Stop.Depth = 0;
            this.mtb_Stop.HighEmphasis = true;
            this.mtb_Stop.Icon = null;
            this.mtb_Stop.Location = new System.Drawing.Point(616, 398);
            this.mtb_Stop.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mtb_Stop.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtb_Stop.Name = "mtb_Stop";
            this.mtb_Stop.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mtb_Stop.Size = new System.Drawing.Size(64, 36);
            this.mtb_Stop.TabIndex = 11;
            this.mtb_Stop.Text = "Stop";
            this.mtb_Stop.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mtb_Stop.UseAccentColor = false;
            this.mtb_Stop.UseVisualStyleBackColor = true;
            this.mtb_Stop.Click += new System.EventHandler(this.mtb_Stop_Click);
            // 
            // mtb_testDBConnection
            // 
            this.mtb_testDBConnection.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mtb_testDBConnection.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mtb_testDBConnection.Depth = 0;
            this.mtb_testDBConnection.HighEmphasis = true;
            this.mtb_testDBConnection.Icon = null;
            this.mtb_testDBConnection.Location = new System.Drawing.Point(13, 398);
            this.mtb_testDBConnection.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mtb_testDBConnection.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtb_testDBConnection.Name = "mtb_testDBConnection";
            this.mtb_testDBConnection.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mtb_testDBConnection.Size = new System.Drawing.Size(175, 36);
            this.mtb_testDBConnection.TabIndex = 12;
            this.mtb_testDBConnection.Text = "Test DB Connection";
            this.mtb_testDBConnection.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mtb_testDBConnection.UseAccentColor = false;
            this.mtb_testDBConnection.UseVisualStyleBackColor = true;
            this.mtb_testDBConnection.Click += new System.EventHandler(this.materialButton1_Click);
            // 
            // ufrm_SampleModalitySCP
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(721, 460);
            this.Controls.Add(this.mtb_testDBConnection);
            this.Controls.Add(this.mtb_Stop);
            this.Controls.Add(this.mtb_StartAndListen);
            this.Controls.Add(this.groupBox2);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "ufrm_SampleModalitySCP";
            this.Text = "Sample Modality SCP Application";
            this.Load += new System.EventHandler(this.ufrm_SampleModalitySCP_Load);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox2;
        private MaterialSkin.Controls.MaterialLabel materialLabel4;
        private MaterialSkin.Controls.MaterialLabel materialLabel5;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_ModalityPort;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_ModalityHost;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_ModalityAETitle;
        private MaterialSkin.Controls.MaterialLabel materialLabel6;
        private MaterialSkin.Controls.MaterialLabel materialLabel1;
        private MaterialSkin.Controls.MaterialComboBox mcb_Backend;
        private MaterialSkin.Controls.MaterialButton mtb_StartAndListen;
        private MaterialSkin.Controls.MaterialButton mtb_Stop;
        private MaterialSkin.Controls.MaterialButton mtb_testDBConnection;
    }
}

