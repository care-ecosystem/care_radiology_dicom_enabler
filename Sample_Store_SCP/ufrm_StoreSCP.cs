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
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.Log;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using Sample_Store_SCP.Network;
using Serilog;
using Plexus.Common.Database;
using FellowOakDicom;

namespace Sample_Store_SCP
{
    public partial class ufrm_StoreSCP : Form
    {
        private static IDicomServer _server;
        public static Serilog.ILogger fileLogger = null;
        public ucls_DAL objDAL = null;
        public ufrm_StoreSCP()
        {
            Global._storagePath = @".\DCM";
            InitializeComponent();

            fileLogger = GetFileLogger();
            objDAL = new ucls_DAL(Path.GetDirectoryName(Application.ExecutablePath));
        }


        private Serilog.ILogger GetFileLogger()
        {
            return new LoggerConfiguration().
                WriteTo.File("logs/StoreSCU.txt",
                restrictedToMinimumLevel : Serilog.Events.LogEventLevel.Information,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: false,
                fileSizeLimitBytes: 10240000)
                .CreateLogger();
        }

        private void mtb_StartAndListen_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Directory.Exists(mtxtb_FolderPath.Text))
                {
                    Directory.CreateDirectory(mtxtb_FolderPath.Text);
                }

                Global._storagePath = mtxtb_FolderPath.Text;
                Global._aeTitle = mtxtb_StoreAETitle.Text;

                int port = Convert.ToInt32(mtxtb_StorePort.Text);
                _server = DicomServerFactory.Create<CStoreSCP>(port);

