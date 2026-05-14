
namespace Plexus_DICOM_Enabler.UserControls
{
    partial class uctrl_SCPSettings
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.materialLabel3 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel2 = new MaterialSkin.Controls.MaterialLabel();
            this.mtxtb_StorePort = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxtb_StoreHost = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxtb_StoreAETitle = new MaterialSkin.Controls.MaterialTextBox();
            this.materialLabel1 = new MaterialSkin.Controls.MaterialLabel();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.materialLabel4 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel5 = new MaterialSkin.Controls.MaterialLabel();
            this.mtxtb_ModalityPort = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxtb_ModalityHost = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxtb_ModalityAETitle = new MaterialSkin.Controls.MaterialTextBox();
            this.materialLabel6 = new MaterialSkin.Controls.MaterialLabel();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.materialLabel3);
            this.groupBox1.Controls.Add(this.materialLabel2);
            this.groupBox1.Controls.Add(this.mtxtb_StorePort);
            this.groupBox1.Controls.Add(this.mtxtb_StoreHost);
            this.groupBox1.Controls.Add(this.mtxtb_StoreAETitle);
            this.groupBox1.Controls.Add(this.materialLabel1);
            this.groupBox1.Font = new System.Drawing.Font("Microsoft Tai Le", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox1.Location = new System.Drawing.Point(28, 27);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(608, 296);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Store SCP Settings";
            // 
            // materialLabel3
            // 
            this.materialLabel3.AutoSize = true;
            this.materialLabel3.Depth = 0;
            this.materialLabel3.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel3.Location = new System.Drawing.Point(36, 213);
            this.materialLabel3.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel3.Name = "materialLabel3";
            this.materialLabel3.Size = new System.Drawing.Size(90, 19);
            this.materialLabel3.TabIndex = 5;
            this.materialLabel3.Text = "Port Number";
            // 
            // materialLabel2
            // 
            this.materialLabel2.AutoSize = true;
            this.materialLabel2.Depth = 0;
            this.materialLabel2.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel2.Location = new System.Drawing.Point(36, 142);
            this.materialLabel2.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel2.Name = "materialLabel2";
            this.materialLabel2.Size = new System.Drawing.Size(95, 19);
            this.materialLabel2.TabIndex = 4;
            this.materialLabel2.Text = "Host Address";
            // 
            // mtxtb_StorePort
            // 
            this.mtxtb_StorePort.AnimateReadOnly = false;
            this.mtxtb_StorePort.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxtb_StorePort.Depth = 0;
            this.mtxtb_StorePort.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxtb_StorePort.LeadingIcon = null;
            this.mtxtb_StorePort.Location = new System.Drawing.Point(201, 194);
            this.mtxtb_StorePort.MaxLength = 50;
            this.mtxtb_StorePort.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_StorePort.Multiline = false;
            this.mtxtb_StorePort.Name = "mtxtb_StorePort";
            this.mtxtb_StorePort.Size = new System.Drawing.Size(95, 50);
            this.mtxtb_StorePort.TabIndex = 3;
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
            this.mtxtb_StoreHost.Location = new System.Drawing.Point(201, 124);
            this.mtxtb_StoreHost.MaxLength = 50;
            this.mtxtb_StoreHost.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_StoreHost.Multiline = false;
            this.mtxtb_StoreHost.Name = "mtxtb_StoreHost";
            this.mtxtb_StoreHost.Size = new System.Drawing.Size(357, 50);
            this.mtxtb_StoreHost.TabIndex = 2;
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
            this.mtxtb_StoreAETitle.Location = new System.Drawing.Point(201, 55);
            this.mtxtb_StoreAETitle.MaxLength = 50;
            this.mtxtb_StoreAETitle.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_StoreAETitle.Multiline = false;
            this.mtxtb_StoreAETitle.Name = "mtxtb_StoreAETitle";
            this.mtxtb_StoreAETitle.Size = new System.Drawing.Size(357, 50);
            this.mtxtb_StoreAETitle.TabIndex = 1;
            this.mtxtb_StoreAETitle.Text = "STORESERVER";
            this.mtxtb_StoreAETitle.TrailingIcon = null;
            // 
            // materialLabel1
            // 
            this.materialLabel1.AutoSize = true;
            this.materialLabel1.Depth = 0;
            this.materialLabel1.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel1.Location = new System.Drawing.Point(36, 69);
            this.materialLabel1.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel1.Name = "materialLabel1";
            this.materialLabel1.Size = new System.Drawing.Size(55, 19);
            this.materialLabel1.TabIndex = 0;
            this.materialLabel1.Text = "AE Title";
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.materialLabel4);
            this.groupBox2.Controls.Add(this.materialLabel5);
            this.groupBox2.Controls.Add(this.mtxtb_ModalityPort);
            this.groupBox2.Controls.Add(this.mtxtb_ModalityHost);
            this.groupBox2.Controls.Add(this.mtxtb_ModalityAETitle);
            this.groupBox2.Controls.Add(this.materialLabel6);
            this.groupBox2.Font = new System.Drawing.Font("Microsoft Tai Le", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(28, 358);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(608, 301);
            this.groupBox2.TabIndex = 6;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Modality SCP Settings";
            // 
            // materialLabel4
            // 
            this.materialLabel4.AutoSize = true;
            this.materialLabel4.Depth = 0;
            this.materialLabel4.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel4.Location = new System.Drawing.Point(36, 217);
            this.materialLabel4.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel4.Name = "materialLabel4";
            this.materialLabel4.Size = new System.Drawing.Size(90, 19);
            this.materialLabel4.TabIndex = 5;
            this.materialLabel4.Text = "Port Number";
            // 
            // materialLabel5
            // 
            this.materialLabel5.AutoSize = true;
            this.materialLabel5.Depth = 0;
            this.materialLabel5.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel5.Location = new System.Drawing.Point(36, 146);
            this.materialLabel5.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel5.Name = "materialLabel5";
            this.materialLabel5.Size = new System.Drawing.Size(95, 19);
            this.materialLabel5.TabIndex = 4;
            this.materialLabel5.Text = "Host Address";
            // 
            // mtxtb_ModalityPort
            // 
            this.mtxtb_ModalityPort.AnimateReadOnly = false;
            this.mtxtb_ModalityPort.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxtb_ModalityPort.Depth = 0;
            this.mtxtb_ModalityPort.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxtb_ModalityPort.LeadingIcon = null;
            this.mtxtb_ModalityPort.Location = new System.Drawing.Point(201, 198);
            this.mtxtb_ModalityPort.MaxLength = 50;
            this.mtxtb_ModalityPort.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_ModalityPort.Multiline = false;
            this.mtxtb_ModalityPort.Name = "mtxtb_ModalityPort";
            this.mtxtb_ModalityPort.Size = new System.Drawing.Size(95, 50);
            this.mtxtb_ModalityPort.TabIndex = 3;
            this.mtxtb_ModalityPort.Text = "2008";
            this.mtxtb_ModalityPort.TrailingIcon = null;
            // 
            // mtxtb_ModalityHost
            // 
            this.mtxtb_ModalityHost.AnimateReadOnly = false;
            this.mtxtb_ModalityHost.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxtb_ModalityHost.Depth = 0;
            this.mtxtb_ModalityHost.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxtb_ModalityHost.LeadingIcon = null;
            this.mtxtb_ModalityHost.Location = new System.Drawing.Point(201, 128);
            this.mtxtb_ModalityHost.MaxLength = 50;
            this.mtxtb_ModalityHost.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_ModalityHost.Multiline = false;
            this.mtxtb_ModalityHost.Name = "mtxtb_ModalityHost";
            this.mtxtb_ModalityHost.Size = new System.Drawing.Size(357, 50);
            this.mtxtb_ModalityHost.TabIndex = 2;
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
            this.mtxtb_ModalityAETitle.Location = new System.Drawing.Point(201, 59);
            this.mtxtb_ModalityAETitle.MaxLength = 50;
            this.mtxtb_ModalityAETitle.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_ModalityAETitle.Multiline = false;
            this.mtxtb_ModalityAETitle.Name = "mtxtb_ModalityAETitle";
            this.mtxtb_ModalityAETitle.Size = new System.Drawing.Size(357, 50);
            this.mtxtb_ModalityAETitle.TabIndex = 1;
            this.mtxtb_ModalityAETitle.Text = "MODALITYSCP";
            this.mtxtb_ModalityAETitle.TrailingIcon = null;
            // 
            // materialLabel6
            // 
            this.materialLabel6.AutoSize = true;
            this.materialLabel6.Depth = 0;
            this.materialLabel6.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel6.Location = new System.Drawing.Point(36, 73);
            this.materialLabel6.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel6.Name = "materialLabel6";
            this.materialLabel6.Size = new System.Drawing.Size(55, 19);
            this.materialLabel6.TabIndex = 0;
            this.materialLabel6.Text = "AE Title";
            // 
            // uctrl_SCPSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Name = "uctrl_SCPSettings";
            this.Size = new System.Drawing.Size(673, 687);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private MaterialSkin.Controls.MaterialLabel materialLabel1;
        private MaterialSkin.Controls.MaterialLabel materialLabel3;
        private MaterialSkin.Controls.MaterialLabel materialLabel2;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_StorePort;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_StoreHost;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_StoreAETitle;
        private System.Windows.Forms.GroupBox groupBox2;
        private MaterialSkin.Controls.MaterialLabel materialLabel4;
        private MaterialSkin.Controls.MaterialLabel materialLabel5;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_ModalityHost;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_ModalityAETitle;
        private MaterialSkin.Controls.MaterialLabel materialLabel6;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_ModalityPort;
    }
}
