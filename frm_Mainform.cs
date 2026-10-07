using MaterialSkin;
using MaterialSkin.Controls;
using Plexus.Common.config;
using Plexus.Common.Database;
using Plexus_DICOM_Enabler.Forms;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Windows.Forms;

namespace Plexus_DICOM_Enabler
{
    public partial class frm_Mainform : MaterialForm
    {
        bool bUpdateServer = false;
        string primarykey = string.Empty;
        ucls_DAL objDAL = null;
        readonly Timer logRefreshTimer = new Timer { Interval = 1000 };
        readonly System.Collections.Generic.Dictionary<string, LogTail> logTails = new System.Collections.Generic.Dictionary<string, LogTail>
        {
            { "ModalitySCP", new LogTail() },
            { "StoreSCP", new LogTail() },
            { "StoreSCU", new LogTail() }
        };
        public frm_Mainform()
        {
            InitializeComponent();
            logRefreshTimer.Tick += logRefreshTimer_Tick;

            // Clicking the icon at the end of the box opens the calendar or folder browser
            mtxtb_CareFromDate.TrailingIcon = CreateCalendarIcon();
            mtxtb_ScpFolder.TrailingIcon = CreateFolderIcon();
            mtxtb_FailedScpFolder.TrailingIcon = CreateFolderIcon();

            var materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);
            materialSkinManager.Theme = MaterialSkinManager.Themes.LIGHT;
            //materialSkinManager.ColorScheme = new ColorScheme(Primary.LightBlue400, Primary.LightBlue500, Primary.LightBlue200, Accent.LightBlue200, TextShade.BLACK);
            materialSkinManager.ColorScheme = new ColorScheme(
                ColorTranslator.FromHtml("#046c4e"),
                ColorTranslator.FromHtml("#024d38"),
                ColorTranslator.FromHtml("#05956b"),
                ColorTranslator.FromHtml("#00e5a0"),
                TextShade.WHITE);
            //MetroColor = MetroColorStyle.Blue;

            objDAL = new ucls_DAL(Global._applicationPath);
        }

