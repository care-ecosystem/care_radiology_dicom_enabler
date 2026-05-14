using Plexus.Common.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Plexus.Common;
using Plexus.Common.config;

namespace GenerateConnectionString
{
    public partial class ufrm_GenerateConCstring : Form
    {
        public ufrm_GenerateConCstring()
        {
            InitializeComponent();
        }

        private void mbtn_TestDBConnection_Click(object sender, EventArgs e)
        {
            string errorString = string.Empty;
            ucls_DAL objDal = new ucls_DAL(Path.GetDirectoryName(Application.ExecutablePath));

            if (objDal.openDBConnection(ref errorString))
            {
                MessageBox.Show("Test DB Connection Succesfull !!!");
            }
            else
            {
                MessageBox.Show("Test DB Connection Failed with exception : " + errorString);

            }
        }

        private void mbtn_GenerateConString_Click(object sender, EventArgs e)
        {
            try
            {
                if (mrdb_Encrypt.Checked)
                {
                    mtxt_EncConnString.Text = ucls_EnDcryption.EncryptString(EncKey.encdeKey, mtxt_ConnectionString.Text);
                    if (mtxt_EncConnString.Text != string.Empty)
                    {
                        cls_PlexusConfig.SaveDetailsToXML(Path.GetDirectoryName(Application.ExecutablePath), "/configurations/connectString", mtxt_EncConnString.Text);
                        MessageBox.Show("Encryption and Saving Connection String to config Succesfull !!");
                    }
                    else
                    {
                        MessageBox.Show("Error Encrypting the connection string.!!");
                    }
                }
                else if (mrdb_Decrypt.Checked)
                {
                    mtxt_ConnectionString.Text = ucls_EnDcryption.DecryptString(EncKey.encdeKey, mtxt_EncConnString.Text);
                    if (mtxt_EncConnString.Text != string.Empty)
                    {
                        MessageBox.Show("Decryption Sucessfull!!");
                    }
                    else
                    {
                        MessageBox.Show("Error Decryption the connection string.!!");
                    }
                }
                
            }
            catch (Exception ex)
            {
                MessageBox.Show("Process Failed  : " + ex.Message);
            }
        }

        private void ufrm_GenerateConCstring_Load(object sender, EventArgs e)
        {
            try
            {
                mtxt_EncConnString.Text =  cls_PlexusConfig.ReadDetailsFromXML(Path.GetDirectoryName(Application.ExecutablePath), "/configurations/connectString");
            }
            catch(Exception ex)
            {
                MessageBox.Show("Error Loading Conneciton String from Config" + ex.Message);
            }

        }
    }
}