                if (_server !=null )
                {
                    MessageBox.Show("Storage SCP Started Successfully !!!");
                    mtb_StartAndListen.Enabled = false;
                    mtb_Stop.Enabled = true;

                }
                else
                {
                    MessageBox.Show("Error Initializing Storage SCP!!");
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show("Error Starting Storage SCP" + ex.Message);
            }

        }

        private void mtb_Stop_Click(object sender, EventArgs e)
        {
            if (_server != null && _server.IsListening)
            {
                _server.Stop();
                _server.Dispose();
                mtb_StartAndListen.Enabled = true;
                mtb_Stop.Enabled = false;
                MessageBox.Show("Storage SCP Stopped Successfully !!!");
            }
        }

        private void ufrm_StoreSCP_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_server != null && _server.IsListening)
            {
                _server.Stop();
                _server.Dispose();
            }
        }

        private void mtbtn_Push_Click(object sender, EventArgs e)
        {
            try
            { 

            if (mtxtb_PushFolder.Text == string.Empty )
            {
                MessageBox.Show("Folder path cannot be empty !!");
                return;
            }

            if (!Directory.Exists(mtxtb_PushFolder.Text))
            {
                MessageBox.Show("Folder does not exist. Please enter valid folder path !!");
                return;
            }

            string[] dcmfiles = Directory.GetFiles(mtxtb_PushFolder.Text,"*.*",SearchOption.AllDirectories);

            if (dcmfiles.Length <= 0 )
            {
                MessageBox.Show("Folder does not contain any files to push. !!");
                return;
            }
                mtpg_ProgressUpload.Value = 0;
                pgbar_upload.Value = 0;
                mtpg_ProgressUpload.Visible = true;
                pgbar_upload.Visible = true;
                mtpg_ProgressUpload.Maximum = dcmfiles.Length;
                pgbar_upload.Maximum = dcmfiles.Length;
                // Create DCM Client 
                fileLogger.Information($"Creating  Association to AE {mtxt_AETitle1.Text} with IP: {mtxt_HostAddress1.Text}");
                var client = DicomClientFactory.Create(mtxt_HostAddress1.Text, Convert.ToInt32(mtxt_Port1.Text), false, mtb_CallingAETitle.Text, mtxt_AETitle1.Text);
                fileLogger.Information($" Association Successfull to AE {mtxt_AETitle1.Text} with IP: {mtxt_HostAddress1.Text}");
                // Send DIcom FIles
                foreach (string dcmfile in dcmfiles)
                {
                    if (string.IsNullOrWhiteSpace(dcmfile))
                    {
                        continue;
                    }
                    DicomSCUFile(client,dcmfile);
                }

            }
            catch ( Exception ex )
            {
                MessageBox.Show("Error Pushing images via DICOM SCU" + ex.Message);
            }

        }

        private async void DicomSCUFile(IDicomClient client, string dcmfile)
        {
            string studyInstanceId = string.Empty;
            try
            {
                fileLogger.Information($" Create C-Store Request to AE {mtxt_AETitle1.Text} with IP: {mtxt_HostAddress1.Text} for file : {dcmfile}");
                var request = new DicomCStoreRequest(dcmfile);
                request.OnResponseReceived += (req, response) => fileLogger.Information("C-Store Response Received, Status: " + response.Status + "for file : "+dcmfile);
                await client.AddRequestAsync(request);
                GetStudyDetails(ref studyInstanceId, dcmfile);
                fileLogger.Information($"C-Store Request Added  for file {dcmfile}");
                await client.SendAsync();

                UpdateStudyStatusDB(3,studyInstanceId, dcmfile);

                fileLogger.Information($"C-Store send initiated for file {dcmfile}");

                

                mtpg_ProgressUpload.PerformStep();
                pgbar_upload.PerformStep();
                if (mtpg_ProgressUpload.Value == mtpg_ProgressUpload.Maximum)
                {
                    System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.Default;
                    MessageBox.Show("Files Uploaded Successfully !!!");
                }
            }
            catch(Exception ex)
            {
                //fileLogger.Information($"Sending file {dcmfile} failed with exception : {ex.Message}" );
                MessageBox.Show($"Sending file {dcmfile} failed with exception : {ex.InnerException.Message}");
                UpdateStudyStatusDB(-10, studyInstanceId, dcmfile);
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="studyinstanceId"></param>
        /// <param name="dcmfile"></param>
        private void GetStudyDetails(ref string studyinstanceId, string dcmfile)
        {
            try
            {
                DicomDataset dicomDataSet = DicomFile.Open(dcmfile).Dataset;

                if (dicomDataSet != null)
                {
                    studyinstanceId = dicomDataSet.GetString(DicomTag.StudyInstanceUID);
                }
                else
                {
                    MessageBox.Show($"Getting study details failed for the file {dcmfile}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Getting Study details for the file {dcmfile} failed with exception : {ex.InnerException.Message}");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="studyInstanceId"></param>
        /// <param name="dcmfile"></param>
        private void UpdateStudyStatusDB(int studySatus, string studyInstanceId, string dcmfile)
        {
            string errorString = string.Empty;
            try
            {
                objDAL.UpdateStudyStatus(studyInstanceId, studySatus, ref errorString);
                if (errorString != string.Empty)
                {
                    MessageBox.Show($"Updating to Database failed for the file {dcmfile} with exception  : {errorString}");
                }
                else
                {
                    MessageBox.Show($"Updating to Database successfull for the file {dcmfile}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sending file {dcmfile} failed with exception : {ex.InnerException.Message}");
            }
        }

        //private void ReadStudyDetailsAndUpdateDB(string dcmfile)
        //{
        //    string studyInstanceId = string.Empty;
        //    string errorString = string.Empty;
        //    try
        //    {
        //        DicomDataset dicomDataSet = DicomFile.Open(dcmfile).Dataset;

        //        if (dicomDataSet != null)
        //        {
        //            studyInstanceId = dicomDataSet.GetString(DicomTag.StudyInstanceUID);
        //            objDAL.UpdateStudyStatus(studyInstanceId, 3, ref errorString);

        //            if (errorString != string.Empty)
        //            {
        //                MessageBox.Show($"Updating to Database failed for the file {dcmfile} with exception  : {errorString}");
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Sending file {dcmfile} failed with exception : {ex.InnerException.Message}");
        //    }
        //}
    }
}
