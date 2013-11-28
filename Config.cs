using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Configuration;
using System.Xml.Linq;
using System.Reflection;
using System.IO;
using System.Threading;
using System.Collections.Specialized;
using System.Net.NetworkInformation;

namespace TSVCEO.CloudPrint
{
    internal static class Config
    {
        #region private properties

        private static Mutex ConfigFileMutex { get; set; }
        private static string AppDataDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Townsville Catholic Education Office", "CloudPrint"); } }

        #endregion

        #region static settings

        public static string OAuthClientID { get { return ConfigurationManager.AppSettings["OAuthClientID"]; } }
        public static string OAuthClientSecret { get { return ConfigurationManager.AppSettings["OAuthClientSecret"]; } }
        public static string OAuthRedirectURI { get { return ConfigurationManager.AppSettings["OAuthRedirectURI"]; } }

        public static string CloudPrintOAuthScope { get { return ConfigurationManager.AppSettings["CloudPrintOAuthScope"]; } }
        public static string CloudPrintBaseURL { get { return ConfigurationManager.AppSettings["CloudPrintBaseURL"]; } }
        public static string CloudPrintUserAgent { get { return ConfigurationManager.AppSettings["CloudPrintUserAgent"]; } }
        public static string CloudPrintProxyName { get { return ConfigurationManager.AppSettings["CloudPrintProxyName"]; } }
        public static string[] CloudPrintAcceptDomains { get { return ConfigurationManager.AppSettings["CloudPrintAcceptDomains"].Split(','); } }

        public static string WebProxyHost { get { return ConfigurationManager.AppSettings["WebProxyHost"]; } }
        public static int WebProxyPort { get { return Int32.Parse(ConfigurationManager.AppSettings["WebProxyPort"] ?? "0"); } }

        public static string XMPPResourceName { get { return ConfigurationManager.AppSettings["XMPPResourceName"]; } }
        public static string XMPPHost { get { return ConfigurationManager.AppSettings["XMPPHost"] ?? "talk.google.com"; } }
        public static int XMPPPort { get { return Int32.Parse(ConfigurationManager.AppSettings["XMPPPort"] ?? "5222"); } }

        public static int UserAuthHttpPort { get { return Int32.Parse(ConfigurationManager.AppSettings["UserAuthHttpPort"] ?? "12387"); } }
        public static string UserAuthHost { get { return ConfigurationManager.AppSettings["UserAuthHost"] ?? Environment.MachineName; } }

        public static string DataDirName { get { return GetAppDataDirFilename("Data"); } }

        public static string CredentialDatabaseFilename { get { return GetAppDataDirFilename(ConfigurationManager.AppSettings["CredentialDatabaseFilename"]); } }
        public static string SessionDatabaseFilename { get { return GetAppDataDirFilename(ConfigurationManager.AppSettings["SessionDatabaseFilename"]); } }

        public static NameValueCollection GhostscriptPrinterDrivers { get { return ConfigurationManager.GetSection("ghostscriptPrinterDrivers") as NameValueCollection ?? new NameValueCollection(); } }

        public static PrinterConfigurationSection PrinterConfigurationSection { get { return ConfigurationManager.GetSection("printerConfiguration") as PrinterConfigurationSection; } }
        
        private static string ConfigFileName { get { return GetAppDataDirFilename(ConfigurationManager.AppSettings["VolatileConfigFilename"]); } }

        #endregion

        #region dynamic settings
        
        public static string XMPP_JID { get { return ReadSettingString("XMPP_JID"); } set { WriteSettingString("XMPP_JID", value); } }
        public static string CloudPrintProxyID { get { return ReadSettingString("CloudPrintProxyID"); } set { WriteSettingString("CloudPrintProxyID", value); } }
        public static string OAuthRefreshToken { get { return ReadSettingString("OAuthRefreshToken"); } set { WriteSettingString("OAuthRefreshToken", value); } }
        public static string OAuthEmail { get { return ReadSettingString("OAuthEmail"); } set { WriteSettingString("OAuthEmail", value); } }
        public static bool OAuthCodeAccepted { get { return ReadSettingString("OAuthCodeAccepted") == "true"; } set { WriteSettingString("OAuthCodeAccepted", value ? "true" : null); } }
        public static string GhostscriptPath { get { return ReadSettingString("GhostscriptPath"); } set { WriteSettingString("GhostscriptPath", value); } }
        
