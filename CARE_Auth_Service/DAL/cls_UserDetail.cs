using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plexus_Auth_Service
{
    class cls_UserDetail
    {
        public string name { get; set; }
        public string firstName { get; set; }
        public string middleName { get; set; }

        public string lastName { get; set; }
        public string status { get; set; }

        public string role { get; set; }

        public string profileFilePath { get; set; }

        public string permission { get; set; }

        public string accessToken { get; set; }

        public string tokenType { get; set; }
    }
}
