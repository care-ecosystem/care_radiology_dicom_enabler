using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Test_SeriLog.logs;

namespace Test_SeriLog
{
 
    
    public partial class ufrm_MainForm : Form
    {
        System.Timers.Timer timer = new System.Timers.Timer();
        public ufrm_MainForm()
        {
            InitializeComponent();
        }

        private void materialButton1_Click(object sender, EventArgs e)
        {
            try
            {
                timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
                timer.Interval = 1000; //number in milisecinds  
                timer.Enabled = true;
                MessageBox.Show("Timer Started Successfully !!!");
                materialButton1.Enabled = false;
                materialButton2.Enabled = true;
            }
            catch( Exception ex)
            {
                MessageBox.Show("Error Starting Timer with exception : " + ex.Message);
            }
        }

        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            WriteToFile("Writing Log @ " + DateTime.Now);
        }

        private void WriteToFile(string writeLog)
        {
            ucls_ReadWriteLog objWriteLog = new ucls_ReadWriteLog();
            objWriteLog.WriteToLog(writeLog,true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private string ReadAllText(string file)
        {
            using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var textReader = new StreamReader(fileStream))
                return textReader.ReadToEnd();
        }


        private void materialButton2_Click(object sender, EventArgs e)
        {
            timer.Enabled = false;
            materialButton2.Enabled = false;
            materialButton1.Enabled = true;
            MessageBox.Show("Timer Stopped Successfully !!!");
        }

        private void mbtn_Refresh_Click(object sender, EventArgs e)
        {
            LoadRichTextBox();
        }

        private void LoadRichTextBox()
        {
            try
            {
                richTextBox1.Text = ReadAllText("logs/TestSERILOG.txt");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error Loading Data in Richtext Box with expcetion : " + ex.Message);
            }
        }

        private void ufrm_MainForm_Load(object sender, EventArgs e)
        {
            LoadRichTextBox();
        }
    }
}
