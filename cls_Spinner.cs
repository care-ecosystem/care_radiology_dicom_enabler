using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Plexus_DICOM_Enabler
{
    /// <summary>
    /// A small spinning loader shown beside a button while its work (e.g. a call to CARE) is running.
    /// Hidden until Start; Stop hides it again. Drawn in the app's theme colour.
    /// </summary>
    [DesignerCategory("Code")]
    public class cls_Spinner : Control
    {
        private readonly Timer timer = new Timer { Interval = 40 };
        private int angle;

        public cls_Spinner()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Color.FromArgb(4, 108, 78);
            Size = new Size(24, 24);
            TabStop = false;
            Visible = false;
            timer.Tick += (sender, e) =>
            {
                angle = (angle + 30) % 360;
                Invalidate();
            };
        }

        /// <summary>True while the spinner is shown and turning.</summary>
        public bool Spinning => timer.Enabled;

        public void Start()
        {
            angle = 0;
            Visible = true;
            BringToFront();
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
            Visible = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float thickness = Math.Max(2f, Math.Min(Width, Height) / 8f);
            var bounds = new RectangleF(thickness / 2, thickness / 2, Width - thickness - 1, Height - thickness - 1);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;
            using (var track = new Pen(Color.FromArgb(50, ForeColor), thickness))
                e.Graphics.DrawEllipse(track, bounds);
            using (var arc = new Pen(ForeColor, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                e.Graphics.DrawArc(arc, bounds, angle, 100);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
