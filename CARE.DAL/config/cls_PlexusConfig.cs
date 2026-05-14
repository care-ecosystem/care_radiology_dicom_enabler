using System.IO;
using System.Reflection;
using System.Xml;

namespace Plexus.Common.config
{
    public static class cls_PlexusConfig
    {
        /// <summary>
        /// Save Details to XML
        /// </summary>
        /// <param name="tagName"></param>
        /// <param name="updateText"></param>
        public static void SaveDetailsToXML(string applicationPath, string tagName, string updateText)
        {
            string xmlPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "cfg/common.cfg");
            XmlDocument configDoc = new XmlDocument();
            configDoc.Load(xmlPath);

            XmlNode userNode = configDoc.SelectSingleNode(tagName);
            if (userNode != null)
            {
                userNode.InnerText = updateText;
            }
            configDoc.Save(xmlPath);
        }

        public static string ReadDetailsFromXML(string applicationPath,string tagName)
        {
            string returnVal = string.Empty;
            string xmlPath = Path.Combine(applicationPath, "cfg/common.cfg");
            XmlDocument configDoc = new XmlDocument();
            configDoc.Load(xmlPath);
            XmlNode sRetNode = configDoc.SelectSingleNode(tagName);
            if (sRetNode != null )
            {
                returnVal = sRetNode.InnerText;
            }

            return returnVal;
        }
    }
}
