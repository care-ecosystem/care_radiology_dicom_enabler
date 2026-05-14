
namespace Plexus_DICOM_Enabler.Forms
{
    partial class frm_LoginScreen
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frm_LoginScreen));
            this.pnl_background = new System.Windows.Forms.Panel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.mtchkb_RememberMe = new MaterialSkin.Controls.MaterialCheckbox();
            this.mtb_Login = new MaterialSkin.Controls.MaterialButton();
            this.mtxtb_Password = new MaterialSkin.Controls.MaterialTextBox2();
            this.mtb_Username = new MaterialSkin.Controls.MaterialTextBox2();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.pnl_background.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // pnl_background
            // 
            this.pnl_background.BackColor = System.Drawing.Color.White;
            this.pnl_background.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pnl_background.BackgroundImage")));
            this.pnl_background.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.pnl_background.Controls.Add(this.panel1);
            this.pnl_background.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnl_background.Location = new System.Drawing.Point(2, 58);
            this.pnl_background.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.pnl_background.Name = "pnl_background";
            this.pnl_background.Size = new System.Drawing.Size(697, 468);
            this.pnl_background.TabIndex = 0;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.mtchkb_RememberMe);
            this.panel1.Controls.Add(this.mtb_Login);
            this.panel1.Controls.Add(this.mtxtb_Password);
            this.panel1.Controls.Add(this.mtb_Username);
            this.panel1.Controls.Add(this.pictureBox1);
            this.panel1.Location = new System.Drawing.Point(191, 87);
            this.panel1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(315, 258);
            this.panel1.TabIndex = 1;
            // 
            // mtchkb_RememberMe
            // 
            this.mtchkb_RememberMe.AutoSize = true;
            this.mtchkb_RememberMe.Depth = 0;
            this.mtchkb_RememberMe.Location = new System.Drawing.Point(27, 184);
            this.mtchkb_RememberMe.Margin = new System.Windows.Forms.Padding(0);
            this.mtchkb_RememberMe.MouseLocation = new System.Drawing.Point(-1, -1);
            this.mtchkb_RememberMe.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtchkb_RememberMe.Name = "mtchkb_RememberMe";
            this.mtchkb_RememberMe.ReadOnly = false;
            this.mtchkb_RememberMe.Ripple = true;
            this.mtchkb_RememberMe.Size = new System.Drawing.Size(137, 37);
            this.mtchkb_RememberMe.TabIndex = 4;
            this.mtchkb_RememberMe.Text = "Remember Me";
            this.mtchkb_RememberMe.UseVisualStyleBackColor = true;
            // 
            // mtb_Login
            // 
            this.mtb_Login.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mtb_Login.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mtb_Login.Depth = 0;
            this.mtb_Login.HighEmphasis = true;
            this.mtb_Login.Icon = null;
            this.mtb_Login.Location = new System.Drawing.Point(221, 209);
            this.mtb_Login.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.mtb_Login.MouseState = MaterialSkin.MouseState.HOVER;
            this.mtb_Login.Name = "mtb_Login";
            this.mtb_Login.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mtb_Login.Size = new System.Drawing.Size(64, 36);
            this.mtb_Login.TabIndex = 3;
            this.mtb_Login.Text = "Login";
            this.mtb_Login.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mtb_Login.UseAccentColor = false;
            this.mtb_Login.UseVisualStyleBackColor = true;
            this.mtb_Login.Click += new System.EventHandler(this.btn_Login_Click);
            // 
            // mtxtb_Password
            // 
            this.mtxtb_Password.AnimateReadOnly = false;
            this.mtxtb_Password.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.mtxtb_Password.CharacterCasing = System.Windows.Forms.CharacterCasing.Normal;
            this.mtxtb_Password.Depth = 0;
            this.mtxtb_Password.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.mtxtb_Password.HideSelection = true;
            this.mtxtb_Password.LeadingIcon = null;
            this.mtxtb_Password.Location = new System.Drawing.Point(27, 128);
            this.mtxtb_Password.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.mtxtb_Password.MaxLength = 32767;
            this.mtxtb_Password.MouseState = MaterialSkin.MouseState.OUT;
            this.mtxtb_Password.Name = "mtxtb_Password";
            this.mtxtb_Password.PasswordChar = '*';
            this.mtxtb_Password.PrefixSuffixText = null;
            this.mtxtb_Password.ReadOnly = false;
            this.mtxtb_Password.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.mtxtb_Password.SelectedText = "";
            this.mtxtb_Password.SelectionLength = 0;
            this.mtxtb_Password.SelectionStart = 0;
            this.mtxtb_Password.ShortcutsEnabled = true;
            this.mtxtb_Password.Size = new System.Drawing.Size(242, 48);
            this.mtxtb_Password.TabIndex = 2;
            this.mtxtb_Password.TabStop = false;
            this.mtxtb_Password.Text = "Plexus@123";
            this.mtxtb_Password.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            this.mtxtb_Password.TrailingIcon = ((System.Drawing.Image)(resources.GetObject("mtxtb_Password.TrailingIcon")));
            this.mtxtb_Password.UseSystemPasswordChar = false;
            // 
            // mtb_Username
            // 
            this.mtb_Username.AnimateReadOnly = false;
            this.mtb_Username.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.mtb_Username.CharacterCasing = System.Windows.Forms.CharacterCasing.Normal;
            this.mtb_Username.Depth = 0;
            this.mtb_Username.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.mtb_Username.HideSelection = true;
            this.mtb_Username.LeadingIcon = null;
            this.mtb_Username.Location = new System.Drawing.Point(27, 72);
            this.mtb_Username.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.mtb_Username.MaxLength = 32767;
            this.mtb_Username.MouseState = MaterialSkin.MouseState.OUT;
            this.mtb_Username.Name = "mtb_Username";
            this.mtb_Username.PasswordChar = '\0';
            this.mtb_Username.PrefixSuffixText = null;
            this.mtb_Username.ReadOnly = false;
            this.mtb_Username.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.mtb_Username.SelectedText = "";
            this.mtb_Username.SelectionLength = 0;
            this.mtb_Username.SelectionStart = 0;
            this.mtb_Username.ShortcutsEnabled = true;
            this.mtb_Username.Size = new System.Drawing.Size(242, 48);
            this.mtb_Username.TabIndex = 1;
            this.mtb_Username.TabStop = false;
            this.mtb_Username.Text = "alagaraja";
            this.mtb_Username.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            this.mtb_Username.TrailingIcon = ((System.Drawing.Image)(resources.GetObject("mtb_Username.TrailingIcon")));
            this.mtb_Username.UseSystemPasswordChar = false;
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
            this.pictureBox1.Location = new System.Drawing.Point(27, 11);
            this.pictureBox1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(242, 36);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // frm_LoginScreen
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(701, 528);
            this.Controls.Add(this.pnl_background);
            this.FormStyle = MaterialSkin.Controls.MaterialForm.FormStyles.ActionBar_48;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frm_LoginScreen";
            this.Padding = new System.Windows.Forms.Padding(2, 58, 2, 2);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "                  ";
            this.Load += new System.EventHandler(this.frm_LoginScreen_Load);
            this.pnl_background.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnl_background;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.PictureBox pictureBox1;
        private MaterialSkin.Controls.MaterialButton mtb_Login;
        private MaterialSkin.Controls.MaterialTextBox2 mtxtb_Password;
        private MaterialSkin.Controls.MaterialTextBox2 mtb_Username;
        private MaterialSkin.Controls.MaterialCheckbox mtchkb_RememberMe;
    }
}