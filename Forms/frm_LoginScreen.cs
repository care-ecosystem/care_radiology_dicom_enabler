using MaterialSkin;
using MaterialSkin.Controls;
using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Windows.Forms;
using Plexus.Common;
using Plexus.Common.Database;
using Plexus.Common.config;

namespace Plexus_DICOM_Enabler.Forms
{
    public partial class frm_LoginScreen : MaterialForm
    {
        public frm_LoginScreen()
        {
            InitializeComponent();
            var materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);
            materialSkinManager.Theme = MaterialSkinManager.Themes.LIGHT;
            materialSkinManager.ColorScheme = new ColorScheme(Primary.Blue700, Primary.Blue900, Primary.Blue500, Accent.Green400, TextShade.WHITE);
        }

        /// <summary>
        /// Even to authorize and authenticate the application
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_Login_Click(object sender, EventArgs e)
        {
            string errorString = string.Empty;
            if ( CheckValidUser(ref errorString)) 
            {
                if ( mtchkb_RememberMe.Checked )
                {
                    SaveUserDetails();
                }
                
                this.Hide();
                //Global.deployType = Convert.ToInt32(ConfigurationManager.AppSettings["deployType"].ToString());
                Global.deployType = Convert.ToInt32(cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/deployType"));
                frm_Mainform frm_Mainform = new frm_Mainform();
                frm_Mainform.ShowDialog();
            }
            else
            {
                if ( errorString != string.Empty )
                {
                    MessageBox.Show("Authentication failed : " + errorString);
                    return;
                }

                MessageBox.Show("Invalid Username or Password.");
            }

        }

        /// <summary>
        /// Save User Details to Configuration
        /// </summary>
        private void SaveUserDetails()
        {
            try
            {
                if (mtchkb_RememberMe.Checked)
                {
                    cls_PlexusConfig.SaveDetailsToXML(Global._applicationPath,@"/configurations/uname", ucls_EnDcryption.EncryptString(EncKey.encdeKey,mtb_Username.Text));
                    cls_PlexusConfig.SaveDetailsToXML(Global._applicationPath, @"/configurations/pwd", ucls_EnDcryption.EncryptString(EncKey.encdeKey, mtxtb_Password.Text));
                    cls_PlexusConfig.SaveDetailsToXML(Global._applicationPath, @"/configurations/spwd", mtchkb_RememberMe.Checked.ToString());
                }
                else
                {
                    cls_PlexusConfig.SaveDetailsToXML(Global._applicationPath, @"/configurations/uname", string.Empty);
                    cls_PlexusConfig.SaveDetailsToXML(Global._applicationPath, @"/configurations/pwd", string.Empty);
                    cls_PlexusConfig.SaveDetailsToXML(Global._applicationPath, @"/configurations/spwd", "False");
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show("Error saving details to configuration " + ex.Message);
            }
        }



        /// <summary>
        /// Check Valid User
        /// </summary>
        /// <param name="errorString"></param>
        /// <returns></returns>
        private bool CheckValidUser(ref string errorString)
        {
            //string authURL = ConfigurationManager.AppSettings["authURL"].ToString();
            //this.Cursor = System.Windows.Forms.Cursors.WaitCursor;

            //try
            //{
            //    string device = ConfigurationManager.AppSettings["deviceName"].ToString();
            //    WebRequest authReq = WebRequest.Create(authURL);
            //    authReq.Method = "POST";
            //    authReq.ContentType = "application/json";
            //    //    {
            //    //       "device": "ARaja",
            //    //       "name": "alagaraja",
            //    //       "password": "Plexus@123"
            //    //     }
            //    string postData = "{\"device\":\""+ device + "\",\"name\":\"" + mtb_Username.Text + "\",\"password\":\"" + mtxtb_Password.Text + "\"}";

            //    using ( var streamWriter =  new StreamWriter(authReq.GetRequestStream()))
            //    {
            //        streamWriter.Write(postData);
            //        streamWriter.Flush();
            //        streamWriter.Close();

            //        var httpResponse = (HttpWebResponse) authReq.GetResponse();

            //        using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            //        {
            //            var resultData = streamReader.ReadToEnd();
            //            //var serializer = new JavaScriptSerializer();
            //            cls_UserDetail userDetails = (cls_UserDetail)JsonSerializer.Deserialize(resultData, typeof(cls_UserDetail));
            //            if (userDetails !=null )
            //            {
            //                if ( userDetails.status == "A" )
            //                {
            //                    return true;
            //                }
            //            }
            //        }
            //    }

            //    return true;
            //}
            //catch ( Exception ex)
            //{
            //    errorString = ex.Message;
            //    return false;
            //}
            //finally
            //{
            //    this.Cursor = System.Windows.Forms.Cursors.Default;
            //}
            return true;
        }

        /// <summary>
        /// Event to cancel the login and close the application
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_Cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void frm_LoginScreen_Load(object sender, EventArgs e)
        {
            //mtb_Login.Size = mtxtb_Password.Size;
            try
            { 
                 LoadUserDetailsFromConfig();
            }
            catch(Exception ex)
            {
                MessageBox.Show("Error loading details from configuration " + ex.Message); 
            }
        }

        /// <summary>
        /// Log user Details from Configration files
        /// </summary>
        private void LoadUserDetailsFromConfig()
        {

            // Check if the Password is stored in Config

            mtchkb_RememberMe.Checked = Convert.ToBoolean(cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/spwd"));

            if (mtchkb_RememberMe.Checked)
            {
                // Get Username from Configuration File 
                if (cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/uname") != string.Empty)
                {
                    mtb_Username.Text = ucls_EnDcryption.DecryptString(EncKey.encdeKey, cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/uname"));
                }
                else
                {
                    mtb_Username.Text = string.Empty;
                }
                // Get Password from Configuration File
                if (cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/pwd") != string.Empty)
                {
                    mtxtb_Password.Text = ucls_EnDcryption.DecryptString(EncKey.encdeKey, cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/pwd"));
                }
                else
                {
                    mtxtb_Password.Text = string.Empty;
                }
            }

        }
    }
}
