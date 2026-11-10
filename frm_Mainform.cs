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
            { "StoreSCU", new LogTail() },
            { cls_AppLog.FileName, new LogTail() }
        };
        public frm_Mainform()
        {
            InitializeComponent();
            logRefreshTimer.Tick += logRefreshTimer_Tick;

            // Clicking the icon at the end of the box opens the calendar or folder browser
            mtxtb_CareFromDate.TrailingIcon = CreateCalendarIcon();
            mtxtb_ScpFolder.TrailingIcon = CreateFolderIcon();
            mtxtb_FailedScpFolder.TrailingIcon = CreateFolderIcon();

            // Server List is hidden for now, unless checkserver is on: the DICOM services then reject any
            // scanner not listed there, and the tab is the only place to list it or turn checkserver off.
            // Removed before the colour scheme is set below: the drawer bakes each icon at its tab's position
            // then, so removing a tab later leaves the icons of the tabs after it drawn off screen.
            bool.TryParse(cls_PlexusConfig.ReadDetailsFromXML(Global._applicationPath, @"/configurations/checkserver"), out bool checkServer);
            if (!checkServer)
                mtc_Modules.TabPages.Remove(tbp_ServerList);

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

        /// <summary>
        /// Shows the Modality SCP and Store SCP settings, read-only. They are not entered by hand: they are
        /// the modality_scp and store_scp of the DICOM Enabler selected in the Configuration tab, as CARE
        /// returned them (its care_devices row).
        /// </summary>
        private void GetSCPSettings()
        {
            var boxes = new Dictionary<string, MaterialTextBox>
            {
                { "mwlaetitle", mtxtb_ModalityAETitle }, { "mwlhost", mtxtb_ModalityHost }, { "mwlport", mtxtb_ModalityPort },
                { "sscpaetitle", mtxtb_StoreAETitle }, { "sscphost", mtxtb_StoreHost }, { "sscpport", mtxtb_StorePort },
            };
            foreach (MaterialTextBox box in boxes.Values)
                box.Text = string.Empty;

            try
            {
                string errorString = string.Empty;
                CareDeviceRecord enabler = cls_ConfigDefaults.GetSavedEnabler(objDAL, ref errorString);
                if (!string.IsNullOrEmpty(errorString))
                {
                    lbl_ScpSettingsSource.Text = "Could not read the DICOM Enabler's settings: " + errorString;
                    return;
                }
                if (enabler == null)
                {
                    lbl_ScpSettingsSource.Text = "No DICOM Enabler is selected. Select one in the Configuration tab and save it: " +
                                                 "these settings come from its DICOM endpoints in CARE.";
                    return;
                }
                if (!cls_ConfigDefaults.TryGetDicomEndpointSettings(enabler, out Dictionary<string, string> settings, ref errorString))
                {
                    lbl_ScpSettingsSource.Text = errorString;
                    return;
                }
                foreach (var box in boxes)
                    box.Value.Text = settings[box.Key];
                lbl_ScpSettingsSource.Text = $"From the DICOM endpoints of DICOM Enabler {enabler.DeviceName?.Trim() ?? enabler.DeviceId} in CARE (read-only). " +
                                             "To change them, change them in CARE or select another DICOM Enabler in the Configuration tab.";
            }
            catch (Exception ex)
            {
                lbl_ScpSettingsSource.Text = "Could not read the DICOM Enabler's settings: " + ex.Message;
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
                // Matched by tab page, not index, so hiding a tab does not shift the others
                TabPage selectedTab = mtc_Modules.SelectedTab;
                if (selectedTab == tbp_Settings) // Get SCP Settings
                    GetSCPSettings();
                else if (selectedTab == tbp_SCUSettings)
                    GetSCUSettings();
                else if (selectedTab == tbp_ServerList) // Server List Tab Clicked
                    GetServeListing();
                else if (selectedTab == tbp_Configuration) // Configuration Tab Clicked
                    GetConfiguration();
                else if (selectedTab == tbp_Devices)
                    LoadDevices();
                else if (selectedTab == tdp_ViewPatients)
                    GetPatientDetails();
                else if (selectedTab == tbp_ViewLog) // View Logs Clicked
                    GetAndPopulateLogs();
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
            if (mtc_Modules.SelectedTab != tbp_ViewLog)
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

            // This application's own log: CARE Modalities API calls, assignment changes, device updates, configuration
            RefreshLog(rtb_AppLog, cls_AppLog.FileName);
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

            // Add Server to Database, once the user confirms
            if (objDAL != null )
            {
                if (cls_ConfirmDialog.Show(this, (bUpdateServer ? "Update" : "Add") + " Server - confirm",
                        (bUpdateServer ? "The server will be updated" : "This server will be added") + " in the database. Save it?",
                        new[] { "Server Name: " + txt_ServerName.Text, "AE Title: " + txt_AETitle.Text, "Host: " + txt_HostAddress.Text,
                                "Port: " + txt_PortNo.Text, "Description: " + rtb_Description.Text },
                        ("Save", DialogResult.Yes), ("Cancel", DialogResult.Cancel)) != DialogResult.Yes)
                    return;

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
                    if (fields.ContainsKey(key) || key == DicomEnablerIdKey)
                        loadedConfigValues[key] = row["config_value"].ToString().Trim();
                }

                // The saved DICOM Enabler stays selected while the user pages through the list
                loadedConfigValues.TryGetValue(DicomEnablerIdKey, out string savedEnablerId);
                string enablerError = string.Empty;
                savedEnabler = string.IsNullOrWhiteSpace(savedEnablerId) ? null : objDAL.GetCareDevice(savedEnablerId.Trim(), ref enablerError);
                selectedDicomEnabler = string.IsNullOrWhiteSpace(savedEnablerId)
                    ? null
                    : new DicomEnablerItem(savedEnablerId.Trim(), savedEnabler?.DeviceName, null);

                // A blank setting (services not started yet) shows its default, which Save stores in care_config
                foreach (var field in fields)
                {
                    loadedConfigValues.TryGetValue(field.Key, out string value);
                    if (string.IsNullOrEmpty(value))
                        value = cls_ConfigDefaults.GetConfigDefault(field.Key);
                    field.Value.Text = value;
                }

                LoadDicomEnablerPage(0);
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

        private async void mbtn_SaveConfig_Click(object sender, EventArgs e)
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

                var values = GetConfigFields().ToDictionary(field => field.Key, field => field.Value.Text.Trim());
                if (IsDicomEnablerConfigAvailable())
                {
                    values[DicomEnablerIdKey] = selectedDicomEnabler?.Id ?? string.Empty;
                }

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

                // A newly selected DICOM Enabler is retrieved from CARE (its details are only returned while
                // it is unassigned) and assigned before it is saved; the one it replaces is unassigned after
                loadedConfigValues.TryGetValue(DicomEnablerIdKey, out string oldEnablerId);
                oldEnablerId = (oldEnablerId ?? string.Empty).Trim();
                loadedConfigValues.TryGetValue("facility_id", out string oldFacilityId);
                bool enablerChanged = changes.ContainsKey(DicomEnablerIdKey);
                string newEnablerId = enablerChanged ? values[DicomEnablerIdKey] : oldEnablerId;
                string newFacilityId = values["facility_id"];
                bool newEnablerAssigned = false;
                CareDeviceRecord newEnabler = null;

                // Nothing is written to the database (or CARE) until the user confirms the changes
                var confirmLines = changes.Where(change => change.Key != DicomEnablerIdKey)
                                          .Select(change => change.Value)
                                          .ToList();
                if (enablerChanged)
                {
                    string newEnablerText = newEnablerId == string.Empty ? "none" : $"{selectedDicomEnabler?.Name ?? newEnablerId} ({newEnablerId})";
                    confirmLines.Add($"DICOM Enabler: {DescribeEnabler(oldEnablerId)} -> {newEnablerText}");
                    if (newEnablerId != string.Empty)
                        confirmLines.Add("  The new DICOM Enabler is assigned to this enabler in CARE and saved to care_devices, and its DICOM endpoints become the Modality SCP / Store SCP settings.");
                    if (oldEnablerId != string.Empty)
                        confirmLines.Add("  The previous DICOM Enabler is unassigned in CARE, and it and its saved devices are removed from care_devices.");
                }
                if (cls_ConfirmDialog.Show(this, "Save Configuration - confirm changes",
                        "These changes will be saved to the database, and the services using them restarted. Save them?",
                        confirmLines, ("Save", DialogResult.Yes), ("Cancel", DialogResult.Cancel)) != DialogResult.Yes)
                {
                    cls_AppLog.Info("CONFIG", "Configuration changes not saved - cancelled by the user: " +
                                                  string.Join("; ", confirmLines.Select(line => line.Trim())));
                    return;
                }

                mbtn_SaveConfig.Enabled = false;
                this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
                try
                {
                    if (enablerChanged)
                        cls_AppLog.Info("DICOM ENABLER", newEnablerId == string.Empty
                            ? $"DICOM Enabler cleared in the Configuration tab (was {DescribeEnabler(oldEnablerId)})"
                            : $"DICOM Enabler {selectedDicomEnabler?.Name ?? newEnablerId} ({newEnablerId}) selected in the Configuration tab" +
                              (oldEnablerId == string.Empty ? string.Empty : $", replacing {DescribeEnabler(oldEnablerId)}"));

                    if (enablerChanged && newEnablerId != string.Empty)
                    {
                        if (!TryGetModalitiesApi(newFacilityId, out string modalitiesURL, out string apiKey, out string apiError))
                        {
                            ShowSaveError("The DICOM Enabler could not be assigned: " + apiError + " Nothing was saved.");
                            return;
                        }
                        var assigned = await System.Threading.Tasks.Task.Run(() => AssignDicomEnabler(modalitiesURL, apiKey, newEnablerId));
                        if (IsDisposed)
                            return;
                        if (!assigned.Item1)
                        {
                            ShowSaveError(assigned.Item3 + " Nothing was saved.");
                            return;
                        }
                        newEnablerAssigned = true;
                        // Saved as its own care_devices row, one column per field, with its DICOM endpoints
                        newEnabler = ucls_CareModalities.ToDeviceRecord(assigned.Item2, newFacilityId, null);
                    }

                    string errorString = string.Empty;
                    if ((newEnabler != null && !objDAL.SaveCareDevice(newEnabler, ref errorString)) || !objDAL.SaveConfig(values, ref errorString))
                    {
                        string message = "Error Saving Configuration with error message : " + errorString;
                        // Give back the DICOM Enabler assigned above, as it was not saved
                        if (newEnablerAssigned)
                        {
                            int removed = 0;
                            string removeError = string.Empty;
                            objDAL.DeleteCareDevicesOfEnabler(newEnablerId, ref removed, ref removeError);
                            message += Environment.NewLine + Environment.NewLine +
                                       await UnassignDicomEnablerAsync(newFacilityId, newEnablerId, "The DICOM Enabler assigned for this save");
                        }
                        ShowSaveError(message);
                        return;
                    }

                    var notes = new StringBuilder();
                    if (enablerChanged && oldEnablerId != string.Empty)
                    {
                        notes.AppendLine(await UnassignDicomEnablerAsync(string.IsNullOrWhiteSpace(oldFacilityId) ? newFacilityId : oldFacilityId.Trim(),
                                                                         oldEnablerId, "The previous DICOM Enabler"));
                        // It is no longer this enabler's: its row and its devices' rows go
                        int removed = 0;
                        string removeError = string.Empty;
                        if (objDAL.DeleteCareDevicesOfEnabler(oldEnablerId, ref removed, ref removeError))
                            cls_AppLog.Info("DEVICES", $"Removed {removed} care_devices row(s) of the previous DICOM Enabler {DescribeEnabler(oldEnablerId)}");
                        else
                            notes.AppendLine("The previous DICOM Enabler's saved devices could not be removed: " + removeError);
                    }
                    if (newEnablerAssigned)
                    {
                        notes.AppendLine("DICOM Enabler " + (newEnabler.DeviceName ?? newEnablerId) + " is assigned to this enabler in CARE.");
                        // Its endpoints become the Modality SCP and Store SCP settings the services listen with
                        string endpointError = string.Empty;
                        if (!cls_ConfigDefaults.ApplyDicomEndpointSettings(newEnabler, changes, ref endpointError))
                            notes.AppendLine("The SCP settings could not be updated from it: " + endpointError);
                    }

                    foreach (var change in changes)
                        cls_AppLog.Info("CONFIG", change.Value);

                    string restartSummary = RestartServicesForChanges(changes);
                    foreach (string line in restartSummary.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                        cls_AppLog.Info("SERVICES", line);
                    string summary = (notes.ToString().TrimEnd() + Environment.NewLine + Environment.NewLine + restartSummary).Trim();
                    this.Cursor = System.Windows.Forms.Cursors.Default;
                    MessageBox.Show(this, "Configuration saved Successfully!!" + Environment.NewLine + Environment.NewLine + summary,
                                     "Saving Configuration Successfull", MessageBoxButtons.OK,
                                     MessageBoxIcon.Information);
                    GetConfiguration();

                    // Show the modality devices connected to the newly assigned DICOM Enabler, and offer to save
                    // them (Sync, with confirmation)
                    if (newEnablerAssigned)
                    {
                        syncDevicesOnOpen = true;
                        mtc_Modules.SelectedTab = tbp_Devices;
                    }
                }
                finally
                {
                    this.Cursor = System.Windows.Forms.Cursors.Default;
                    mbtn_SaveConfig.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                cls_AppLog.Error("CONFIG", "Saving the configuration failed with exception " + ex);
                MessageBox.Show(this, "Error Saving Configuration with expection : " + ex.Message,
                                     "Error Saving Configuration", MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);
            }
        }

        /// <summary>A saved enabler for the log: its name from care_devices when it is the one loaded, and its id.</summary>
        private string DescribeEnabler(string enablerId)
        {
            if (string.IsNullOrEmpty(enablerId))
                return "none";
            string name = string.Equals(savedEnabler?.DeviceId, enablerId, StringComparison.OrdinalIgnoreCase) ? savedEnabler.DeviceName : null;
            return string.IsNullOrWhiteSpace(name) ? enablerId : $"{name.Trim()} ({enablerId})";
        }

        private void ShowSaveError(string message)
        {
            cls_AppLog.Error("CONFIG", "Configuration not saved: " + message.Replace(Environment.NewLine, " "));
            this.Cursor = System.Windows.Forms.Cursors.Default;
            MessageBox.Show(this, message, "Error Saving Configuration", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>The facility's modalities API URL and the static API key; see cls_CareSync.TryGetModalitiesApi.</summary>
        private static bool TryGetModalitiesApi(string facilityId, out string modalitiesURL, out string apiKey, out string error)
        {
            return cls_CareSync.TryGetModalitiesApi(facilityId, out modalitiesURL, out apiKey, out error);
        }

        /// <summary>
        /// Retrieves the DICOM Enabler's full details (GET .../modalities/{id}/), then assigns it
        /// (PATCH .../modalities/{id}/status/ assigned). Returns success, the enabler and the error.
        /// Runs off the UI thread.
        /// </summary>
        private static Tuple<bool, CareModality, string> AssignDicomEnabler(string modalitiesURL, string apiKey, string enablerId)
        {
            if (!ucls_CareModalities.TryRetrieveModality(modalitiesURL, apiKey, enablerId, LogDicomEnablerLoad, out CareModality enabler, out string error))
                return Tuple.Create(false, (CareModality)null, "The DICOM Enabler's details could not be loaded from CARE: " + error);
            if (!string.IsNullOrEmpty(enabler.device_type) && !enabler.IsDicomEnabler)
                return Tuple.Create(false, enabler, $"The selected device is a {enabler.device_type} device, not a DICOM Enabler.");
            // The Modality SCP and Store SCP settings come only from these endpoints, so it is not assigned without them
            if (!ucls_CareModalities.ValidateDicomEndpoints(enabler.modality_scp, enabler.store_scp, out error))
                return Tuple.Create(false, enabler, error + " Set its DICOM endpoints in CARE and select it again.");
            cls_AppLog.Info("DICOM ENABLER", $"Details of {enabler?.device_name} ({enablerId}) from CARE: " +
                $"Modality Worklist SCP {enabler?.modality_scp?.ae_title}:{enabler?.modality_scp?.port}, " +
                $"Store SCP {enabler?.store_scp?.ae_title}:{enabler?.store_scp?.port}, IP {enabler?.ip ?? "-"}, location {enabler?.location?.name ?? "-"}");
            if (!ucls_CareModalities.TryUpdateAssignmentStatus(modalitiesURL, apiKey, enablerId, ucls_CareModalities.AssignmentAssigned,
                                                               LogDicomEnablerLoad, out error))
                return Tuple.Create(false, enabler, "The DICOM Enabler could not be assigned in CARE: " + error);
            cls_AppLog.Info("DICOM ENABLER", $"{enabler?.device_name} ({enablerId}) assignment_status: unassigned -> assigned");
            return Tuple.Create(true, enabler, (string)null);
        }

        /// <summary>
        /// Sets a DICOM Enabler back to unassigned in CARE, as no enabler uses it any more. Returns a
        /// line for the message saying whether that worked (what names the enabler in it).
        /// </summary>
        private async System.Threading.Tasks.Task<string> UnassignDicomEnablerAsync(string facilityId, string enablerId, string what)
        {
            if (!TryGetModalitiesApi(facilityId, out string modalitiesURL, out string apiKey, out string error))
                return $"{what} could not be unassigned in CARE: {error}";
            string unassignError = null;
            bool ok = await System.Threading.Tasks.Task.Run(() => ucls_CareModalities.TryUpdateAssignmentStatus(
                modalitiesURL, apiKey, enablerId, ucls_CareModalities.AssignmentUnassigned, LogDicomEnablerLoad, out unassignError));
            string result = ok
                ? $"{what} ({enablerId}) is unassigned in CARE."
                : $"{what} ({enablerId}) could not be unassigned in CARE: {unassignError}. Unassign it in CARE.";
            if (ok)
                cls_AppLog.Info("DICOM ENABLER", $"{what} ({enablerId}) assignment_status: assigned -> unassigned");
            else
                cls_AppLog.Warning("DICOM ENABLER", result);
            return result;
        }

        // care_config key of the DICOM Enabler selected in the Configuration tab. Its details and DICOM
        // endpoints are its care_devices row, kept as CARE no longer returns them once it is assigned.
        private const string DicomEnablerIdKey = "dicom_enabler_id";
        // The saved DICOM Enabler's care_devices row, read when the Configuration tab loads
        private CareDeviceRecord savedEnabler;

        // The DICOM Enabler dropdown shows one page (ucls_CareModalities.PageSize) of the facility's
        // unassigned DICOM Enablers at a time; Prev / Next load the other pages from CARE.
        private DicomEnablerItem selectedDicomEnabler;
        private int dicomEnablerOffset;
        private int dicomEnablerTotal;
        // Increased by every load, so the result of an older load that finishes late is ignored
        private int dicomEnablerLoadVersion;
        // Set while the dropdown items are rebuilt, so that does not change selectedDicomEnabler
        private bool rebuildingDicomEnablerItems;

        /// <summary>
        /// A DICOM Enabler in the dropdown. Id is null for the "(None)" entry, which clears the selection.
        /// </summary>
        private sealed class DicomEnablerItem
        {
            public static readonly DicomEnablerItem None = new DicomEnablerItem(null, null, null);

            public DicomEnablerItem(string id, string name, string detail)
            {
                Id = id;
                Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
                Detail = detail;
            }

            public string Id { get; }
            public string Name { get; }
            public string Detail { get; }
            // Set for the saved enabler when it is not on the page CARE returned
            public bool NotOnPage { get; set; }

            public override string ToString()
            {
                if (Id == null)
                    return "(None)";
                string text = Name ?? Id;
                if (!string.IsNullOrEmpty(Detail))
                    text += " - " + Detail;
                if (NotOnPage)
                    text += " (saved)";
                return text;
            }
        }

        /// <summary>
        /// False when care_config has no dicom_enabler_id row yet (Initializer.bat not re-run since this
        /// setting was added), as Save only updates rows that exist.
        /// </summary>
        private bool IsDicomEnablerConfigAvailable()
        {
            return loadedConfigValues.ContainsKey(DicomEnablerIdKey);
        }

        /// <summary>
        /// Loads one page of the facility's unassigned DICOM Enablers from CARE into the dropdown:
        /// GET .../external/modalities/?care_type=dicom-enabler&assignment_status=unassigned&limit=14&offset=offset.
        /// The call runs in the background so the form stays responsive.
        /// </summary>
        private async void LoadDicomEnablerPage(int offset)
        {
            int loadVersion = ++dicomEnablerLoadVersion;

            if (!IsDicomEnablerConfigAvailable())
            {
                ShowDicomEnablers(null, "The DICOM Enabler setting is missing from the database. Run Initializer.bat to add it.");
                mcb_DicomEnabler.Enabled = false;
                mbtn_DicomEnablerRefresh.Enabled = false;
                return;
            }
            mcb_DicomEnabler.Enabled = true;

            string facilityId = mtxtb_FacilityId.Text.Trim();
            string baseUrl = (ConfigurationManager.AppSettings["careBackendURL"] ?? string.Empty).Trim();
            string modalitiesPath = ConfigurationManager.AppSettings["modalitiesURL"];
            string staticAPIKey = ConfigurationManager.AppSettings["staticAPIKey"] ?? string.Empty;

            string notLoadedReason = null;
            if (facilityId == string.Empty)
                notLoadedReason = "Enter the Facility Id and click Refresh to load the DICOM Enablers.";
            else if (baseUrl == string.Empty)
                notLoadedReason = "careBackendURL is not set in App.config - the DICOM Enablers cannot be loaded.";
            else if (string.IsNullOrWhiteSpace(staticAPIKey))
                notLoadedReason = "staticAPIKey is not set in App.config - the DICOM Enablers cannot be loaded.";
            if (notLoadedReason != null)
            {
                ShowDicomEnablers(null, notLoadedReason);
                mbtn_DicomEnablerRefresh.Enabled = true;
                return;
            }

            string modalitiesURL = ucls_CareModalities.BuildModalitiesURL(baseUrl, modalitiesPath, facilityId);
            SetDicomEnablerPagingEnabled(false, false);
            mbtn_DicomEnablerRefresh.Enabled = false;
            lbl_DicomEnablerPage.Text = "Loading DICOM Enablers from CARE...";

            var result = await System.Threading.Tasks.Task.Run(() =>
            {
                bool ok = ucls_CareModalities.TryFetchUnassignedDicomEnablersPage(
                    modalitiesURL, staticAPIKey, offset, LogDicomEnablerLoad, out CareModalitiesResponse page, out string error);
                return Tuple.Create(ok, page, error);
            });

            // The form was closed, or a newer load (Refresh, Prev, Next, tab reopened) replaced this one
            if (IsDisposed || loadVersion != dicomEnablerLoadVersion)
                return;
            mbtn_DicomEnablerRefresh.Enabled = true;

            if (!result.Item1)
            {
                ShowDicomEnablers(null, "Could not load the DICOM Enablers: " + result.Item3);
                return;
            }

            CareModalitiesResponse loadedPage = result.Item2;
            // Enablers assigned since the last page was loaded can leave this page empty: show the last page instead
            int lastPageOffset = Math.Max(0, (loadedPage.count - 1) / ucls_CareModalities.PageSize) * ucls_CareModalities.PageSize;
            if (loadedPage.results.Count == 0 && offset > 0 && lastPageOffset < offset)
            {
                LoadDicomEnablerPage(lastPageOffset);
                return;
            }

            dicomEnablerOffset = offset;
            dicomEnablerTotal = loadedPage.count;
            ShowDicomEnablers(loadedPage.results, null);
        }

        /// <summary>
        /// Fills the dropdown with "(None)", the selected enabler when it is not on this page, and the
        /// page's enablers, and selects the selected one. enablers null means no page was loaded:
        /// message then says why and paging is disabled.
        /// </summary>
        private void ShowDicomEnablers(List<CareModality> enablers, string message)
        {
            rebuildingDicomEnablerItems = true;
            try
            {
                mcb_DicomEnabler.Items.Clear();
                mcb_DicomEnabler.Items.Add(DicomEnablerItem.None);

                var pageItems = new List<DicomEnablerItem>();
                foreach (CareModality enabler in enablers ?? new List<CareModality>())
                {
                    if (string.IsNullOrWhiteSpace(enabler.id))
                        continue;
                    var details = new List<string>();
                    if (!string.IsNullOrWhiteSpace(enabler.location?.name))
                        details.Add(enabler.location.name.Trim());
                    if (!string.IsNullOrWhiteSpace(enabler.ip))
                        details.Add(enabler.ip.Trim());
                    pageItems.Add(new DicomEnablerItem(enabler.id.Trim(), enabler.device_name, string.Join(", ", details)));
                }

                DicomEnablerItem toSelect = DicomEnablerItem.None;
                if (selectedDicomEnabler != null)
                {
                    toSelect = pageItems.FirstOrDefault(item => string.Equals(item.Id, selectedDicomEnabler.Id, StringComparison.OrdinalIgnoreCase));
                    if (toSelect == null)
                    {
                        toSelect = new DicomEnablerItem(selectedDicomEnabler.Id, selectedDicomEnabler.Name, selectedDicomEnabler.Detail) { NotOnPage = true };
                        mcb_DicomEnabler.Items.Add(toSelect);
                    }
                }
                foreach (DicomEnablerItem item in pageItems)
                    mcb_DicomEnabler.Items.Add(item);
                mcb_DicomEnabler.SelectedItem = toSelect;
            }
            finally
            {
                rebuildingDicomEnablerItems = false;
            }

            if (enablers == null)
            {
                lbl_DicomEnablerPage.Text = message;
                SetDicomEnablerPagingEnabled(false, false);
                return;
            }

            int pageSize = ucls_CareModalities.PageSize;
            int totalPages = Math.Max(1, (dicomEnablerTotal + pageSize - 1) / pageSize);
            int currentPage = dicomEnablerOffset / pageSize + 1;
            lbl_DicomEnablerPage.Text = dicomEnablerTotal == 0
                ? "No unassigned DICOM Enablers in this facility."
                : $"Page {currentPage} of {totalPages} ({dicomEnablerTotal} unassigned DICOM Enabler{(dicomEnablerTotal == 1 ? "" : "s")})";
            SetDicomEnablerPagingEnabled(dicomEnablerOffset > 0, dicomEnablerOffset + pageSize < dicomEnablerTotal);
        }

        private void SetDicomEnablerPagingEnabled(bool previous, bool next)
        {
            mbtn_DicomEnablerPrev.Enabled = previous;
            mbtn_DicomEnablerNext.Enabled = next;
        }

        // The CARE Modalities API clients' log lines (requests, results, connection and authorization
        // errors) go to View Logs > App Log
        private static void LogDicomEnablerLoad(string message, bool success)
        {
            if (success)
                cls_AppLog.Info("MODALITIES", message);
            else
                cls_AppLog.Error("MODALITIES", message);
        }

        private void mcb_DicomEnabler_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (rebuildingDicomEnablerItems)
                return;
            var item = mcb_DicomEnabler.SelectedItem as DicomEnablerItem;
            selectedDicomEnabler = item == null || item.Id == null ? null : item;
        }

        private void mbtn_DicomEnablerRefresh_Click(object sender, EventArgs e)
        {
            LoadDicomEnablerPage(0);
        }

        private void mbtn_DicomEnablerPrev_Click(object sender, EventArgs e)
        {
            LoadDicomEnablerPage(Math.Max(0, dicomEnablerOffset - ucls_CareModalities.PageSize));
        }

        private void mbtn_DicomEnablerNext_Click(object sender, EventArgs e)
        {
            LoadDicomEnablerPage(dicomEnablerOffset + ucls_CareModalities.PageSize);
        }

        // The Devices tab lists the DICOM devices (modality machines such as CT or X-Ray, each with its own
        // modality) saved in care_devices for the DICOM Enabler saved in the Configuration tab, in a
        // scrollable grid; clicking a device shows its details. Opening the tab only reads care_devices.
        // Sync checks CARE for new devices and changes, and applies them only once the user confirms.
        private bool devicesSyncRunning;
        // Set when a DICOM Enabler was just assigned: the Devices tab syncs once it opens, so its devices
        // are offered for saving straight away
        private bool syncDevicesOnOpen;

        /// <summary>
        /// Shows the devices saved in care_devices for the saved DICOM Enabler. Does not call CARE and
        /// changes nothing: Sync does that, with confirmation.
        /// </summary>
        private void LoadDevices()
        {
            dgv_Devices.Rows.Clear();

            string errorString = string.Empty;
            string facilityId = objDAL.GetConfigValue("facility_id", string.Empty, ref errorString).Trim();
            string enablerId = string.IsNullOrEmpty(errorString) ? objDAL.GetConfigValue(DicomEnablerIdKey, string.Empty, ref errorString).Trim() : string.Empty;
            string enablerName = string.IsNullOrEmpty(errorString) && enablerId != string.Empty
                ? objDAL.GetCareDevice(enablerId, ref errorString)?.DeviceName?.Trim() ?? string.Empty
                : string.Empty;

            lbl_DevicesEnabler.Text = enablerId == string.Empty
                ? "DICOM Enabler: none selected"
                : "DICOM Enabler: " + (enablerName == string.Empty ? enablerId : enablerName);
            mbtn_DevicesSync.Enabled = !devicesSyncRunning && facilityId != string.Empty && enablerId != string.Empty;

            if (!string.IsNullOrEmpty(errorString))
            {
                lbl_DevicesStatus.Text = "Could not read the configuration: " + errorString;
                return;
            }
            if (facilityId == string.Empty || enablerId == string.Empty)
            {
                lbl_DevicesStatus.Text = facilityId == string.Empty
                    ? "Enter and save the Facility Id in the Configuration tab to see its devices."
                    : "Select and save a DICOM Enabler in the Configuration tab to see the devices connected to it.";
                return;
            }

            List<CareDeviceRecord> saved = objDAL.GetCareDevices(enablerId, ref errorString);
            if (!string.IsNullOrEmpty(errorString))
            {
                lbl_DevicesStatus.Text = "Could not read the saved devices: " + errorString;
                return;
            }
            ShowDevices(saved);
            if (saved.Count == 0)
            {
                lbl_DevicesStatus.Text = "No devices are saved for this DICOM Enabler yet. Click Sync to get them from CARE.";
            }
            else
            {
                DateTime? savedAt = saved.Max(d => d.UpdatedTime);
                lbl_DevicesStatus.Text += Environment.NewLine +
                    $"Saved{(savedAt.HasValue ? " on " + savedAt.Value.ToString("dd-MMM-yyyy HH:mm") : string.Empty)}. Click Sync to check CARE for new devices and changes.";
            }

            if (syncDevicesOnOpen)
            {
                syncDevicesOnOpen = false;
                SyncDevices("DICOM Enabler assigned");
            }
        }

        /// <summary>
        /// Checks CARE for new DICOM devices and changes to the saved ones, to the DICOM Enabler's entry and to
        /// the Modality SCP / Store SCP settings, then asks before applying them. Applying saves care_devices
        /// and care_config, writes the SCP settings and restarts the services that use a changed setting.
        /// </summary>
        private async void SyncDevices(string why)
        {
            if (devicesSyncRunning)
                return;
            devicesSyncRunning = true;
            mbtn_DevicesSync.Enabled = false;
            string previousStatus = lbl_DevicesStatus.Text;
            lbl_DevicesStatus.Text = "Checking CARE for new devices and changes...";
            try
            {
                CareSyncCheck check = await System.Threading.Tasks.Task.Run(() => cls_CareSync.Check(why));
                if (IsDisposed)
                    return;

                if (!check.Checked)
                {
                    lbl_DevicesStatus.Text = previousStatus;
                    MessageBox.Show(this, check.Error, "Sync with CARE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!check.HasChanges)
                {
                    lbl_DevicesStatus.Text = previousStatus;
                    string message = $"The {check.CareDevices.Count} device(s), the DICOM Enabler and the SCP settings match CARE. Nothing to update.";
                    if (check.Warnings.Count > 0)
                        message += Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, check.Warnings);
                    MessageBox.Show(this, message, "Sync with CARE", MessageBoxButtons.OK,
                                    check.Warnings.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                    return;
                }

                DialogResult answer = cls_ConfirmDialog.Show(this, "Sync with CARE - confirm changes",
                    "CARE has these changes for this DICOM Enabler. Nothing has been changed yet. Update the saved devices and settings?",
                    check.Describe(),
                    ("Update", DialogResult.Yes), ("Don't update", DialogResult.No));
                if (answer != DialogResult.Yes)
                {
                    cls_CareSync.LogDeclined(check, why);
                    lbl_DevicesStatus.Text = previousStatus + Environment.NewLine + "Changes from CARE not applied.";
                    return;
                }

                var scpChanges = new Dictionary<string, string>();
                bool applied = cls_CareSync.Apply(check, scpChanges, out string applyError);
                string restartSummary = scpChanges.Count > 0 ? RestartServicesForChanges(scpChanges) : string.Empty;
                foreach (string line in restartSummary.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    cls_AppLog.Info("SERVICES", line);

                LoadDevices();
                MessageBox.Show(this, (applied ? "The changes from CARE were saved." : "Not every change was saved: " + applyError) +
                                      (restartSummary == string.Empty ? string.Empty : Environment.NewLine + Environment.NewLine + restartSummary),
                                "Sync with CARE", MessageBoxButtons.OK, applied ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
            finally
            {
                devicesSyncRunning = false;
                if (!IsDisposed)
                    mbtn_DevicesSync.Enabled = true;
            }
        }

        /// <summary>Fills the grid, one row per device (Tag = its CARE device id), and summarises them by modality.</summary>
        private void ShowDevices(List<CareDeviceRecord> devices)
        {
            dgv_Devices.Rows.Clear();
            foreach (CareDeviceRecord device in devices)
            {
                string status = string.Join(" / ", new[] { device.Status, device.AvailabilityStatus }.Where(value => !string.IsNullOrWhiteSpace(value)));
                string lastSynced = DateTimeOffset.TryParse(device.LastSyncedAt, out DateTimeOffset synced)
                    ? synced.ToLocalTime().ToString("dd-MMM-yyyy HH:mm")
                    : "Never";
                int row = dgv_Devices.Rows.Add(
                    device.DeviceName,
                    ucls_CareModalities.DescribeModality(device.Modality),
                    device.AeTitle,
                    device.Ip,
                    device.LocationName,
                    status,
                    device.AssignmentStatus,
                    lastSynced);
                dgv_Devices.Rows[row].Tag = device.DeviceId;
            }
            dgv_Devices.ClearSelection();

            if (devices.Count == 0)
            {
                lbl_DevicesStatus.Text = "No devices are connected to this DICOM Enabler.";
                return;
            }
            string byModality = string.Join(", ", devices.GroupBy(d => string.IsNullOrWhiteSpace(d.Modality) ? "no modality" : d.Modality)
                                                         .Select(g => $"{g.Count()} {g.Key}"));
            lbl_DevicesStatus.Text = $"{devices.Count} device{(devices.Count == 1 ? "" : "s")} ({byModality}). Click a device to see its details.";
        }

        /// <summary>Clicking a device opens its details, from care_devices, read-only.</summary>
        private void dgv_Devices_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || !(dgv_Devices.Rows[e.RowIndex].Tag is string deviceId))
                return;
            string errorString = string.Empty;
            CareDeviceRecord device = objDAL.GetCareDevice(deviceId, ref errorString);
            if (device == null)
            {
                MessageBox.Show(this, string.IsNullOrEmpty(errorString)
                                    ? "This device is not saved in care_devices. Click Refresh to load the devices again."
                                    : "Could not read the device's details: " + errorString,
                                "Device Details", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string title = device.DeviceName ?? device.DeviceId;
            if (!string.IsNullOrWhiteSpace(device.Modality))
                title += " - " + ucls_CareModalities.DescribeModality(device.Modality);
            string note = device.UpdatedTime.HasValue ? "As CARE returned it on " + device.UpdatedTime.Value.ToString("dd-MMM-yyyy HH:mm") : null;
            ShowReadableDetails(title, device, note);
        }

        /// <summary>
        /// Shows the saved DICOM Enabler's full details, as CARE returned them when it was selected and assigned.
        /// </summary>
        private void mbtn_EnablerDetails_Click(object sender, EventArgs e)
        {
            string errorString = string.Empty;
            CareDeviceRecord enabler = cls_ConfigDefaults.GetSavedEnabler(objDAL, ref errorString);
            if (!string.IsNullOrEmpty(errorString) || enabler == null)
            {
                MessageBox.Show(this, !string.IsNullOrEmpty(errorString)
                                    ? "Could not read the DICOM Enabler's details: " + errorString
                                    : "No DICOM Enabler details are saved. Select a DICOM Enabler in the Configuration tab and save it.",
                                "DICOM Enabler Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ShowReadableDetails("DICOM Enabler " + (enabler.DeviceName ?? enabler.DeviceId), enabler,
                                "As CARE returned it on " + (enabler.UpdatedTime?.ToString("dd-MMM-yyyy HH:mm") ?? "-"));
        }

        /// <summary>
        /// A read-only window with a device's details as labelled fields grouped in sections (General,
        /// Modality Machine, Store SCP, ...). Nothing in it can be edited: the details come from CARE.
        /// </summary>
        private void ShowReadableDetails(string title, CareDeviceRecord device, string note)
        {
            List<string[]> rows = ucls_CareModalities.ReadableDetails(device);
            using (Form dialog = new Form())
            {
                dialog.Text = title + " - Details";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.Size = new Size(620, 560);
                dialog.Font = new Font("Segoe UI", 9.75F);
                dialog.BackColor = Color.White;

                var grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AllowUserToResizeRows = false,
                    RowHeadersVisible = false,
                    ColumnHeadersVisible = false,
                    SelectionMode = DataGridViewSelectionMode.CellSelect,
                    BackgroundColor = Color.White,
                    BorderStyle = BorderStyle.None,
                    CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                    GridColor = Color.Gainsboro,
                    AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                    ScrollBars = ScrollBars.Vertical
                };
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Field", Width = 210, SortMode = DataGridViewColumnSortMode.NotSortable });
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Value",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    DefaultCellStyle = { WrapMode = DataGridViewTriState.True }
                });
                grid.Columns[0].DefaultCellStyle.ForeColor = Color.DimGray;
                grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(232, 240, 254);
                grid.DefaultCellStyle.SelectionForeColor = Color.Black;
                grid.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);

                if (rows.Count == 0)
                    grid.Rows.Add("Details", "CARE returned no details for this device.");
                string section = null;
                foreach (string[] row in rows)
                {
                    if (row[0] != section)
                    {
                        section = row[0];
                        int header = grid.Rows.Add(section, string.Empty);
                        grid.Rows[header].DefaultCellStyle.Font = new Font(dialog.Font, FontStyle.Bold);
                        grid.Rows[header].DefaultCellStyle.ForeColor = Color.Black;
                        grid.Rows[header].DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
                        grid.Rows[header].Cells[0].Style.ForeColor = Color.Black;
                    }
                    grid.Rows.Add(row[1], row[2]);
                }
                dialog.Controls.Add(grid);

                var footer = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(12, 8, 12, 8) };
                var close = new Button { Text = "Close", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 90 };
                footer.Controls.Add(close);
                footer.Controls.Add(new Label
                {
                    Text = "Read-only" + (string.IsNullOrEmpty(note) ? string.Empty : " - " + note) + ". To change it, change it in CARE.",
                    Dock = DockStyle.Fill,
                    ForeColor = Color.DimGray,
                    TextAlign = ContentAlignment.MiddleLeft
                });
                dialog.Controls.Add(footer);
                dialog.AcceptButton = close;
                dialog.CancelButton = close;
                grid.ClearSelection();
                dialog.ShowDialog(this);
            }
        }

        private void mbtn_DevicesSync_Click(object sender, EventArgs e)
        {
            SyncDevices("Sync button");
        }

        // care_from_date is sent to the CARE worklist API as entered, in this format
        private const string ConfigDateFormat = "yyyy-MM-dd HH:mm:ss";

        private const string MwlServiceName = "Care MWL SCP Service";
        private const string StoreScpServiceName = "Care Store SCP Service";
        private const string StoreScuServiceName = "Care Store SCU Service";

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
                case "mwlaetitle":
                case "mwlhost":
                case "mwlport":
                    return new[] { MwlServiceName };
                case "sscpaetitle":
                case "sscphost":
                case "sscpport":
                    return new[] { StoreScpServiceName };
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
                current = cls_ConfigDefaults.GetConfigDefault("care_from_date");
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
                    current = cls_ConfigDefaults.GetConfigDefault(configKey);
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
    }
}
