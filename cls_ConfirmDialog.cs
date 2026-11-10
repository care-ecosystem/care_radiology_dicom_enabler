using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Plexus_DICOM_Enabler
{
    /// <summary>
    /// Asks the user to confirm changes before they are made: a window with what is about to change, one
    /// line per change, and a button for each choice. Used before anything from CARE or the Configuration
    /// tab is written to the database or to the settings the services use.
    /// </summary>
    public static class cls_ConfirmDialog
    {
        /// <summary>
        /// Shows the changes and returns the DialogResult of the button clicked, or Cancel when the window
        /// is closed. buttons are (text, result) pairs, shown left to right; the first is the default.
        /// </summary>
        public static DialogResult Show(IWin32Window owner, string title, string intro, IEnumerable<string> lines, params (string Text, DialogResult Result)[] buttons)
        {
            using (var dialog = new Form())
            {
                dialog.Text = title;
                dialog.StartPosition = owner == null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.Size = new Size(700, 480);
                dialog.MinimumSize = new Size(480, 320);
                dialog.Font = new Font("Segoe UI", 9.75F);
                dialog.BackColor = Color.White;

                var introLabel = new Label
                {
                    Text = intro,
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 56,
                    Padding = new Padding(12, 10, 12, 4)
                };
                var list = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    BackColor = Color.FromArgb(248, 248, 248),
                    BorderStyle = BorderStyle.FixedSingle,
                    ScrollBars = ScrollBars.Vertical,
                    WordWrap = true,
                    Text = string.Join("\r\n", (lines ?? Enumerable.Empty<string>()).Select(line => line.StartsWith("  ") ? line : "- " + line))
                };
                var listPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 0) };
                listPanel.Controls.Add(list);

                var buttonPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 52,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Padding(8, 10, 8, 8)
                };
                // RightToLeft: add the last button first so they read left to right
                Button first = null;
                foreach (var (text, result) in buttons.Reverse())
                {
                    var button = new Button { Text = text, DialogResult = result, AutoSize = true, MinimumSize = new Size(110, 30) };
                    buttonPanel.Controls.Add(button);
                    first = button;
                }

                dialog.Controls.Add(listPanel);
                dialog.Controls.Add(buttonPanel);
                dialog.Controls.Add(introLabel);
                dialog.AcceptButton = first;
                list.SelectionLength = 0;

                DialogResult chosen = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
                return buttons.Any(b => b.Result == chosen) ? chosen : DialogResult.Cancel;
            }
        }
    }
}
