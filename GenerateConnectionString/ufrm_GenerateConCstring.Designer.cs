
namespace GenerateConnectionString
{
    partial class ufrm_GenerateConCstring
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
            this.mtxt_ConnectionString = new MaterialSkin.Controls.MaterialTextBox();
            this.materialLabel1 = new MaterialSkin.Controls.MaterialLabel();
            this.materialLabel2 = new MaterialSkin.Controls.MaterialLabel();
            this.mtxt_EncConnString = new MaterialSkin.Controls.MaterialTextBox();
            this.mbtn_GenerateConString = new MaterialSkin.Controls.MaterialButton();
            this.mbtn_TestDBConnection = new MaterialSkin.Controls.MaterialButton();
            this.mrdb_Encrypt = new MaterialSkin.Controls.MaterialRadioButton();
            this.mrdb_Decrypt = new MaterialSkin.Controls.MaterialRadioButton();
            this.SuspendLayout();
            // 
            // mtxt_ConnectionString
            // 
            this.mtxt_ConnectionString.AnimateReadOnly = false;
            this.mtxt_ConnectionString.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxt_ConnectionString.Depth = 0;
            this.mtxt_ConnectionString.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.6F);
            this.mtxt_ConnectionString.LeadingIcon = null;
            this.mtxt_ConnectionString.Location = new System.Drawing.Point(12, 74);
            this.mtxt_ConnectionString.MaxLength = 50;
            this.mtxt_ConnectionString.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxt_ConnectionString.Multiline = false;
            this.mtxt_ConnectionString.Name = "mtxt_ConnectionString";
            this.mtxt_ConnectionString.Size = new System.Drawing.Size(763, 50);
            this.mtxt_ConnectionString.TabIndex = 0;
            this.mtxt_ConnectionString.Text = "Server=localhost;Database=plexus_mi2;Uid=root;Pwd=inzin@123;";
            this.mtxt_ConnectionString.TrailingIcon = null;
            // 
            // materialLabel1
            // 
            this.materialLabel1.AutoSize = true;
            this.materialLabel1.Depth = 0;
            this.materialLabel1.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel1.Location = new System.Drawing.Point(12, 37);
            this.materialLabel1.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel1.Name = "materialLabel1";
            this.materialLabel1.Size = new System.Drawing.Size(127, 19);
            this.materialLabel1.TabIndex = 1;
            this.materialLabel1.Text = "Connection String";
            // 
            // materialLabel2
            // 
            this.materialLabel2.AutoSize = true;
            this.materialLabel2.Depth = 0;
            this.materialLabel2.Font = new System.Drawing.Font("Roboto", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.materialLabel2.Location = new System.Drawing.Point(12, 151);
            this.materialLabel2.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel2.Name = "materialLabel2";
            this.materialLabel2.Size = new System.Drawing.Size(201, 19);
            this.materialLabel2.TabIndex = 3;
            this.materialLabel2.Text = "Encrypted Connection String";
            // 
            // mtxt_EncConnString
            // 
            this.mtxt_EncConnString.AnimateReadOnly = false;
            this.mtxt_EncConnString.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.mtxt_EncConnString.Depth = 0;
            this.mtxt_EncConnString.Font = new System.Drawing.Font("Roboto", 16F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.mtxt_EncConnString.LeadingIcon = null;
            this.mtxt_EncConnString.Location = new System.Drawing.Point(12, 188);
            this.mtxt_EncConnString.MaxLength = 50;
            this.mtxt_EncConnString.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxt_EncConnString.Multiline = false;
            this.mtxt_EncConnString.Name = "mtxt_EncConnString";
            this.mtxt_EncConnString.Size = new System.Drawing.Size(763, 50);
            this.mtxt_EncConnString.TabIndex = 2;
            this.mtxt_EncConnString.Text = "BE8vCF6jUAHpbAzVt7U9nJSLwTpFLiP44X4K0ph+Q775B25VH8YTqz66Es7o9De5i6JV+VjW6TYvViBmq" +
    "luPdQ==";
            this.mtxt_EncConnString.TrailingIcon = null;
            // 
            // mbtn_GenerateConString
            // 
            this.mbtn_GenerateConString.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mbtn_GenerateConString.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mbtn_GenerateConString.Depth = 0;
            this.mbtn_GenerateConString.HighEmphasis = true;
            this.mbtn_GenerateConString.Icon = null;
            this.mbtn_GenerateConString.Location = new System.Drawing.Point(512, 352);
            this.mbtn_GenerateConString.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mbtn_GenerateConString.MouseState = MaterialSkin.MouseState.HOVER;
            this.mbtn_GenerateConString.Name = "mbtn_GenerateConString";
            this.mbtn_GenerateConString.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mbtn_GenerateConString.Size = new System.Drawing.Size(86, 36);
            this.mbtn_GenerateConString.TabIndex = 4;
            this.mbtn_GenerateConString.Text = "Process";
            this.mbtn_GenerateConString.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mbtn_GenerateConString.UseAccentColor = false;
            this.mbtn_GenerateConString.UseVisualStyleBackColor = true;
            this.mbtn_GenerateConString.Click += new System.EventHandler(this.mbtn_GenerateConString_Click);
            // 
            // mbtn_TestDBConnection
            // 
            this.mbtn_TestDBConnection.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mbtn_TestDBConnection.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mbtn_TestDBConnection.Depth = 0;
            this.mbtn_TestDBConnection.HighEmphasis = true;
            this.mbtn_TestDBConnection.Icon = null;
            this.mbtn_TestDBConnection.Location = new System.Drawing.Point(38, 352);
            this.mbtn_TestDBConnection.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mbtn_TestDBConnection.MouseState = MaterialSkin.MouseState.HOVER;
            this.mbtn_TestDBConnection.Name = "mbtn_TestDBConnection";
            this.mbtn_TestDBConnection.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mbtn_TestDBConnection.Size = new System.Drawing.Size(175, 36);
            this.mbtn_TestDBConnection.TabIndex = 5;
            this.mbtn_TestDBConnection.Text = "Test DB Conneciton";
            this.mbtn_TestDBConnection.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mbtn_TestDBConnection.UseAccentColor = false;
            this.mbtn_TestDBConnection.UseVisualStyleBackColor = true;
            this.mbtn_TestDBConnection.Click += new System.EventHandler(this.mbtn_TestDBConnection_Click);
            // 
            // mrdb_Encrypt
            // 
            this.mrdb_Encrypt.AutoSize = true;
            this.mrdb_Encrypt.Checked = true;
            this.mrdb_Encrypt.Depth = 0;
            this.mrdb_Encrypt.Location = new System.Drawing.Point(99, 277);
            this.mrdb_Encrypt.Margin = new System.Windows.Forms.Padding(0);
            this.mrdb_Encrypt.MouseLocation = new System.Drawing.Point(-1, -1);
            this.mrdb_Encrypt.MouseState = MaterialSkin.MouseState.HOVER;
            this.mrdb_Encrypt.Name = "mrdb_Encrypt";
            this.mrdb_Encrypt.Ripple = true;
            this.mrdb_Encrypt.Size = new System.Drawing.Size(88, 37);
            this.mrdb_Encrypt.TabIndex = 6;
            this.mrdb_Encrypt.TabStop = true;
            this.mrdb_Encrypt.Text = "Encrypt";
            this.mrdb_Encrypt.UseVisualStyleBackColor = true;
            // 
            // mrdb_Decrypt
            // 
            this.mrdb_Decrypt.AutoSize = true;
            this.mrdb_Decrypt.Depth = 0;
            this.mrdb_Decrypt.Location = new System.Drawing.Point(392, 277);
            this.mrdb_Decrypt.Margin = new System.Windows.Forms.Padding(0);
            this.mrdb_Decrypt.MouseLocation = new System.Drawing.Point(-1, -1);
            this.mrdb_Decrypt.MouseState = MaterialSkin.MouseState.HOVER;
            this.mrdb_Decrypt.Name = "mrdb_Decrypt";
            this.mrdb_Decrypt.Ripple = true;
            this.mrdb_Decrypt.Size = new System.Drawing.Size(89, 37);
            this.mrdb_Decrypt.TabIndex = 7;
            this.mrdb_Decrypt.TabStop = true;
            this.mrdb_Decrypt.Text = "Decrypt";
            this.mrdb_Decrypt.UseVisualStyleBackColor = true;
            // 
            // ufrm_GenerateConCstring
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(787, 419);
            this.Controls.Add(this.mrdb_Decrypt);
            this.Controls.Add(this.mrdb_Encrypt);
            this.Controls.Add(this.mbtn_TestDBConnection);
            this.Controls.Add(this.mbtn_GenerateConString);
            this.Controls.Add(this.materialLabel2);
            this.Controls.Add(this.mtxt_EncConnString);
            this.Controls.Add(this.materialLabel1);
            this.Controls.Add(this.mtxt_ConnectionString);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ufrm_GenerateConCstring";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Generate Connection String";
            this.Load += new System.EventHandler(this.ufrm_GenerateConCstring_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private MaterialSkin.Controls.MaterialTextBox mtxt_ConnectionString;
        private MaterialSkin.Controls.MaterialLabel materialLabel1;
        private MaterialSkin.Controls.MaterialLabel materialLabel2;
        private MaterialSkin.Controls.MaterialTextBox mtxt_EncConnString;
        private MaterialSkin.Controls.MaterialButton mbtn_GenerateConString;
        private MaterialSkin.Controls.MaterialButton mbtn_TestDBConnection;
        private MaterialSkin.Controls.MaterialRadioButton mrdb_Encrypt;
        private MaterialSkin.Controls.MaterialRadioButton mrdb_Decrypt;
    }
}

