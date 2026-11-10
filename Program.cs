using Plexus_DICOM_Enabler.Forms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Plexus_DICOM_Enabler
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Global._applicationPath = Path.GetDirectoryName(Application.ExecutablePath);
            cls_GeneralLog.Info("APP", "CARE DICOM Enabler started (version " + Application.ProductVersion + ")");
            try
            {
                Application.Run(new frm_LoginScreen());
            }
            finally
            {
                cls_GeneralLog.Info("APP", "CARE DICOM Enabler closed");
                cls_GeneralLog.Close();
            }
        }
    }
}
