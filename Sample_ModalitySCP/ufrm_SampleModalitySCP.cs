using System;
using System.Configuration;
using System.IO;
using System.Windows.Forms;
using FellowOakDicom;
using FellowOakDicom.Log;
using Plexus.Common.Database;
using Worklist_SCP;

namespace Sample_ModalitySCP
{
    public partial class ufrm_SampleModalitySCP : Form
    {
        public ufrm_SampleModalitySCP()
        {
            InitializeComponent();
        }

        private void mtb_StartAndListen_Click(object sender, EventArgs e)
        {
            try
            {
                new DicomSetupBuilder()
                       .RegisterServices(s => s.AddFellowOakDicom().AddLogManager<ConsoleLogManager>())
                       .Build();

                if (mtxtb_ModalityAETitle.Text == string.Empty || mtxtb_ModalityHost.Text == string.Empty || mtxtb_ModalityPort.Text == string.Empty)
                {
                    MessageBox.Show("Please fill in the mandatory fields");
                    return;
                }

                WorklistServer.Start(Convert.ToInt32(mtxtb_ModalityPort.Text), mtxtb_ModalityAETitle.Text, mcb_Backend.SelectedIndex);

                MessageBox.Show("Worklist Server Ready to send data to Modality !!!");

            }
            catch(Exception ex)
            {
                MessageBox.Show("Error Starting Modality SCP" + ex.Message);
            }
        }

        private void mtb_Stop_Click(object sender, EventArgs e)
        {
            try { 
                WorklistServer.Stop();
                MessageBox.Show("Worklist Server Stopped !!!");
            }
            catch(Exception ex)
            {
                MessageBox.Show("Error Stopping Modality SCP" + ex.Message);
            }
        }

        private void ufrm_SampleModalitySCP_Load(object sender, EventArgs e)
        {
            mcb_Backend.SelectedIndex = Convert.ToInt32(ConfigurationManager.AppSettings["backend"].ToString());
        }

        private void mcb_Backend_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetSetting("backend", mcb_Backend.SelectedIndex.ToString());
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        private void SetSetting(string key, string value)
        {
            Configuration configuration =
                ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            configuration.AppSettings.Settings[key].Value = value;
            configuration.Save(ConfigurationSaveMode.Full, true);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void materialButton1_Click(object sender, EventArgs e)
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
    }
}
