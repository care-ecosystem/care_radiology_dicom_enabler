
namespace Plexus_DICOM_Enabler
{
    partial class frm_Mainform
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frm_Mainform));
            this.imgList_Icons = new System.Windows.Forms.ImageList(this.components);
            this.mtc_Modules = new MaterialSkin.Controls.MaterialTabControl();
            this.tbp_ServerManager = new System.Windows.Forms.TabPage();
            this.uctrl_ServerManager1 = new Plexus_DICOM_Enabler.UserControls.uctrl_ServerManager();
            this.tbp_Settings = new System.Windows.Forms.TabPage();
            this.mbtn_SaveSCPSettings = new MaterialSkin.Controls.MaterialButton();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.materialLabel3 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel2 = new MaterialSkin.Controls.MaterialLabel();
            this.mtxtb_StorePort = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxtb_StoreHost = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxtb_StoreAETitle = new MaterialSkin.Controls.MaterialTextBox();
            this.materialLabel1 = new MaterialSkin.Controls.MaterialLabel();
            this.grpb_ModalitySCP = new System.Windows.Forms.GroupBox();
            this.materialLabel4 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel5 = new MaterialSkin.Controls.MaterialLabel();
            this.mtxtb_ModalityPort = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxtb_ModalityHost = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxtb_ModalityAETitle = new MaterialSkin.Controls.MaterialTextBox();
            this.materialLabel6 = new MaterialSkin.Controls.MaterialLabel();
            this.tbp_SCUSettings = new System.Windows.Forms.TabPage();
            this.mbtn_SaveSCUSettings = new MaterialSkin.Controls.MaterialButton();
            this.grpb_StoreSCUSettings = new System.Windows.Forms.GroupBox();
            this.mtb_callingAETitle = new MaterialSkin.Controls.MaterialTextBox();
            this.materialLabel14 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel9 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel11 = new MaterialSkin.Controls.MaterialLabel();
            this.mtxtb_StoreSCUPort = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxtb_StoreSCUHost = new MaterialSkin.Controls.MaterialTextBox();
            this.mtxtb_StoreSCUAETitle = new MaterialSkin.Controls.MaterialTextBox();
            this.materialLabel13 = new MaterialSkin.Controls.MaterialLabel();
            this.tbp_ServerList = new System.Windows.Forms.TabPage();
            this.mtchkb_CheckServer = new MaterialSkin.Controls.MaterialCheckbox();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.rtb_Description = new System.Windows.Forms.RichTextBox();
            this.txt_PortNo = new System.Windows.Forms.TextBox();
            this.txt_HostAddress = new System.Windows.Forms.TextBox();
            this.txt_AETitle = new System.Windows.Forms.TextBox();
            this.txt_ServerName = new System.Windows.Forms.TextBox();
            this.materialLabel12 = new MaterialSkin.Controls.MaterialLabel();
            this.mtbtn_AddUpdateServer = new MaterialSkin.Controls.MaterialButton();
            this.dgv_ServerList = new System.Windows.Forms.DataGridView();
            this.pk = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.servername = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.serverAETitle = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.serverHost = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.serverPort = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.description = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.delete = new System.Windows.Forms.DataGridViewImageColumn();
            this.tdp_ViewPatients = new System.Windows.Forms.TabPage();
            this.mbtn_PatientRefresh = new MaterialSkin.Controls.MaterialButton();
            this.dgv_PatientList = new System.Windows.Forms.DataGridView();
            this.patientid = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.patName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.accessionNumber = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.modality = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.status = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.noofseries = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.noofimages = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tbp_ViewLog = new System.Windows.Forms.TabPage();
            this.tbc_Logs = new System.Windows.Forms.TabControl();
            this.tp_MWLLog = new System.Windows.Forms.TabPage();
            this.rtb_MWLLog = new System.Windows.Forms.RichTextBox();
            this.tp_SCPLog = new System.Windows.Forms.TabPage();
            this.rtb_SCPLog = new System.Windows.Forms.RichTextBox();
            this.tp_SCULog = new System.Windows.Forms.TabPage();
            this.rtb_SCULog = new System.Windows.Forms.RichTextBox();
            this.tbp_AboutUs = new System.Windows.Forms.TabPage();
            this.materialLabel10 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel8 = new MaterialSkin.Controls.MaterialLabel();
            this.label1 = new System.Windows.Forms.Label();
            this.materialLabel7 = new MaterialSkin.Controls.MaterialLabel();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.materialDrawer1 = new MaterialSkin.Controls.MaterialDrawer();
            this.mtc_Modules.SuspendLayout();
            this.tbp_ServerManager.SuspendLayout();
            this.tbp_Settings.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.grpb_ModalitySCP.SuspendLayout();
            this.tbp_SCUSettings.SuspendLayout();
            this.grpb_StoreSCUSettings.SuspendLayout();
            this.tbp_ServerList.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_ServerList)).BeginInit();
            this.tdp_ViewPatients.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_PatientList)).BeginInit();
            this.tbp_ViewLog.SuspendLayout();
            this.tbc_Logs.SuspendLayout();
            this.tp_MWLLog.SuspendLayout();
            this.tp_SCPLog.SuspendLayout();
            this.tp_SCULog.SuspendLayout();
            this.tbp_AboutUs.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // imgList_Icons
            // 
            this.imgList_Icons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imgList_Icons.ImageStream")));
            this.imgList_Icons.TransparentColor = System.Drawing.Color.Transparent;
            this.imgList_Icons.Images.SetKeyName(0, "aboutus.png");
            this.imgList_Icons.Images.SetKeyName(1, "serverlist.png");
            this.imgList_Icons.Images.SetKeyName(2, "services.png");
            this.imgList_Icons.Images.SetKeyName(3, "settings.png");
            this.imgList_Icons.Images.SetKeyName(4, "ViewLogs.png");
            this.imgList_Icons.Images.SetKeyName(5, "patientlist.png");
            this.imgList_Icons.Images.SetKeyName(6, "scusetting.png");
            // 
            // mtc_Modules
            // 
            this.mtc_Modules.Controls.Add(this.tbp_ServerManager);
            this.mtc_Modules.Controls.Add(this.tbp_Settings);
            this.mtc_Modules.Controls.Add(this.tbp_SCUSettings);
            this.mtc_Modules.Controls.Add(this.tbp_ServerList);
            this.mtc_Modules.Controls.Add(this.tdp_ViewPatients);
            this.mtc_Modules.Controls.Add(this.tbp_ViewLog);
            this.mtc_Modules.Controls.Add(this.tbp_AboutUs);
            this.mtc_Modules.Depth = 0;
            this.mtc_Modules.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mtc_Modules.ImageList = this.imgList_Icons;
            this.mtc_Modules.Location = new System.Drawing.Point(3, 64);
            this.mtc_Modules.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.mtc_Modules.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtc_Modules.Multiline = true;
            this.mtc_Modules.Name = "mtc_Modules";
            this.mtc_Modules.SelectedIndex = 0;
            this.mtc_Modules.Size = new System.Drawing.Size(1037, 789);
            this.mtc_Modules.TabIndex = 0;
            this.mtc_Modules.SelectedIndexChanged += new System.EventHandler(this.mtc_Modules_SelectedIndexChanged);
            // 
            // tbp_ServerManager
            // 
            this.tbp_ServerManager.Controls.Add(this.uctrl_ServerManager1);
            this.tbp_ServerManager.ImageKey = "services.png";
            this.tbp_ServerManager.Location = new System.Drawing.Point(4, 25);
            this.tbp_ServerManager.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tbp_ServerManager.Name = "tbp_ServerManager";
            this.tbp_ServerManager.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tbp_ServerManager.Size = new System.Drawing.Size(1029, 760);
            this.tbp_ServerManager.TabIndex = 0;
            this.tbp_ServerManager.Text = "Server Manager";
            this.tbp_ServerManager.UseVisualStyleBackColor = true;
            // 
            // uctrl_ServerManager1
            // 
            this.uctrl_ServerManager1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uctrl_ServerManager1.Location = new System.Drawing.Point(3, 2);
            this.uctrl_ServerManager1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.uctrl_ServerManager1.Name = "uctrl_ServerManager1";
            this.uctrl_ServerManager1.Size = new System.Drawing.Size(1023, 756);
            this.uctrl_ServerManager1.TabIndex = 0;
            // 
            // tbp_Settings
            // 
            this.tbp_Settings.Controls.Add(this.mbtn_SaveSCPSettings);
            this.tbp_Settings.Controls.Add(this.groupBox1);
            this.tbp_Settings.Controls.Add(this.grpb_ModalitySCP);
            this.tbp_Settings.ImageKey = "settings.png";
            this.tbp_Settings.Location = new System.Drawing.Point(4, 25);
            this.tbp_Settings.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tbp_Settings.Name = "tbp_Settings";
            this.tbp_Settings.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tbp_Settings.Size = new System.Drawing.Size(1029, 760);
            this.tbp_Settings.TabIndex = 1;
            this.tbp_Settings.Text = "SCP Settings";
            this.tbp_Settings.UseVisualStyleBackColor = true;
            // 
            // mbtn_SaveSCPSettings
            // 
            this.mbtn_SaveSCPSettings.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mbtn_SaveSCPSettings.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mbtn_SaveSCPSettings.Depth = 0;
            this.mbtn_SaveSCPSettings.HighEmphasis = true;
            this.mbtn_SaveSCPSettings.Icon = null;
            this.mbtn_SaveSCPSettings.Location = new System.Drawing.Point(933, 693);
            this.mbtn_SaveSCPSettings.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mbtn_SaveSCPSettings.MouseState = MaterialSkin.MouseState.HOVER;
            this.mbtn_SaveSCPSettings.Name = "mbtn_SaveSCPSettings";
            this.mbtn_SaveSCPSettings.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mbtn_SaveSCPSettings.Size = new System.Drawing.Size(64, 36);
            this.mbtn_SaveSCPSettings.TabIndex = 9;
            this.mbtn_SaveSCPSettings.Text = "Save";
            this.mbtn_SaveSCPSettings.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mbtn_SaveSCPSettings.UseAccentColor = false;
            this.mbtn_SaveSCPSettings.UseVisualStyleBackColor = true;
            this.mbtn_SaveSCPSettings.Click += new System.EventHandler(this.mbtn_SaveSCPSettings_Click);
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
            this.groupBox1.Location = new System.Drawing.Point(101, 36);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox1.Size = new System.Drawing.Size(896, 297);
            this.groupBox1.TabIndex = 7;
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
            this.mtxtb_StorePort.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
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
            this.mtxtb_StoreHost.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
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
            this.mtxtb_StoreAETitle.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
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
            // grpb_ModalitySCP
            // 
            this.grpb_ModalitySCP.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpb_ModalitySCP.Controls.Add(this.materialLabel4);
            this.grpb_ModalitySCP.Controls.Add(this.materialLabel5);
            this.grpb_ModalitySCP.Controls.Add(this.mtxtb_ModalityPort);
            this.grpb_ModalitySCP.Controls.Add(this.mtxtb_ModalityHost);
            this.grpb_ModalitySCP.Controls.Add(this.mtxtb_ModalityAETitle);
            this.grpb_ModalitySCP.Controls.Add(this.materialLabel6);
            this.grpb_ModalitySCP.Font = new System.Drawing.Font("Microsoft Tai Le", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpb_ModalitySCP.Location = new System.Drawing.Point(101, 367);
            this.grpb_ModalitySCP.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.grpb_ModalitySCP.Name = "grpb_ModalitySCP";
            this.grpb_ModalitySCP.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.grpb_ModalitySCP.Size = new System.Drawing.Size(896, 303);
            this.grpb_ModalitySCP.TabIndex = 8;
            this.grpb_ModalitySCP.TabStop = false;
            this.grpb_ModalitySCP.Text = "Modality SCP Settings";
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
            this.mtxtb_ModalityPort.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
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
            this.mtxtb_ModalityHost.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
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
            this.mtxtb_ModalityAETitle.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
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
            // tbp_SCUSettings
            // 
            this.tbp_SCUSettings.Controls.Add(this.mbtn_SaveSCUSettings);
            this.tbp_SCUSettings.Controls.Add(this.grpb_StoreSCUSettings);
            this.tbp_SCUSettings.ImageKey = "scusetting.png";
            this.tbp_SCUSettings.Location = new System.Drawing.Point(4, 25);
            this.tbp_SCUSettings.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tbp_SCUSettings.Name = "tbp_SCUSettings";
            this.tbp_SCUSettings.Size = new System.Drawing.Size(1029, 760);
            this.tbp_SCUSettings.TabIndex = 6;
            this.tbp_SCUSettings.Text = "SCU Settings";
            this.tbp_SCUSettings.UseVisualStyleBackColor = true;
            // 
            // mbtn_SaveSCUSettings
            // 
            this.mbtn_SaveSCUSettings.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mbtn_SaveSCUSettings.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mbtn_SaveSCUSettings.Depth = 0;
            this.mbtn_SaveSCUSettings.HighEmphasis = true;
            this.mbtn_SaveSCUSettings.Icon = null;
            this.mbtn_SaveSCUSettings.Location = new System.Drawing.Point(912, 400);
            this.mbtn_SaveSCUSettings.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mbtn_SaveSCUSettings.MouseState = MaterialSkin.MouseState.HOVER;
            this.mbtn_SaveSCUSettings.Name = "mbtn_SaveSCUSettings";
            this.mbtn_SaveSCUSettings.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mbtn_SaveSCUSettings.Size = new System.Drawing.Size(64, 36);
            this.mbtn_SaveSCUSettings.TabIndex = 10;
            this.mbtn_SaveSCUSettings.Text = "Save";
            this.mbtn_SaveSCUSettings.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mbtn_SaveSCUSettings.UseAccentColor = false;
            this.mbtn_SaveSCUSettings.UseVisualStyleBackColor = true;
            this.mbtn_SaveSCUSettings.Click += new System.EventHandler(this.mbtn_SaveSCUSettings_Click);
            // 
            // grpb_StoreSCUSettings
            // 
            this.grpb_StoreSCUSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpb_StoreSCUSettings.Controls.Add(this.mtb_callingAETitle);
            this.grpb_StoreSCUSettings.Controls.Add(this.materialLabel14);
            this.grpb_StoreSCUSettings.Controls.Add(this.materialLabel9);
            this.grpb_StoreSCUSettings.Controls.Add(this.materialLabel11);
            this.grpb_StoreSCUSettings.Controls.Add(this.mtxtb_StoreSCUPort);
            this.grpb_StoreSCUSettings.Controls.Add(this.mtxtb_StoreSCUHost);
            this.grpb_StoreSCUSettings.Controls.Add(this.mtxtb_StoreSCUAETitle);
            this.grpb_StoreSCUSettings.Controls.Add(this.materialLabel13);
            this.grpb_StoreSCUSettings.Font = new System.Drawing.Font("Microsoft Tai Le", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpb_StoreSCUSettings.Location = new System.Drawing.Point(80, 41);
            this.grpb_StoreSCUSettings.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.grpb_StoreSCUSettings.Name = "grpb_StoreSCUSettings";
            this.grpb_StoreSCUSettings.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.grpb_StoreSCUSettings.Size = new System.Drawing.Size(896, 351);
            this.grpb_StoreSCUSettings.TabIndex = 8;
            this.grpb_StoreSCUSettings.TabStop = false;
            this.grpb_StoreSCUSettings.Text = "Store SCU Settings";
            // 
            // mtb_callingAETitle
            // 
            this.mtb_callingAETitle.AnimateReadOnly = false;
            this.mtb_callingAETitle.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtb_callingAETitle.Depth = 0;
            this.mtb_callingAETitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtb_callingAETitle.LeadingIcon = null;
            this.mtb_callingAETitle.Location = new System.Drawing.Point(201, 270);
            this.mtb_callingAETitle.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.mtb_callingAETitle.MaxLength = 50;
            this.mtb_callingAETitle.MouseState = MaterialSkin.MouseState.OUT;
            this.mtb_callingAETitle.Multiline = false;
            this.mtb_callingAETitle.Name = "mtb_callingAETitle";
            this.mtb_callingAETitle.Size = new System.Drawing.Size(357, 50);
            this.mtb_callingAETitle.TabIndex = 7;
            this.mtb_callingAETitle.Text = "STORESCU";
            this.mtb_callingAETitle.TrailingIcon = null;
            // 
            // materialLabel14
            // 
            this.materialLabel14.AutoSize = true;
            this.materialLabel14.Depth = 0;
            this.materialLabel14.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel14.Location = new System.Drawing.Point(36, 288);
            this.materialLabel14.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel14.Name = "materialLabel14";
            this.materialLabel14.Size = new System.Drawing.Size(108, 19);
            this.materialLabel14.TabIndex = 6;
            this.materialLabel14.Text = "Calling AE Title";
            // 
            // materialLabel9
            // 
            this.materialLabel9.AutoSize = true;
            this.materialLabel9.Depth = 0;
            this.materialLabel9.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel9.Location = new System.Drawing.Point(36, 213);
            this.materialLabel9.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel9.Name = "materialLabel9";
            this.materialLabel9.Size = new System.Drawing.Size(90, 19);
            this.materialLabel9.TabIndex = 5;
            this.materialLabel9.Text = "Port Number";
            // 
            // materialLabel11
            // 
            this.materialLabel11.AutoSize = true;
            this.materialLabel11.Depth = 0;
            this.materialLabel11.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel11.Location = new System.Drawing.Point(36, 142);
            this.materialLabel11.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel11.Name = "materialLabel11";
            this.materialLabel11.Size = new System.Drawing.Size(95, 19);
            this.materialLabel11.TabIndex = 4;
            this.materialLabel11.Text = "Host Address";
            // 
            // mtxtb_StoreSCUPort
            // 
            this.mtxtb_StoreSCUPort.AnimateReadOnly = false;
            this.mtxtb_StoreSCUPort.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxtb_StoreSCUPort.Depth = 0;
            this.mtxtb_StoreSCUPort.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxtb_StoreSCUPort.LeadingIcon = null;
            this.mtxtb_StoreSCUPort.Location = new System.Drawing.Point(201, 194);
            this.mtxtb_StoreSCUPort.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.mtxtb_StoreSCUPort.MaxLength = 50;
            this.mtxtb_StoreSCUPort.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_StoreSCUPort.Multiline = false;
            this.mtxtb_StoreSCUPort.Name = "mtxtb_StoreSCUPort";
            this.mtxtb_StoreSCUPort.Size = new System.Drawing.Size(95, 50);
            this.mtxtb_StoreSCUPort.TabIndex = 3;
            this.mtxtb_StoreSCUPort.Text = "2007";
            this.mtxtb_StoreSCUPort.TrailingIcon = null;
            // 
            // mtxtb_StoreSCUHost
            // 
            this.mtxtb_StoreSCUHost.AnimateReadOnly = false;
            this.mtxtb_StoreSCUHost.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxtb_StoreSCUHost.Depth = 0;
            this.mtxtb_StoreSCUHost.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxtb_StoreSCUHost.LeadingIcon = null;
            this.mtxtb_StoreSCUHost.Location = new System.Drawing.Point(201, 124);
            this.mtxtb_StoreSCUHost.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.mtxtb_StoreSCUHost.MaxLength = 50;
            this.mtxtb_StoreSCUHost.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_StoreSCUHost.Multiline = false;
            this.mtxtb_StoreSCUHost.Name = "mtxtb_StoreSCUHost";
            this.mtxtb_StoreSCUHost.Size = new System.Drawing.Size(357, 50);
            this.mtxtb_StoreSCUHost.TabIndex = 2;
            this.mtxtb_StoreSCUHost.Text = "127.0.0.1";
            this.mtxtb_StoreSCUHost.TrailingIcon = null;
            // 
            // mtxtb_StoreSCUAETitle
            // 
            this.mtxtb_StoreSCUAETitle.AnimateReadOnly = false;
            this.mtxtb_StoreSCUAETitle.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxtb_StoreSCUAETitle.Depth = 0;
            this.mtxtb_StoreSCUAETitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxtb_StoreSCUAETitle.LeadingIcon = null;
            this.mtxtb_StoreSCUAETitle.Location = new System.Drawing.Point(201, 55);
            this.mtxtb_StoreSCUAETitle.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.mtxtb_StoreSCUAETitle.MaxLength = 50;
            this.mtxtb_StoreSCUAETitle.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_StoreSCUAETitle.Multiline = false;
            this.mtxtb_StoreSCUAETitle.Name = "mtxtb_StoreSCUAETitle";
            this.mtxtb_StoreSCUAETitle.Size = new System.Drawing.Size(357, 50);
            this.mtxtb_StoreSCUAETitle.TabIndex = 1;
            this.mtxtb_StoreSCUAETitle.Text = "STORESERVER";
            this.mtxtb_StoreSCUAETitle.TrailingIcon = null;
            // 
            // materialLabel13
            // 
            this.materialLabel13.AutoSize = true;
            this.materialLabel13.Depth = 0;
            this.materialLabel13.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel13.Location = new System.Drawing.Point(36, 69);
            this.materialLabel13.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel13.Name = "materialLabel13";
            this.materialLabel13.Size = new System.Drawing.Size(55, 19);
            this.materialLabel13.TabIndex = 0;
            this.materialLabel13.Text = "AE Title";
            // 
            // tbp_ServerList
            // 
            this.tbp_ServerList.Controls.Add(this.mtchkb_CheckServer);
            this.tbp_ServerList.Controls.Add(this.label6);
            this.tbp_ServerList.Controls.Add(this.label5);
            this.tbp_ServerList.Controls.Add(this.label4);
            this.tbp_ServerList.Controls.Add(this.label3);
            this.tbp_ServerList.Controls.Add(this.label2);
            this.tbp_ServerList.Controls.Add(this.rtb_Description);
            this.tbp_ServerList.Controls.Add(this.txt_PortNo);
            this.tbp_ServerList.Controls.Add(this.txt_HostAddress);
            this.tbp_ServerList.Controls.Add(this.txt_AETitle);
            this.tbp_ServerList.Controls.Add(this.txt_ServerName);
            this.tbp_ServerList.Controls.Add(this.materialLabel12);
            this.tbp_ServerList.Controls.Add(this.mtbtn_AddUpdateServer);
            this.tbp_ServerList.Controls.Add(this.dgv_ServerList);
            this.tbp_ServerList.ImageKey = "serverlist.png";
            this.tbp_ServerList.Location = new System.Drawing.Point(4, 25);
            this.tbp_ServerList.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tbp_ServerList.Name = "tbp_ServerList";
            this.tbp_ServerList.Size = new System.Drawing.Size(1029, 760);
            this.tbp_ServerList.TabIndex = 2;
            this.tbp_ServerList.Text = "Server List";
            this.tbp_ServerList.UseVisualStyleBackColor = true;
            // 
            // mtchkb_CheckServer
            // 
            this.mtchkb_CheckServer.AutoSize = true;
            this.mtchkb_CheckServer.Depth = 0;
            this.mtchkb_CheckServer.Location = new System.Drawing.Point(132, 4);
            this.mtchkb_CheckServer.Margin = new System.Windows.Forms.Padding(0);
            this.mtchkb_CheckServer.MouseLocation = new System.Drawing.Point(-1, -1);
            this.mtchkb_CheckServer.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtchkb_CheckServer.Name = "mtchkb_CheckServer";
            this.mtchkb_CheckServer.ReadOnly = false;
            this.mtchkb_CheckServer.Ripple = true;
            this.mtchkb_CheckServer.Size = new System.Drawing.Size(221, 37);
            this.mtchkb_CheckServer.TabIndex = 15;
            this.mtchkb_CheckServer.Text = "Restrict Receive of Images";
            this.mtchkb_CheckServer.UseVisualStyleBackColor = true;
            this.mtchkb_CheckServer.CheckedChanged += new System.EventHandler(this.mtchkb_CheckServer_CheckedChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(129, 127);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(98, 16);
            this.label6.TabIndex = 14;
            this.label6.Text = "Description : ";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(757, 60);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(105, 16);
            this.label5.TabIndex = 13;
            this.label5.Text = "Port Number : ";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(549, 60);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(113, 16);
            this.label4.TabIndex = 11;
            this.label4.Text = "Host Address : ";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(341, 60);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(74, 16);
            this.label3.TabIndex = 10;
            this.label3.Text = "AE Title : ";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(129, 62);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(110, 16);
            this.label2.TabIndex = 9;
            this.label2.Text = "Server Name : ";
            // 
            // rtb_Description
            // 
            this.rtb_Description.Location = new System.Drawing.Point(129, 146);
            this.rtb_Description.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.rtb_Description.Name = "rtb_Description";
            this.rtb_Description.Size = new System.Drawing.Size(465, 75);
            this.rtb_Description.TabIndex = 8;
            this.rtb_Description.Text = "";
            // 
            // txt_PortNo
            // 
            this.txt_PortNo.Location = new System.Drawing.Point(761, 84);
            this.txt_PortNo.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.txt_PortNo.Name = "txt_PortNo";
            this.txt_PortNo.Size = new System.Drawing.Size(183, 22);
            this.txt_PortNo.TabIndex = 7;
            // 
            // txt_HostAddress
            // 
            this.txt_HostAddress.Location = new System.Drawing.Point(552, 84);
            this.txt_HostAddress.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.txt_HostAddress.Name = "txt_HostAddress";
            this.txt_HostAddress.Size = new System.Drawing.Size(183, 22);
            this.txt_HostAddress.TabIndex = 6;
            // 
            // txt_AETitle
            // 
            this.txt_AETitle.Location = new System.Drawing.Point(345, 84);
            this.txt_AETitle.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.txt_AETitle.Name = "txt_AETitle";
            this.txt_AETitle.Size = new System.Drawing.Size(183, 22);
            this.txt_AETitle.TabIndex = 5;
            // 
            // txt_ServerName
            // 
            this.txt_ServerName.Location = new System.Drawing.Point(129, 84);
            this.txt_ServerName.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.txt_ServerName.Name = "txt_ServerName";
            this.txt_ServerName.Size = new System.Drawing.Size(183, 22);
            this.txt_ServerName.TabIndex = 4;
            // 
            // materialLabel12
            // 
            this.materialLabel12.AutoSize = true;
            this.materialLabel12.Depth = 0;
            this.materialLabel12.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel12.Location = new System.Drawing.Point(283, 23);
            this.materialLabel12.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel12.Name = "materialLabel12";
            this.materialLabel12.Size = new System.Drawing.Size(1, 0);
            this.materialLabel12.TabIndex = 3;
            // 
            // mtbtn_AddUpdateServer
            // 
            this.mtbtn_AddUpdateServer.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mtbtn_AddUpdateServer.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mtbtn_AddUpdateServer.Depth = 0;
            this.mtbtn_AddUpdateServer.HighEmphasis = true;
            this.mtbtn_AddUpdateServer.Icon = null;
            this.mtbtn_AddUpdateServer.Location = new System.Drawing.Point(836, 186);
            this.mtbtn_AddUpdateServer.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mtbtn_AddUpdateServer.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtbtn_AddUpdateServer.Name = "mtbtn_AddUpdateServer";
            this.mtbtn_AddUpdateServer.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mtbtn_AddUpdateServer.Size = new System.Drawing.Size(108, 36);
            this.mtbtn_AddUpdateServer.TabIndex = 1;
            this.mtbtn_AddUpdateServer.Text = "Add Server";
            this.mtbtn_AddUpdateServer.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mtbtn_AddUpdateServer.UseAccentColor = false;
            this.mtbtn_AddUpdateServer.UseVisualStyleBackColor = true;
            this.mtbtn_AddUpdateServer.Click += new System.EventHandler(this.mtbtn_AddUpdateServer_Click);
            // 
            // dgv_ServerList
            // 
            this.dgv_ServerList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgv_ServerList.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.pk,
            this.servername,
            this.serverAETitle,
            this.serverHost,
            this.serverPort,
            this.description,
            this.delete});
            this.dgv_ServerList.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.dgv_ServerList.Location = new System.Drawing.Point(80, 242);
            this.dgv_ServerList.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.dgv_ServerList.Name = "dgv_ServerList";
            this.dgv_ServerList.RowHeadersWidth = 51;
            this.dgv_ServerList.RowTemplate.Height = 24;
            this.dgv_ServerList.Size = new System.Drawing.Size(947, 519);
            this.dgv_ServerList.TabIndex = 0;
            this.dgv_ServerList.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgv_ServerList_CellClick);
            this.dgv_ServerList.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgv_ServerList_CellContentClick);
            // 
            // pk
            // 
            this.pk.DataPropertyName = "pk";
            this.pk.HeaderText = "pk";
            this.pk.MinimumWidth = 6;
            this.pk.Name = "pk";
            this.pk.Visible = false;
            this.pk.Width = 125;
            // 
            // servername
            // 
            this.servername.DataPropertyName = "name";
            this.servername.FillWeight = 150F;
            this.servername.HeaderText = "Server Name";
            this.servername.MinimumWidth = 6;
            this.servername.Name = "servername";
            this.servername.Width = 125;
            // 
            // serverAETitle
            // 
            this.serverAETitle.DataPropertyName = "aetitle";
            this.serverAETitle.FillWeight = 150F;
            this.serverAETitle.HeaderText = "AE Title";
            this.serverAETitle.MinimumWidth = 6;
            this.serverAETitle.Name = "serverAETitle";
            this.serverAETitle.Width = 125;
            // 
            // serverHost
            // 
            this.serverHost.DataPropertyName = "hostaddress";
            this.serverHost.FillWeight = 150F;
            this.serverHost.HeaderText = "Host Address";
            this.serverHost.MinimumWidth = 6;
            this.serverHost.Name = "serverHost";
            this.serverHost.Width = 125;
            // 
            // serverPort
            // 
            this.serverPort.DataPropertyName = "portnumber";
            this.serverPort.HeaderText = "Port Number";
            this.serverPort.MinimumWidth = 6;
            this.serverPort.Name = "serverPort";
            this.serverPort.Width = 125;
            // 
            // description
            // 
            this.description.DataPropertyName = "description";
            this.description.FillWeight = 200F;
            this.description.HeaderText = "Description";
            this.description.MinimumWidth = 6;
            this.description.Name = "description";
            this.description.Width = 125;
            // 
            // delete
            // 
            this.delete.HeaderText = "";
            this.delete.Image = global::CARE_DICOM_Enabler.Properties.Resources.delete;
            this.delete.ImageLayout = System.Windows.Forms.DataGridViewImageCellLayout.Zoom;
            this.delete.MinimumWidth = 6;
            this.delete.Name = "delete";
            this.delete.Width = 40;
            // 
            // tdp_ViewPatients
            // 
            this.tdp_ViewPatients.Controls.Add(this.mbtn_PatientRefresh);
            this.tdp_ViewPatients.Controls.Add(this.dgv_PatientList);
            this.tdp_ViewPatients.ImageKey = "patientlist.png";
            this.tdp_ViewPatients.Location = new System.Drawing.Point(4, 25);
            this.tdp_ViewPatients.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tdp_ViewPatients.Name = "tdp_ViewPatients";
            this.tdp_ViewPatients.Size = new System.Drawing.Size(1029, 760);
            this.tdp_ViewPatients.TabIndex = 5;
            this.tdp_ViewPatients.Text = "View Patient List";
            this.tdp_ViewPatients.UseVisualStyleBackColor = true;
            // 
            // mbtn_PatientRefresh
            // 
            this.mbtn_PatientRefresh.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mbtn_PatientRefresh.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mbtn_PatientRefresh.Depth = 0;
            this.mbtn_PatientRefresh.HighEmphasis = true;
            this.mbtn_PatientRefresh.Icon = null;
            this.mbtn_PatientRefresh.Location = new System.Drawing.Point(81, 14);
            this.mbtn_PatientRefresh.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mbtn_PatientRefresh.MouseState = MaterialSkin.MouseState.HOVER;
            this.mbtn_PatientRefresh.Name = "mbtn_PatientRefresh";
            this.mbtn_PatientRefresh.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mbtn_PatientRefresh.Size = new System.Drawing.Size(84, 36);
            this.mbtn_PatientRefresh.TabIndex = 2;
            this.mbtn_PatientRefresh.Text = "Refresh";
            this.mbtn_PatientRefresh.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mbtn_PatientRefresh.UseAccentColor = false;
            this.mbtn_PatientRefresh.UseVisualStyleBackColor = true;
            this.mbtn_PatientRefresh.Click += new System.EventHandler(this.mbtn_PatientRefresh_Click);
            // 
            // dgv_PatientList
            // 
            this.dgv_PatientList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgv_PatientList.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.patientid,
            this.patName,
            this.accessionNumber,
            this.modality,
            this.status,
            this.noofseries,
            this.noofimages});
            this.dgv_PatientList.Location = new System.Drawing.Point(80, 59);
            this.dgv_PatientList.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.dgv_PatientList.Name = "dgv_PatientList";
            this.dgv_PatientList.RowHeadersWidth = 51;
            this.dgv_PatientList.RowTemplate.Height = 24;
            this.dgv_PatientList.Size = new System.Drawing.Size(947, 697);
            this.dgv_PatientList.TabIndex = 1;
            this.dgv_PatientList.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.dataGridView2_CellFormatting);
            // 
            // patientid
            // 
            this.patientid.DataPropertyName = "pat_id";
            this.patientid.HeaderText = "Patient ID";
            this.patientid.MinimumWidth = 6;
            this.patientid.Name = "patientid";
            this.patientid.Width = 125;
            // 
            // patName
            // 
            this.patName.DataPropertyName = "pat_name";
            this.patName.HeaderText = "Patient Name";
            this.patName.MinimumWidth = 6;
            this.patName.Name = "patName";
            this.patName.Width = 125;
            // 
            // accessionNumber
            // 
            this.accessionNumber.DataPropertyName = "accession_no";
            this.accessionNumber.HeaderText = "Accession Number";
            this.accessionNumber.MinimumWidth = 6;
            this.accessionNumber.Name = "accessionNumber";
            this.accessionNumber.Width = 125;
            // 
            // modality
            // 
            this.modality.DataPropertyName = "modality";
            this.modality.HeaderText = "Modality";
            this.modality.MinimumWidth = 6;
            this.modality.Name = "modality";
            this.modality.Width = 125;
            // 
            // status
            // 
            this.status.DataPropertyName = "study_status";
            this.status.HeaderText = "Status";
            this.status.MinimumWidth = 6;
            this.status.Name = "status";
            this.status.Width = 125;
            // 
            // noofseries
            // 
            this.noofseries.DataPropertyName = "num_series";
            this.noofseries.HeaderText = "Series #";
            this.noofseries.MinimumWidth = 6;
            this.noofseries.Name = "noofseries";
            this.noofseries.Width = 125;
            // 
            // noofimages
            // 
            this.noofimages.DataPropertyName = "num_instance";
            this.noofimages.HeaderText = "Images #";
            this.noofimages.MinimumWidth = 6;
            this.noofimages.Name = "noofimages";
            this.noofimages.Width = 125;
            // 
            // tbp_ViewLog
            // 
            this.tbp_ViewLog.Controls.Add(this.tbc_Logs);
            this.tbp_ViewLog.ImageKey = "ViewLogs.png";
            this.tbp_ViewLog.Location = new System.Drawing.Point(4, 25);
            this.tbp_ViewLog.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tbp_ViewLog.Name = "tbp_ViewLog";
            this.tbp_ViewLog.Size = new System.Drawing.Size(1029, 760);
            this.tbp_ViewLog.TabIndex = 3;
            this.tbp_ViewLog.Text = "View Logs";
            this.tbp_ViewLog.UseVisualStyleBackColor = true;
            // 
            // tbc_Logs
            // 
            this.tbc_Logs.Controls.Add(this.tp_MWLLog);
            this.tbc_Logs.Controls.Add(this.tp_SCPLog);
            this.tbc_Logs.Controls.Add(this.tp_SCULog);
            this.tbc_Logs.Location = new System.Drawing.Point(80, 2);
            this.tbc_Logs.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tbc_Logs.Name = "tbc_Logs";
            this.tbc_Logs.SelectedIndex = 0;
            this.tbc_Logs.Size = new System.Drawing.Size(947, 757);
            this.tbc_Logs.TabIndex = 0;
            // 
            // tp_MWLLog
            // 
            this.tp_MWLLog.Controls.Add(this.rtb_MWLLog);
            this.tp_MWLLog.Location = new System.Drawing.Point(4, 25);
            this.tp_MWLLog.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tp_MWLLog.Name = "tp_MWLLog";
            this.tp_MWLLog.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tp_MWLLog.Size = new System.Drawing.Size(939, 728);
            this.tp_MWLLog.TabIndex = 0;
            this.tp_MWLLog.Text = "MWL Log";
            this.tp_MWLLog.UseVisualStyleBackColor = true;
            // 
            // rtb_MWLLog
            // 
            this.rtb_MWLLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb_MWLLog.Location = new System.Drawing.Point(3, 2);
            this.rtb_MWLLog.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.rtb_MWLLog.Name = "rtb_MWLLog";
            this.rtb_MWLLog.Size = new System.Drawing.Size(933, 724);
            this.rtb_MWLLog.TabIndex = 0;
            this.rtb_MWLLog.Text = "";
            // 
            // tp_SCPLog
            // 
            this.tp_SCPLog.Controls.Add(this.rtb_SCPLog);
            this.tp_SCPLog.Location = new System.Drawing.Point(4, 25);
            this.tp_SCPLog.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tp_SCPLog.Name = "tp_SCPLog";
            this.tp_SCPLog.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tp_SCPLog.Size = new System.Drawing.Size(939, 728);
            this.tp_SCPLog.TabIndex = 1;
            this.tp_SCPLog.Text = "SCP Log";
            this.tp_SCPLog.UseVisualStyleBackColor = true;
            // 
            // rtb_SCPLog
            // 
            this.rtb_SCPLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb_SCPLog.Location = new System.Drawing.Point(3, 2);
            this.rtb_SCPLog.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.rtb_SCPLog.Name = "rtb_SCPLog";
            this.rtb_SCPLog.Size = new System.Drawing.Size(933, 724);
            this.rtb_SCPLog.TabIndex = 0;
            this.rtb_SCPLog.Text = "";
            // 
            // tp_SCULog
            // 
            this.tp_SCULog.Controls.Add(this.rtb_SCULog);
            this.tp_SCULog.Location = new System.Drawing.Point(4, 25);
            this.tp_SCULog.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tp_SCULog.Name = "tp_SCULog";
            this.tp_SCULog.Size = new System.Drawing.Size(939, 728);
            this.tp_SCULog.TabIndex = 2;
            this.tp_SCULog.Text = "SCU Log";
            this.tp_SCULog.UseVisualStyleBackColor = true;
            // 
            // rtb_SCULog
            // 
            this.rtb_SCULog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtb_SCULog.Location = new System.Drawing.Point(0, 0);
            this.rtb_SCULog.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.rtb_SCULog.Name = "rtb_SCULog";
            this.rtb_SCULog.Size = new System.Drawing.Size(939, 728);
            this.rtb_SCULog.TabIndex = 0;
            this.rtb_SCULog.Text = "";
            // 
            // tbp_AboutUs
            // 
            this.tbp_AboutUs.Controls.Add(this.materialLabel10);
            this.tbp_AboutUs.Controls.Add(this.materialLabel8);
            this.tbp_AboutUs.Controls.Add(this.label1);
            this.tbp_AboutUs.Controls.Add(this.materialLabel7);
            this.tbp_AboutUs.Controls.Add(this.pictureBox1);
            this.tbp_AboutUs.ImageKey = "aboutus.png";
            this.tbp_AboutUs.Location = new System.Drawing.Point(4, 25);
            this.tbp_AboutUs.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tbp_AboutUs.Name = "tbp_AboutUs";
            this.tbp_AboutUs.Size = new System.Drawing.Size(1029, 760);
            this.tbp_AboutUs.TabIndex = 4;
            this.tbp_AboutUs.Text = "About Us";
            this.tbp_AboutUs.UseVisualStyleBackColor = true;
            // 
            // materialLabel10
            // 
            this.materialLabel10.Depth = 0;
            this.materialLabel10.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel10.Location = new System.Drawing.Point(203, 245);
            this.materialLabel10.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel10.Name = "materialLabel10";
            this.materialLabel10.Size = new System.Drawing.Size(669, 240);
            this.materialLabel10.TabIndex = 6;
            this.materialLabel10.Text = resources.GetString("materialLabel10.Text");
            this.materialLabel10.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // materialLabel8
            // 
            this.materialLabel8.AutoSize = true;
            this.materialLabel8.Depth = 0;
            this.materialLabel8.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel8.Location = new System.Drawing.Point(439, 178);
            this.materialLabel8.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel8.Name = "materialLabel8";
            this.materialLabel8.Size = new System.Drawing.Size(157, 19);
            this.materialLabel8.TabIndex = 4;
            this.materialLabel8.Text = "Copyright 2025 - 2026";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(439, 198);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(0, 16);
            this.label1.TabIndex = 3;
            // 
            // materialLabel7
            // 
            this.materialLabel7.AutoSize = true;
            this.materialLabel7.Depth = 0;
            this.materialLabel7.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel7.Location = new System.Drawing.Point(459, 130);
            this.materialLabel7.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel7.Name = "materialLabel7";
            this.materialLabel7.Size = new System.Drawing.Size(106, 19);
            this.materialLabel7.TabIndex = 2;
            this.materialLabel7.Text = "Version 1.0.0.0";
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
            this.pictureBox1.Location = new System.Drawing.Point(324, 18);
            this.pictureBox1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(368, 66);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 1;
            this.pictureBox1.TabStop = false;
            // 
            // materialDrawer1
            // 
            this.materialDrawer1.AutoHide = false;
            this.materialDrawer1.AutoShow = false;
            this.materialDrawer1.BackgroundWithAccent = false;
            this.materialDrawer1.BaseTabControl = this.mtc_Modules;
            this.materialDrawer1.Depth = 0;
            this.materialDrawer1.Dock = System.Windows.Forms.DockStyle.Left;
            this.materialDrawer1.HighlightWithAccent = true;
            this.materialDrawer1.IndicatorWidth = 0;
            this.materialDrawer1.IsOpen = false;
            this.materialDrawer1.Location = new System.Drawing.Point(3, 64);
            this.materialDrawer1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.materialDrawer1.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialDrawer1.Name = "materialDrawer1";
            this.materialDrawer1.ShowIconsWhenHidden = false;
            this.materialDrawer1.Size = new System.Drawing.Size(77, 789);
            this.materialDrawer1.TabIndex = 3;
            this.materialDrawer1.Text = "materialDrawer1";
            this.materialDrawer1.UseColors = false;
            // 
            // frm_Mainform
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1043, 855);
            this.Controls.Add(this.materialDrawer1);
            this.Controls.Add(this.mtc_Modules);
            this.DrawerTabControl = this.mtc_Modules;
            this.DrawerUseColors = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frm_Mainform";
            this.Padding = new System.Windows.Forms.Padding(3, 64, 3, 2);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "CARE DICOM Enabler";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.frm_Mainform_FormClosed);
            this.Load += new System.EventHandler(this.frm_Mainform_Load);
            this.mtc_Modules.ResumeLayout(false);
            this.tbp_ServerManager.ResumeLayout(false);
            this.tbp_Settings.ResumeLayout(false);
            this.tbp_Settings.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.grpb_ModalitySCP.ResumeLayout(false);
            this.grpb_ModalitySCP.PerformLayout();
            this.tbp_SCUSettings.ResumeLayout(false);
            this.tbp_SCUSettings.PerformLayout();
            this.grpb_StoreSCUSettings.ResumeLayout(false);
            this.grpb_StoreSCUSettings.PerformLayout();
            this.tbp_ServerList.ResumeLayout(false);
            this.tbp_ServerList.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_ServerList)).EndInit();
            this.tdp_ViewPatients.ResumeLayout(false);
            this.tdp_ViewPatients.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgv_PatientList)).EndInit();
            this.tbp_ViewLog.ResumeLayout(false);
            this.tbc_Logs.ResumeLayout(false);
            this.tp_MWLLog.ResumeLayout(false);
            this.tp_SCPLog.ResumeLayout(false);
            this.tp_SCULog.ResumeLayout(false);
            this.tbp_AboutUs.ResumeLayout(false);
            this.tbp_AboutUs.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ImageList imgList_Icons;
        private MaterialSkin.Controls.MaterialTabControl mtc_Modules;
        private System.Windows.Forms.TabPage tbp_ServerManager;
        private System.Windows.Forms.TabPage tbp_Settings;
        private System.Windows.Forms.TabPage tbp_ServerList;
        private System.Windows.Forms.TabPage tbp_ViewLog;
        private System.Windows.Forms.TabPage tbp_AboutUs;
        private MaterialSkin.Controls.MaterialDrawer materialDrawer1;
        private System.Windows.Forms.TabPage tdp_ViewPatients;
        private UserControls.uctrl_ServerManager uctrl_ServerManager1;
        private System.Windows.Forms.GroupBox groupBox1;
        private MaterialSkin.Controls.MaterialLabel materialLabel3;
        private MaterialSkin.Controls.MaterialLabel materialLabel2;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_StorePort;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_StoreHost;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_StoreAETitle;
        private MaterialSkin.Controls.MaterialLabel materialLabel1;
        private System.Windows.Forms.GroupBox grpb_ModalitySCP;
        private MaterialSkin.Controls.MaterialLabel materialLabel4;
        private MaterialSkin.Controls.MaterialLabel materialLabel5;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_ModalityPort;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_ModalityHost;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_ModalityAETitle;
        private MaterialSkin.Controls.MaterialLabel materialLabel6;
        private MaterialSkin.Controls.MaterialButton mbtn_SaveSCPSettings;
        private System.Windows.Forms.DataGridView dgv_ServerList;
        private System.Windows.Forms.DataGridView dgv_PatientList;
        private System.Windows.Forms.PictureBox pictureBox1;
        private MaterialSkin.Controls.MaterialLabel materialLabel7;
        private MaterialSkin.Controls.MaterialLabel materialLabel10;
        private MaterialSkin.Controls.MaterialLabel materialLabel8;
        private System.Windows.Forms.Label label1;
        private MaterialSkin.Controls.MaterialButton mtbtn_AddUpdateServer;
        private MaterialSkin.Controls.MaterialLabel materialLabel12;
        private System.Windows.Forms.TextBox txt_PortNo;
        private System.Windows.Forms.TextBox txt_HostAddress;
        private System.Windows.Forms.TextBox txt_AETitle;
        private System.Windows.Forms.TextBox txt_ServerName;
        private System.Windows.Forms.RichTextBox rtb_Description;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TabControl tbc_Logs;
        private System.Windows.Forms.TabPage tp_MWLLog;
        private System.Windows.Forms.RichTextBox rtb_MWLLog;
        private System.Windows.Forms.TabPage tp_SCPLog;
        private System.Windows.Forms.TabPage tp_SCULog;
        private System.Windows.Forms.RichTextBox rtb_SCULog;
        private System.Windows.Forms.RichTextBox rtb_SCPLog;
        private MaterialSkin.Controls.MaterialCheckbox mtchkb_CheckServer;
        private System.Windows.Forms.DataGridViewTextBoxColumn pk;
        private System.Windows.Forms.DataGridViewTextBoxColumn servername;
        private System.Windows.Forms.DataGridViewTextBoxColumn serverAETitle;
        private System.Windows.Forms.DataGridViewTextBoxColumn serverHost;
        private System.Windows.Forms.DataGridViewTextBoxColumn serverPort;
        private System.Windows.Forms.DataGridViewTextBoxColumn description;
        private System.Windows.Forms.DataGridViewImageColumn delete;
        private System.Windows.Forms.TabPage tbp_SCUSettings;
        private System.Windows.Forms.GroupBox grpb_StoreSCUSettings;
        private MaterialSkin.Controls.MaterialLabel materialLabel9;
        private MaterialSkin.Controls.MaterialLabel materialLabel11;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_StoreSCUPort;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_StoreSCUHost;
        private MaterialSkin.Controls.MaterialTextBox mtxtb_StoreSCUAETitle;
        private MaterialSkin.Controls.MaterialLabel materialLabel13;
        private MaterialSkin.Controls.MaterialButton mbtn_SaveSCUSettings;
        private MaterialSkin.Controls.MaterialTextBox mtb_callingAETitle;
        private MaterialSkin.Controls.MaterialLabel materialLabel14;
        private System.Windows.Forms.DataGridViewTextBoxColumn patientid;
        private System.Windows.Forms.DataGridViewTextBoxColumn patName;
        private System.Windows.Forms.DataGridViewTextBoxColumn accessionNumber;
        private System.Windows.Forms.DataGridViewTextBoxColumn modality;
        private System.Windows.Forms.DataGridViewTextBoxColumn status;
        private System.Windows.Forms.DataGridViewTextBoxColumn noofseries;
        private System.Windows.Forms.DataGridViewTextBoxColumn noofimages;
        private MaterialSkin.Controls.MaterialButton mbtn_PatientRefresh;
    }
}