        private void mbtn_SaveSCPSettings_Click(object sender, EventArgs e)
        {
            try { 
                if (string.IsNullOrEmpty(mtxtb_ModalityAETitle.Text) || string.IsNullOrEmpty(mtxtb_ModalityHost.Text) || 
                    string.IsNullOrEmpty(mtxtb_ModalityPort.Text) || string.IsNullOrEmpty(mtxtb_StoreAETitle.Text) || 
                    string.IsNullOrEmpty(mtxtb_StoreHost.Text) || string.IsNullOrEmpty(mtxtb_StorePort.Text) )
                {
                    MessageBox.Show(this, "Please fill mandatory fields. All Fields are mandatory",
                                      "Error Saving Configuration", MessageBoxButtons.OK,
                                      MessageBoxIcon.Error);
                    return;
                }
                else
                {
                    // Save MWL Settings
                    SetSetting("mwlaetitle", mtxtb_ModalityAETitle.Text);
                    SetSetting("mwlhost", mtxtb_ModalityHost.Text);
                    SetSetting("mwlport", mtxtb_ModalityPort.Text);

                    // Save StorageSCP Settings
                    SetSetting("sscpaetitle", mtxtb_StoreAETitle.Text);
                    SetSetting("sscphost", mtxtb_StoreHost.Text);
                    SetSetting("sscpport", mtxtb_StorePort.Text);

                    MessageBox.Show(this, "SCP Settings saved Successfully!! For the settings to take effect,please restart the services from Server Manager",
                                     "Saving Configuration Successfull", MessageBoxButtons.OK,
                                     MessageBoxIcon.Information);
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(this, "Error Saving Server Configuration with expection : " + ex.Message,
                                     "Error Saving Configuration", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
            }
        }


        /// <summary>
        /// Get SCP Settings from App Configuration file
        /// </summary>
        private void GetSCPSettings()
        {
            try
            {
                // Load Modality SCP Settings
                mtxtb_ModalityAETitle.Text = cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/mwlaetitle"); // ConfigurationManager.AppSettings["mwlaetitle"].ToString();
                mtxtb_ModalityHost.Text = cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/mwlhost");  // ConfigurationManager.AppSettings["mwlhost"].ToString();
                mtxtb_ModalityPort.Text = cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/mwlport");  // ConfigurationManager.AppSettings["mwlport"].ToString();

                // Storage SCP Settings
                mtxtb_StoreAETitle.Text = cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/sscpaetitle");  // ConfigurationManager.AppSettings["sscpaetitle"].ToString();
                mtxtb_StoreHost.Text = cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/sscphost");  // ConfigurationManager.AppSettings["sscphost"].ToString();
                mtxtb_StorePort.Text = cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/sscpport");  // ConfigurationManager.AppSettings["sscpport"].ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error loading SCP Settings" + ex.Message,
                                     "Error loading SCP Settings", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
            }
        }


        /// <summary>
        /// Get SCP Settings from App Configuration file
        /// </summary>
        private void GetSCUSettings()
        {
            try
            {
                // Storage SCU Settings
                mtxtb_StoreSCUAETitle.Text = cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/sscuaetitle");  
                mtxtb_StoreSCUHost.Text = cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/sscuhost");  
                mtxtb_StoreSCUPort.Text = cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/sscuport");  
                mtb_callingAETitle.Text = cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/callingaetitle");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error loading SCP Settings" + ex.Message,
                                     "Error loading SCP Settings", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        private void SetSetting(string key, string value)
        {
            cls_PlexusConfig.SaveDetailsToXML(Global._applicationPath, @"/configurations/" + key, value);
        }

        private void mtc_Modules_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                switch(mtc_Modules.SelectedIndex)
                {
                    case 0:
                        //MessageBox.Show(mtc_Modules.SelectedIndex.ToString());
                        break;
                    case 1: // Get SCP Settings
                        GetSCPSettings();
                        break;
                    case 2: // Server List Tab Clicked
                        GetSCUSettings();
                        break;
                    case 3:
                        GetServeListing();
                        break;
                    case 4: // Configuration Tab Clicked
                        GetConfiguration();
                        break;
                    case 5:
                        GetPatientDetails();
                        break;
                    case 6: // View Logs Clicked
                        GetAndPopulateLogs();
                        //MessageBox.Show(mtc_Modules.SelectedIndex.ToString());
                        break;
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(this, "Error loading data" + ex.Message,
                                     "Error loading data", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
            }
        }

        private void GetPatientDetails()
        {
            try
            {
                this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
                string errorString = string.Empty;
             

                // Get Patient List from Database.

                DataSet dsResult = objDAL.LoadPatientList(ref errorString);

                if (dsResult == null)
                {
                    MessageBox.Show(this, "Error loading Patient List : " + errorString,
                                     "Error loading PatientList", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
                    return;
                }
                else
                {
                    if (dsResult.Tables[0].Rows.Count > 0)
                        dgv_PatientList.DataSource = dsResult.Tables[0];
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error loading Patient List" + ex.Message,
                                     "Error loading Patient List", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = System.Windows.Forms.Cursors.Default;
            }
        }

        private void GetAndPopulateLogs()
        {
            try
            {
                // Full reload whenever the View Logs tab is opened, then tail every second
                foreach (var tail in logTails.Values)
                    tail.Reset();

                RefreshLogs();
                logRefreshTimer.Start();
            }
            catch(Exception ex)
            {
                MessageBox.Show(this, "Error Get and Populate Logs" + ex.Message,
                                     "Error Get and Populate Logs", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
            }
        }

        private void logRefreshTimer_Tick(object sender, EventArgs e)
        {
            if (mtc_Modules.SelectedIndex != 6)
            {
                logRefreshTimer.Stop();
                return;
            }

            try
            {
                RefreshLogs();
            }
            catch (IOException)
            {
                // Log file is being rolled or written; pick up the changes on the next tick
            }
        }

        private void RefreshLogs()
        {
            // Read and Populate ModalitySCP Logs
            RefreshLog(rtb_MWLLog, "ModalitySCP");

            // Read and Populate StoreSCP Logs
            RefreshLog(rtb_SCPLog, "StoreSCP");

            // Read and Populate StoreSCU Logs
            RefreshLog(rtb_SCULog, "StoreSCU");
        }

        /// <summary>
        /// Shows every part of the log still on disk for the latest day, oldest first. A service rolls to a
        /// new part when one reaches its size limit, and the earlier parts stay until retention zips and
        /// deletes them, so errors written just before a roll are still shown. Only the text written to the
        /// newest part since the last read is appended; any change in the set of parts reloads them all.
        /// </summary>
        private void RefreshLog(RichTextBox logBox, string searchPattern)
        {
            LogTail tail = logTails[searchPattern];
            List<string> logFiles = GetLogFiles(searchPattern, out string message);

            if (logFiles == null)
            {
                if (tail.Message != message)
                {
                    tail.Reset();
                    tail.Message = message;
                    logBox.Clear();
                    AppendColoredText(logBox, message, logBox.ForeColor);
                }
                return;
            }

            using (var latestStream = OpenLogFile(logFiles[logFiles.Count - 1]))
            {
                bool reload = !logFiles.SequenceEqual(tail.Files) || latestStream.Length < tail.Position;
                if (!reload && latestStream.Length == tail.Position)
                    return;

                var newText = new StringBuilder();
                if (reload)
                {
                    tail.Position = 0;
                    tail.PendingLine = string.Empty;
                    tail.EntryColor = Color.Empty;
                    foreach (string earlierFile in logFiles.Take(logFiles.Count - 1))
                        newText.Append(ReadWholeLogPart(earlierFile));
                }

                newText.Append(tail.PendingLine);
                latestStream.Seek(tail.Position, SeekOrigin.Begin);
                using (var textReader = new StreamReader(latestStream))
                {
                    newText.Append(textReader.ReadToEnd());
                    tail.Position = latestStream.Position;
                }

                // Hold back a partly written last line until the rest arrives, so its level can be read
                string text = newText.ToString();
                int lastNewLine = text.LastIndexOf('\n');
                tail.PendingLine = text.Substring(lastNewLine + 1);
                text = text.Substring(0, lastNewLine + 1);

                // Follow new lines only while the view is scrolled to the bottom, so scrolling back to read is
                // not interrupted; following starts again once the view is scrolled back to the bottom
                bool followTail = tail.Files.Count == 0 || IsScrolledToBottom(logBox);
                int firstVisibleLine = (int)SendMessage(logBox.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
                int selectionStart = logBox.SelectionStart;
                int selectionLength = logBox.SelectionLength;

                tail.Files = logFiles;
                tail.Message = null;

                // Appending moves the caret and the view, so drawing is held until the view is put back
                SendMessage(logBox.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
                try
                {
                    if (reload)
                        logBox.Clear();
                    AppendLogLines(logBox, tail, text);

                    if (followTail)
                    {
                        logBox.SelectionStart = logBox.TextLength;
                        logBox.ScrollToCaret();
                    }
                    else
                    {
                        selectionStart = Math.Min(selectionStart, logBox.TextLength);
                        logBox.Select(selectionStart, Math.Min(selectionLength, logBox.TextLength - selectionStart));
                        // Scroll back to the line that was at the top of the view
                        int linesToScroll = firstVisibleLine - (int)SendMessage(logBox.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
                        SendMessage(logBox.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)linesToScroll);
                    }
                }
                finally
                {
                    SendMessage(logBox.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                    logBox.Invalidate();
                }
            }
        }

        private const int WM_SETREDRAW = 0x000B;
        private const int EM_LINESCROLL = 0x00B6;
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        // True when the last line of the log is in view (or the whole log fits in the box)
        private static bool IsScrolledToBottom(RichTextBox logBox)
        {
            int lastVisibleChar = logBox.GetCharIndexFromPosition(new Point(1, logBox.ClientSize.Height - 1));
            // The log ends with a newline, so the last line is empty; the line before it is the last entry
            return logBox.GetLineFromCharIndex(lastVisibleChar) >= logBox.GetLineFromCharIndex(logBox.TextLength) - 1;
        }

        // Allows the service to keep writing, and retention to delete the part, while it is being read
        private static FileStream OpenLogFile(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }

        private static string ReadWholeLogPart(string path)
        {
            try
            {
                using (var textReader = new StreamReader(OpenLogFile(path)))
                {
                    string text = textReader.ReadToEnd();
                    return text.Length == 0 || text.EndsWith("\n") ? text : text + Environment.NewLine;
                }
            }
            catch (FileNotFoundException)
            {
                // Zipped and deleted since the folder was listed; the next refresh drops it from the list
                return string.Empty;
            }
        }

        // Serilog's file format: "2026-10-07 10:29:30.168 +05:30 [ERR] message"
        private static readonly System.Text.RegularExpressions.Regex LogEntryHeader =
            new System.Text.RegularExpressions.Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+ [+-]\d{2}:\d{2} \[(\w{3})\]");

        /// <summary>
        /// Appends complete log lines, showing Error and Fatal entries in red and Warning entries in orange.
        /// Lines that do not start a new entry, such as an exception's stack trace, keep the color of the
        /// entry they belong to.
        /// </summary>
        private void AppendLogLines(RichTextBox logBox, LogTail tail, string text)
        {
            var run = new StringBuilder();
            Color runColor = tail.EntryColor;
            int lineStart = 0;
            while (lineStart < text.Length)
            {
                int lineEnd = text.IndexOf('\n', lineStart);
                string line = text.Substring(lineStart, lineEnd - lineStart + 1);
                lineStart = lineEnd + 1;

                var header = LogEntryHeader.Match(line);
                if (header.Success)
                    tail.EntryColor = GetLevelColor(header.Groups[1].Value);

                if (tail.EntryColor != runColor)
                {
                    AppendColoredText(logBox, run.ToString(), runColor.IsEmpty ? logBox.ForeColor : runColor);
                    run.Clear();
                    runColor = tail.EntryColor;
                }
                run.Append(line);
            }
            AppendColoredText(logBox, run.ToString(), runColor.IsEmpty ? logBox.ForeColor : runColor);
        }

        // Color.Empty means the log box's normal text color
        private static Color GetLevelColor(string level)
        {
            switch (level)
            {
                case "ERR":
                case "FTL":
                    return Color.Red;
                case "WRN":
                    return Color.DarkOrange;
                default:
                    return Color.Empty;
            }
        }

        private static void AppendColoredText(RichTextBox logBox, string text, Color color)
        {
            if (text.Length == 0)
                return;
            logBox.Select(logBox.TextLength, 0);
            logBox.SelectionColor = color;
            logBox.SelectedText = text;
        }

        /// <summary>
        /// The parts of the log in the folder holding its most recently written part, oldest first.
        /// </summary>
        private List<string> GetLogFiles(string searchPattern, out string message)
        {
            message = null;
            string logDirectory = Path.Combine(Application.StartupPath, "logs");
            if (!Directory.Exists(logDirectory))
            {
                message = "No logs found. Services may not have started yet.";
                return null;
            }
            var directory = new DirectoryInfo(logDirectory);
            // Services write into a logs/yyyy-MM-dd folder per day
            FileInfo[] files = directory.GetFiles(searchPattern + "*.txt", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                message = "No log file found for " + searchPattern + ".";
                return null;
            }
            string dayFolder = files.OrderByDescending(f => f.LastWriteTime).First().DirectoryName;
            return files.Where(f => f.DirectoryName == dayFolder)
                        .OrderBy(f => GetLogPartNumber(f.Name, searchPattern))
                        .Select(f => f.FullName)
                        .ToList();
        }

        // Serilog names the parts StoreSCU.txt, StoreSCU_001.txt, StoreSCU_002.txt, ... Compared as numbers
        // because past _999 the names no longer sort as text.
        private static int GetLogPartNumber(string fileName, string searchPattern)
        {
            string suffix = Path.GetFileNameWithoutExtension(fileName).Substring(searchPattern.Length).TrimStart('_');
            return int.TryParse(suffix, out int partNumber) ? partNumber : 0;
        }

        private class LogTail
        {
            // The parts shown, oldest first; text is appended from the last one
            public List<string> Files = new List<string>();
            // How far the last part has been read
            public long Position;
            public string Message;
            // Text after the last line break read so far, shown once its line is complete
            public string PendingLine = string.Empty;
            // Color of the last entry shown, so its following lines keep it; Empty for normal text
            public Color EntryColor = Color.Empty;

            public void Reset()
            {
                Files = new List<string>();
                Position = 0;
                Message = null;
                PendingLine = string.Empty;
                EntryColor = Color.Empty;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void GetServeListing()
        {
            try
            {
                this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
                string errorString = string.Empty;
                // Get Checking of Server from Confiruation FIle. 
                mtchkb_CheckServer.Checked = Convert.ToBoolean(cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/checkserver"));

                // Get Server List from Database.

                DataSet dsResult = objDAL.LoadServerList(ref errorString);

                if (dsResult == null)
                {
                    MessageBox.Show(this, "Error loading Server List : " + errorString,
                                     "Error loading Server List", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
                    return;
                }
                else
                {
                    // Bind even when empty, so deleting the last server clears it from the grid
                    dgv_ServerList.DataSource = dsResult.Tables[0];
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error loading Server List" + ex.Message,
                                     "Error loading Server List", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = System.Windows.Forms.Cursors.Default;
            }

        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mtchkb_CheckServer_CheckedChanged(object sender, EventArgs e)
        {
            SetSetting("checkserver", mtchkb_CheckServer.Checked.ToString());
        }

        private void mtbtn_AddUpdateServer_Click(object sender, EventArgs e)
        {
            string errorString = string.Empty;
            if ( txt_ServerName.Text == string.Empty || txt_AETitle.Text == string.Empty ||
                txt_HostAddress.Text == string.Empty || txt_PortNo.Text == string.Empty )
            {
                MessageBox.Show(this, "Please fill mandatory fields. All Fields are mandatory except description",
                                     "Check Mandatory", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
                return;
            }

            // Add Server to Database
            if (objDAL != null )
            {
                if ( objDAL.insertorUpdateServer(txt_ServerName.Text, txt_AETitle.Text, txt_HostAddress.Text, txt_PortNo.Text, rtb_Description.Text, primarykey, bUpdateServer, ref errorString)) {
                    MessageBox.Show(this, "Server details added/updated Successfully!!",
                                    "Server added Successfully", MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                    if (bUpdateServer)
                    {
                        bUpdateServer = false;
                        mtbtn_AddUpdateServer.Text = "Add Server";
                    }
                    ClearTextBoxes();
                    GetServeListing();
                }
                else
                {
                    MessageBox.Show(this, "Error Saving Server with error message : " + errorString,
                                     "Error loading Server List", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
                }


            }


        }

        /// <summary>
        /// Clear Server Details TextBoxes
        /// </summary>
        private void ClearTextBoxes()
        {
            txt_ServerName.Text = txt_AETitle.Text = txt_HostAddress.Text = txt_PortNo.Text = rtb_Description.Text = string.Empty;
            primarykey = string.Empty;

            // Without a selected server the next save must add, not update (an update with no pk is invalid SQL)
            bUpdateServer = false;
            mtbtn_AddUpdateServer.Text = "Add Server";
        }

        private void frm_Mainform_FormClosed(object sender, FormClosedEventArgs e)
        {
            logRefreshTimer.Dispose();
            objDAL.Dispose();
            this.Dispose();
            Application.Exit();
        }

        private void dgv_ServerList_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                string errorString = string.Empty;
                if ( e.ColumnIndex == 0 )
                {
                    if ( MessageBox.Show(this, "Are you sure you want to delete the server details?",
                                    "Delete Server", MessageBoxButtons.YesNoCancel,
                                    MessageBoxIcon.Information) == DialogResult.Yes )
                    {
                        if (dgv_ServerList.Rows[e.RowIndex].Cells["pk"] != null)
                            if (objDAL.DeleteServer(dgv_ServerList.Rows[e.RowIndex].Cells["pk"].Value.ToString(),ref errorString) )
                            {
                                MessageBox.Show(this, "Server details deleted Successfully!!",
                                        "Server details deleted Successfully", MessageBoxButtons.OK,MessageBoxIcon.Information);
                                ClearTextBoxes();
                                GetServeListing();
                                return;
                            }
                        else
                            {
                                MessageBox.Show(this, "Error Deleting Server from server list with error message : " + errorString,
                                              "Error deleting Server", MessageBoxButtons.OK,
                                              MessageBoxIcon.Error);

                            }
                    }

                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(this, "Error Deleting Server from server list with error message : " + ex.Message,
                              "Error deleting Server from server list", MessageBoxButtons.OK,
                              MessageBoxIcon.Error);

            }
        }


        private void dgv_ServerList_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            { 
                // Get Values from the Grid
                if (e.RowIndex >= 0 ) { 
                    if (dgv_ServerList.Rows[e.RowIndex].Cells["servername"] != null )
                        txt_ServerName.Text =  dgv_ServerList.Rows[e.RowIndex].Cells["servername"].Value.ToString();
                    if (dgv_ServerList.Rows[e.RowIndex].Cells["serverAETitle"] != null)
                        txt_AETitle.Text = dgv_ServerList.Rows[e.RowIndex].Cells["serverAETitle"].Value.ToString();
                    if (dgv_ServerList.Rows[e.RowIndex].Cells["serverHost"] != null)
                        txt_HostAddress.Text = dgv_ServerList.Rows[e.RowIndex].Cells["serverHost"].Value.ToString();
                    if (dgv_ServerList.Rows[e.RowIndex].Cells["serverPort"] != null)
                        txt_PortNo.Text = dgv_ServerList.Rows[e.RowIndex].Cells["serverPort"].Value.ToString();
                    if (dgv_ServerList.Rows[e.RowIndex].Cells["description"] != null)
                        rtb_Description.Text = dgv_ServerList.Rows[e.RowIndex].Cells["description"].Value.ToString();
                    if (dgv_ServerList.Rows[e.RowIndex].Cells["pk"] != null)
                        primarykey = dgv_ServerList.Rows[e.RowIndex].Cells["pk"].Value.ToString();

                    bUpdateServer = true;
                    mtbtn_AddUpdateServer.Text = "Update Server";
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(this, "Error loading content from grid: " + ex.Message,
                                   "Error loading content ", MessageBoxButtons.OK,
                                   MessageBoxIcon.Error);
            }
        }

        private void frm_Mainform_Load(object sender, EventArgs e)
        {
            if (Global.deployType > 1 )
            {
                HideControlsForServer(Global.deployType);
            }
            uctrl_ServerManager1.EnableDisableButtons();

            // On small or scaled screens the window can be taller than the screen, which hides the
            // bottom of every tab. Fit it to the screen so tabs that scroll show their scroll bar.
            Rectangle workArea = Screen.FromControl(this).WorkingArea;
            if (Width > workArea.Width || Height > workArea.Height)
            {
                Size = new Size(Math.Min(Width, workArea.Width), Math.Min(Height, workArea.Height));
                Location = new Point(workArea.Left + (workArea.Width - Width) / 2, workArea.Top + (workArea.Height - Height) / 2);
            }
        }


        /// <summary>
        /// Hide Controls which are not needed for Server
        /// </summary>
        private void HideControlsForServer(int deploymentType)
        {
            if (deploymentType == 2)
            {
                grpb_ModalitySCP.Visible = false;
                // Remove MWL and SCU Tabs from Log Form
                tbc_Logs.TabPages.Remove(tp_MWLLog);
                tbc_Logs.TabPages.Remove(tp_SCULog);
                grpb_StoreSCUSettings.Enabled = false;
                //mtc_Modules.TabPages.Remove(tbp_SCUSettings);
            }
            else if ( deploymentType == 3 )
            {
                grpb_ModalitySCP.Visible = false;
                tbc_Logs.TabPages.Remove(tp_MWLLog);
                grpb_StoreSCUSettings.Enabled = true;
            }
        }

        private void mbtn_SaveSCUSettings_Click(object sender, EventArgs e)
        {
            try
            {
                if ( string.IsNullOrEmpty(mtxtb_StoreSCUAETitle.Text) ||
                    string.IsNullOrEmpty(mtxtb_StoreSCUHost.Text) || string.IsNullOrEmpty(mtxtb_StoreSCUPort.Text) || string.IsNullOrEmpty(mtb_callingAETitle.Text))
                {
                    MessageBox.Show(this, "Please fill mandatory fields. All Fields are mandatory",
                                      "Error Saving Configuration", MessageBoxButtons.OK,
                                      MessageBoxIcon.Error);
                    return;
                }
                else
                {
                    // Save StorageSCP Settings
                    SetSetting("sscuaetitle", mtxtb_StoreSCUAETitle.Text);
                    SetSetting("sscuhost", mtxtb_StoreSCUHost.Text);
                    SetSetting("sscuport", mtxtb_StoreSCUPort.Text);
                    SetSetting("callingaetitle", mtb_callingAETitle.Text);

                    MessageBox.Show(this, "Store SCU Settings saved Successfully!!",
                                     "Saving Configuration Successfull", MessageBoxButtons.OK,
                                     MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error Saving Store SCU Configuration with expection : " + ex.Message,
                                     "Error Saving Store SCU Configuration", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
            }
        }

        private void dataGridView2_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (this.dgv_PatientList.Columns[e.ColumnIndex].Name == "status")
            {
                if (e.Value != null)
                {
                    switch (e.Value)
                    {
                        case 0:
                            e.Value = "Registered";
                            e.CellStyle.ForeColor = Color.Black;
                            break;
                        case 1:
                            e.Value = "Modality Query";
                            e.CellStyle.ForeColor = Color.Black;
                            break;
                        case 2:
                            e.Value = "Image(s) Recieved";
                            e.CellStyle.ForeColor = Color.Black;
                            break;
                        case 3:
                            e.Value = "Image(s) Uploaded";
                            e.CellStyle.ForeColor = Color.Black;
                            break;
                        case -10:
                            e.Value = "Image(s) Uploaded Failed";
                            e.CellStyle.ForeColor = Color.Red;
                            break;

                    }
                }
                
            }
        }

        private void mbtn_PatientRefresh_Click(object sender, EventArgs e)
        {
            GetPatientDetails();
        }

        /// <summary>
        /// Load the integration settings from care_config into the Configuration tab
        /// </summary>
        private void GetConfiguration()
        {
            try
            {
                this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
                string errorString = string.Empty;

                DataSet dsResult = objDAL.LoadConfig(ref errorString);

                if (dsResult == null)
                {
                    MessageBox.Show(this, "Error loading Configuration : " + errorString,
                                     "Error loading Configuration", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
                    return;
                }

                var fields = GetConfigFields();
                loadedConfigValues.Clear();
                foreach (DataRow row in dsResult.Tables[0].Rows)
                {
                    string key = row["config_key"].ToString();
                    if (fields.ContainsKey(key))
                        loadedConfigValues[key] = row["config_value"].ToString().Trim();
                }

                // A blank setting shows the value the services use for it instead
                filledDefaults.Clear();
                foreach (var field in fields)
                {
                    loadedConfigValues.TryGetValue(field.Key, out string value);
                    if (string.IsNullOrEmpty(value))
                    {
                        value = GetConfigDefault(field.Key);
                        filledDefaults[field.Key] = value;
                    }
                    field.Value.Text = value;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error loading Configuration" + ex.Message,
                                     "Error loading Configuration", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = System.Windows.Forms.Cursors.Default;
            }
        }

        private void mbtn_SaveConfig_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(mtxtb_FacilityId.Text))
                {
                    MessageBox.Show(this, "Please enter the Facility Id. The CARE worklist is not fetched without it.",
                                     "Check Mandatory", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
                    return;
                }

                var values = GetConfigFields().ToDictionary(field => field.Key, field => GetValueToSave(field.Key, field.Value));

                string fromDate = values["care_from_date"];
                if (fromDate != string.Empty && !DateTime.TryParseExact(fromDate, ConfigDateFormat,
                        System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    MessageBox.Show(this, "From Date must be in the format " + ConfigDateFormat + ", or left blank to use the default. Use the Calendar button to select it.",
                                     "Check From Date", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
                    return;
                }

                // Blank uses the default; otherwise a whole number of at least minValue, as the services require
                var wholeNumberFields = new[]
                {
                    new { Key = "scu_poll_interval_seconds", Name = "Poll Interval (sec)", MinValue = 1 },
                    new { Key = "worklist_refresh_start_seconds", Name = "Refresh Start (sec)", MinValue = 0 },
                    new { Key = "worklist_refresh_interval_seconds", Name = "Refresh Interval (sec)", MinValue = 1 },
                    new { Key = "max_upload_retries", Name = "Max Upload Retries", MinValue = 1 },
                    new { Key = "upload_retry_delay_minutes", Name = "Retry Delay (min)", MinValue = 1 },
                };
                foreach (var field in wholeNumberFields)
                {
                    string value = values[field.Key];
                    if (value != string.Empty && (!int.TryParse(value, out int number) || number < field.MinValue))
                    {
                        MessageBox.Show(this, field.Name + " must be a whole number of " + field.MinValue + " or more, or left blank to use the default.",
                                         "Check " + field.Name, MessageBoxButtons.OK,
                                         MessageBoxIcon.Error);
                        return;
                    }
                }

                // One entry per changed setting, e.g. "scu_poll_interval_seconds: 5 -> 10"
                var changes = new System.Collections.Generic.Dictionary<string, string>();
                foreach (var value in values)
                {
                    loadedConfigValues.TryGetValue(value.Key, out string oldValue);
                    string newValue = value.Value.Trim();
                    if ((oldValue ?? string.Empty) != newValue)
                        changes[value.Key] = $"{value.Key}: {DisplayConfigValue(oldValue)} -> {DisplayConfigValue(newValue)}";
                }

                if (changes.Count == 0)
                {
                    MessageBox.Show(this, "No configuration changes to save.",
                                     "Saving Configuration", MessageBoxButtons.OK,
                                     MessageBoxIcon.Information);
                    return;
                }

                string errorString = string.Empty;
                if (objDAL.SaveConfig(values, ref errorString))
                {
                    this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
                    string restartSummary;
                    try
                    {
                        restartSummary = RestartServicesForChanges(changes);
                    }
                    finally
                    {
                        this.Cursor = System.Windows.Forms.Cursors.Default;
                    }

                    MessageBox.Show(this, "Configuration saved Successfully!!" + Environment.NewLine + Environment.NewLine + restartSummary,
                                     "Saving Configuration Successfull", MessageBoxButtons.OK,
                                     MessageBoxIcon.Information);
                    GetConfiguration();
                }
                else
                {
                    MessageBox.Show(this, "Error Saving Configuration with error message : " + errorString,
                                     "Error Saving Configuration", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error Saving Configuration with expection : " + ex.Message,
                                     "Error Saving Configuration", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
            }
        }

        // care_from_date is sent to the CARE worklist API as entered, in this format
        private const string ConfigDateFormat = "yyyy-MM-dd HH:mm:ss";

        private const string MwlServiceName = "Care MWL SCP Service";
        private const string StoreScpServiceName = "Care Store SCP Service";
        private const string StoreScuServiceName = "Care Store SCU Service";

        // The default put into each box whose care_config setting was blank when the tab was loaded
        private readonly System.Collections.Generic.Dictionary<string, string> filledDefaults = new System.Collections.Generic.Dictionary<string, string>();

        /// <summary>
        /// The value to store for a box. A default filled in for a blank setting is stored as blank again, so
        /// saving without changing it does not pin the default in care_config or restart the services.
        /// </summary>
        private string GetValueToSave(string configKey, MaterialTextBox textBox)
        {
            string value = textBox.Text.Trim();
            if (filledDefaults.TryGetValue(configKey, out string filledDefault) && value == filledDefault.Trim())
                return string.Empty;
            return value;
        }

        // The care_config values shown when the Configuration tab was last loaded, to find what Save changes
        private readonly System.Collections.Generic.Dictionary<string, string> loadedConfigValues = new System.Collections.Generic.Dictionary<string, string>();

        /// <summary>
        /// The services that read each care_config setting
        /// </summary>
        private static string[] GetServicesUsingSetting(string configKey)
        {
            switch (configKey)
            {
                case "facility_id":
                case "care_modality":
                case "care_from_date":
                    return new[] { MwlServiceName, StoreScuServiceName };
                case "worklist_refresh_start_seconds":
                case "worklist_refresh_interval_seconds":
                    return new[] { MwlServiceName };
                case "scu_poll_interval_seconds":
                case "max_upload_retries":
                case "upload_retry_delay_minutes":
                case "failed_scp_folder":
                    return new[] { StoreScuServiceName };
                case "scp_folder":
                    return new[] { MwlServiceName, StoreScpServiceName, StoreScuServiceName };
                default:
                    return new string[0];
            }
        }

        private static string DisplayConfigValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(blank - default)" : value.Trim();
        }

        /// <summary>
        /// Restarts each running service that reads a changed setting so it loads the new configuration.
        /// The changes are passed as start parameters, and the service writes them to its own log.
        /// Returns a summary of what happened to each service.
        /// </summary>
        private string RestartServicesForChanges(System.Collections.Generic.Dictionary<string, string> changes)
        {
            var summary = new StringBuilder();
            foreach (string serviceName in new[] { MwlServiceName, StoreScpServiceName, StoreScuServiceName })
            {
                string[] serviceChanges = changes.Where(change => GetServicesUsingSetting(change.Key).Contains(serviceName))
                                                 .Select(change => change.Value).ToArray();
                if (serviceChanges.Length == 0)
                    continue;

                try
                {
                    if (!ServiceController.GetServices().Any(s => s.ServiceName == serviceName))
                    {
                        summary.AppendLine(serviceName + ": not installed");
                        continue;
                    }

                    using (ServiceController service = new ServiceController(serviceName))
                    {
                        if (service.Status != ServiceControllerStatus.Running)
                        {
                            summary.AppendLine(serviceName + ": not running - it will use the new configuration when started");
                            continue;
                        }

                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        service.Start(serviceChanges);
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        summary.AppendLine(serviceName + ": restarted");
                    }
                }
                catch (Exception ex)
                {
                    summary.AppendLine(serviceName + ": restart failed (" + ex.Message + ") - restart it from Server Manager");
                }
            }
            return summary.ToString().TrimEnd();
        }

        /// <summary>
        /// Lets only digits be typed into the seconds and retry count fields
        /// </summary>
        private void mtxtb_WholeNumber_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                e.Handled = true;
        }

        /// <summary>
        /// Opens a calendar with a time picker and puts the selected date and time into From Date
        /// </summary>
        private void mtxtb_CareFromDate_TrailingIconClick(object sender, EventArgs e)
        {
            // Start from the entered value, else the default, else today
            DateTime initial = DateTime.Today;
            string current = mtxtb_CareFromDate.Text.Trim();
            if (current == string.Empty)
                current = GetConfigDefault("care_from_date");
            if (DateTime.TryParseExact(current, new[] { ConfigDateFormat, "yyyy-MM-dd" },
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime parsed))
                initial = parsed;

            using (Form dialog = new Form())
            {
                dialog.Text = "Select From Date";
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.AutoSize = true;
                dialog.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                dialog.Padding = new Padding(12);

                MonthCalendar calendar = new MonthCalendar
                {
                    Location = new Point(12, 12),
                    MaxSelectionCount = 1,
                    SelectionStart = initial.Date
                };
                Label timeLabel = new Label
                {
                    Text = "Time",
                    AutoSize = true,
                    Location = new Point(12, calendar.Bottom + 16)
                };
                DateTimePicker timePicker = new DateTimePicker
                {
                    Format = DateTimePickerFormat.Custom,
                    CustomFormat = "HH:mm:ss",
                    ShowUpDown = true,
                    Value = DateTime.Today + initial.TimeOfDay,
                    Location = new Point(70, calendar.Bottom + 12),
                    Width = 100
                };
                Button okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Location = new Point(12, timePicker.Bottom + 16)
                };
                Button cancelButton = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(okButton.Right + 8, timePicker.Bottom + 16)
                };

                dialog.Controls.AddRange(new Control[] { calendar, timeLabel, timePicker, okButton, cancelButton });
                dialog.AcceptButton = okButton;
                dialog.CancelButton = cancelButton;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    DateTime selected = calendar.SelectionStart.Date + timePicker.Value.TimeOfDay;
                    mtxtb_CareFromDate.Text = selected.ToString(ConfigDateFormat, System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }

        private void mtxtb_ScpFolder_TrailingIconClick(object sender, EventArgs e)
        {
            BrowseForFolder(mtxtb_ScpFolder, "scp_folder", "Select the folder where received DICOM files are saved and picked up for upload");
        }

        private void mtxtb_FailedScpFolder_TrailingIconClick(object sender, EventArgs e)
        {
            BrowseForFolder(mtxtb_FailedScpFolder, "failed_scp_folder", "Select the folder files are moved to after the upload retry limit is hit");
        }

        /// <summary>
        /// Lets the user pick a folder, starting from the one entered or the default, and puts it in the text box
        /// </summary>
        private void BrowseForFolder(MaterialTextBox textBox, string configKey, string description)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.ShowNewFolderButton = true;

                string current = textBox.Text.Trim();
                if (current == string.Empty)
                    current = GetConfigDefault(configKey);
                if (Directory.Exists(current))
                    dialog.SelectedPath = current;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    textBox.Text = dialog.SelectedPath;
            }
        }

        // The text box recolours its icons to the theme, so only the shape drawn here matters
        private const int IconSize = 24;

        private static Bitmap CreateCalendarIcon()
        {
            Bitmap icon = new Bitmap(IconSize, IconSize);
            using (Graphics g = Graphics.FromImage(icon))
            using (Pen pen = new Pen(Color.Black, 2))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.DrawRectangle(pen, 3, 5, 18, 16);     // page
                g.DrawLine(pen, 3, 10, 21, 10);         // header bar
                g.DrawLine(pen, 8, 2, 8, 7);            // binder rings
                g.DrawLine(pen, 16, 2, 16, 7);
                g.FillRectangle(Brushes.Black, 7, 13, 3, 3);    // a marked day
            }
            return icon;
        }

        private static Bitmap CreateFolderIcon()
        {
            Bitmap icon = new Bitmap(IconSize, IconSize);
            using (Graphics g = Graphics.FromImage(icon))
            using (Pen pen = new Pen(Color.Black, 2) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round })
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.DrawPolygon(pen, new[]
                {
                    new Point(2, 5), new Point(9, 5), new Point(11, 8), new Point(22, 8),
                    new Point(22, 20), new Point(2, 20)
                });
                g.DrawLine(pen, 2, 11, 22, 11);         // flap
            }
            return icon;
        }

        /// <summary>
        /// The Configuration tab text box for each care_config key
        /// </summary>
        private System.Collections.Generic.Dictionary<string, MaterialTextBox> GetConfigFields()
        {
            return new System.Collections.Generic.Dictionary<string, MaterialTextBox>
            {
                { "facility_id", mtxtb_FacilityId },
                { "care_modality", mtxtb_CareModality },
                { "care_from_date", mtxtb_CareFromDate },
                { "scu_poll_interval_seconds", mtxtb_ScuPollInterval },
                { "worklist_refresh_start_seconds", mtxtb_WorklistRefreshStart },
                { "worklist_refresh_interval_seconds", mtxtb_WorklistRefreshInterval },
                { "max_upload_retries", mtxtb_MaxUploadRetries },
                { "upload_retry_delay_minutes", mtxtb_UploadRetryDelay },
                { "scp_folder", mtxtb_ScpFolder },
                { "failed_scp_folder", mtxtb_FailedScpFolder },
            };
        }

        /// <summary>
        /// The value the services use when a care_config setting is blank: their App.config value, or
        /// the built-in default when App.config has none. Mirrors the fallbacks in the MWL, SCU and
        /// Store SCP services.
        /// </summary>
        private string GetConfigDefault(string configKey)
        {
            switch (configKey)
            {
                case "care_modality":
                    return CombineServiceDefaults(ReadServiceSetting("CARE_MWL_Service", "careModality"), ReadServiceSetting("CARE_SCU_Service", "careModality"));
                case "care_from_date":
                    return CombineServiceDefaults(ReadServiceSetting("CARE_MWL_Service", "careFromDate"), ReadServiceSetting("CARE_SCU_Service", "careFromDate"));
                case "worklist_refresh_start_seconds":
                    return WholeNumberOrDefault(ReadServiceSetting("CARE_MWL_Service", "worklistRefreshStartSeconds"), 30, 0);
                case "worklist_refresh_interval_seconds":
                    return WholeNumberOrDefault(ReadServiceSetting("CARE_MWL_Service", "worklistRefreshIntervalSeconds"), 30, 1);
                case "scu_poll_interval_seconds":
                    return "5";
                case "scp_folder":
                    return Path.Combine(Global._applicationPath, "SCP");
                case "failed_scp_folder":
                    return Path.Combine(Global._applicationPath, "FailedSCP");
                case "max_upload_retries":
                    return WholeNumberOrDefault(ReadServiceSetting("CARE_SCU_Service", "maxUploadRetries"), 10, 1);
                case "upload_retry_delay_minutes":
                    return "2";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Reads an appSettings value from a service's .exe.config installed next to this application.
        /// Returns null when the file or the key is missing.
        /// </summary>
        private string ReadServiceSetting(string serviceAssembly, string key)
        {
            try
            {
                string configPath = Path.Combine(Global._applicationPath, serviceAssembly + ".exe.config");
                if (!File.Exists(configPath))
                    return null;

                var configDoc = new System.Xml.XmlDocument();
                configDoc.Load(configPath);
                foreach (System.Xml.XmlNode node in configDoc.SelectNodes("/configuration/appSettings/add"))
                {
                    if (node.Attributes?["key"]?.Value == key)
                        return node.Attributes["value"]?.Value ?? string.Empty;
                }
            }
            catch (Exception)
            {
                // Unreadable config - shown as not found
            }
            return null;
        }

        /// <summary>
        /// The value when the MWL and SCU services agree. Empty when they differ or App.config has none,
        /// as no single value can be shown in the box.
        /// </summary>
        private static string CombineServiceDefaults(string mwlValue, string scuValue)
        {
            return mwlValue == scuValue ? (mwlValue ?? string.Empty).Trim() : string.Empty;
        }

        /// <summary>
        /// The App.config value when it is a whole number >= minValue, otherwise the built-in default,
        /// as the services do.
        /// </summary>
        private static string WholeNumberOrDefault(string appConfigValue, int builtInDefault, int minValue)
        {
            if (int.TryParse(appConfigValue, out int parsed) && parsed >= minValue)
                return parsed.ToString();
            return builtInDefault.ToString();
        }
    }
}