        #endregion

        #region constructor
        
        static Config()
        {
            Directory.CreateDirectory(AppDataDir);
            ConfigFileMutex = new Mutex(false, "TCEOCloudPrintProxyConfigFileMutex");
            if (CloudPrintProxyID == null)
            {
                CloudPrintProxyID = Guid.NewGuid().ToString();
            }
        }
        
        #endregion

        #region private methods

        private static string GetAppDataDirFilename(string filename)
        {
            return Path.Combine(AppDataDir, filename);
        }

        private static T LockConfigFile<T>(Func<T> action)
        {
            bool hasmutex = false;
            try
            {
                hasmutex = ConfigFileMutex.WaitOne();
                return action();
            }
            finally
            {
                if (hasmutex)
                {
                    ConfigFileMutex.ReleaseMutex();
                }
            }
        }

        private static void LockConfigFile(Action action)
        {
            LockConfigFile<object>(() => 
                { 
                    action(); 
                    return null; 
                }
            );
        }

        private static XDocument ReadConfigFileNoLock()
        {
            if (File.Exists(ConfigFileName))
            {
                using (var stream = File.Open(ConfigFileName, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return XDocument.Load(stream);
                }
            }
            else
            {
                return new XDocument(new XElement("settings"));
            }
        }
        
        private static XDocument ReadConfigFile()
        {
            return LockConfigFile(() => ReadConfigFileNoLock());
        }

        private static void UpdateConfigFile(Action<XDocument> updater)
        {
            LockConfigFile(() =>
                {
                    XDocument doc = ReadConfigFileNoLock();
                    updater(doc);
                    string tempname = GetAppDataDirFilename(Path.GetRandomFileName());
                    
                    using (var stream = File.Open(tempname, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        doc.Save(stream);
                    }

                    try
                    {
                        if (File.Exists(ConfigFileName))
                        {
                            File.Replace(tempname, ConfigFileName, null);
                        }
                        else
                        {
                            File.Move(tempname, ConfigFileName);
                        }
                    }
                    finally
                    {
                        File.Delete(tempname);
                    }
                }
            );
        }

        private static T ReadSetting<T>(string settingname, Func<XElement, T> reader)
        {
            var doc = ReadConfigFile();
            var root = doc.Element("settings");
            return reader(root.Elements("setting").Where(xe => xe.Attribute("name").Value == settingname).SingleOrDefault());
        }

        private static void WriteSetting(string settingname, Action<XElement> writer)
        {
            UpdateConfigFile((doc) =>
                {
                    XElement root = doc.Element("settings");
                    XElement el = root.Elements("setting").Where(xe => xe.Attribute("name").Value == settingname).SingleOrDefault();
                    if (el == null)
                    {
                        el = new XElement("setting", new XAttribute("name", settingname));
                        root.Add(el);
                    }
                    writer(el);
                }
            );
        }

        private static void DeleteSetting(string settingname)
        {
            UpdateConfigFile((doc) =>
                {
                    XElement el = doc.Element("settings").Elements("setting").Where(xe => xe.Attribute("name").Value == settingname).SingleOrDefault();
                    if (el != null)
                    {
                        el.Remove();
                    }
                }
            );
        }

        private static string ReadSettingString(string settingname)
        {
            return ReadSetting(settingname, (xe) => xe == null ? null : xe.Value);
        }

        private static void WriteSettingString(string settingname, string settingvalue)
        {
            if (settingvalue == null)
            {
                DeleteSetting(settingname);
            }
            else
            {
                WriteSetting(settingname, (xe) => xe.Value = settingvalue);
            }
        }

        #endregion
    }
}
