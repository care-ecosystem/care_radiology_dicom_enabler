using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Threading.Tasks;

namespace Plexus_SCU_Service
{
    [RunInstaller(true)]
    public partial class CAREStoreSCU_Installer : System.Configuration.Install.Installer
    {
        public CAREStoreSCU_Installer()
        {
            InitializeComponent();
        }
    }
}
