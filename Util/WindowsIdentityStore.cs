using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Data.SQLite;

namespace TSVCEO.CloudPrint.Util
{
    public static class WindowsIdentityStore
    {
        #region enums

        private enum LogonType : int
        {
            LOGON32_LOGON_INTERACTIVE = 2,
            LOGON32_LOGON_NETWORK = 3,
            LOGON32_LOGON_BATCH = 4,
            LOGON32_LOGON_SERVICE = 5,
            LOGON32_LOGON_UNLOCK = 7,
            LOGON32_LOGON_NETWORK_CLEARTEXT = 8,
            LOGON32_LOGON_NEW_CREDENTIALS = 9
        }

        private enum LogonProvider : int
        {
            LOGON32_PROVIDER_DEFAULT = 0,
            LOGON32_PROVIDER_WINNT35 = 1,
            LOGON32_PROVIDER_WINNT40 = 2,
            LOGON32_PROVIDER_WINNT50 = 3
        }

        private enum JoinStatus : int
        {
            Unknown = 0,
            UnJoined = 1,
            Workgroup = 2,
            Domain = 3
        }

        #endregion

        #region private properties / fields

        private static string Domain { get; set; }
        private static Dictionary<string, WindowsIdentity> IdentityCache { get; set; }
        private static Dictionary<string, SecureString> CredentialCache { get; set; }
        private static SQLiteConnection CredentialDatabase { get; set; }
        
        #endregion

        #region constructor
        
        static WindowsIdentityStore()
        {
            Domain = GetDomainName();
            IdentityCache = new Dictionary<string, WindowsIdentity>();
            CredentialCache = new Dictionary<string, SecureString>();
            CredentialDatabase = new SQLiteConnection("Data Source=" + Config.CredentialDatabaseFilename);
            CredentialDatabase.Open();
            InitCredentialsTable();
        }
        
        #endregion

        #region interop methods
        
        [DllImport("netapi32.dll", SetLastError = true)]
        private static extern int NetGetJoinInformation(string computerName, out IntPtr buffer, out JoinStatus status);

        [DllImport("netapi32.dll", SetLastError = true)]
        private static extern int NetApiBufferFree(IntPtr buffer);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LogonUser(string username, string domain, [In] IntPtr password, LogonType logonType, LogonProvider logonProvider, out IntPtr token);
        
        #endregion

        #region private methods
        
        private static bool LogonUser(string username, string domain, SecureString password, LogonType logonType, LogonProvider logonProvider, out IntPtr token)
        {
            if (password == null)
            {
                throw new ArgumentNullException("password");
            }

            IntPtr plainpassword = IntPtr.Zero;
            try
            {
                plainpassword = Marshal.SecureStringToGlobalAllocUnicode(password);
                return LogonUser(username, domain, plainpassword, logonType, logonProvider, out token);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(plainpassword);
            }
        }

        private static string GetDomainName()
        {
            IntPtr pDomain;
            JoinStatus joinStatus;
            string domain = null;
            int status = NetGetJoinInformation(null, out pDomain, out joinStatus);

            if (status == 0 && joinStatus == JoinStatus.Domain)
            {
                domain = Marshal.PtrToStringAuto(pDomain);
            }

            if (pDomain != IntPtr.Zero)
            {
                NetApiBufferFree(pDomain);
            }

            return domain;
        }

        private static string ProtectUsername(string username)
        {
            MD5 md5 = MD5.Create();
            byte[] usernamedata = Encoding.UTF8.GetBytes(username);
            md5.Initialize();
            md5.TransformFinalBlock(usernamedata, 0, usernamedata.Length);
            return Convert.ToBase64String(md5.Hash).Replace("=", "");
        }

        private static SecureString GetUserCredential(string username)
        {
            if (CredentialCache.ContainsKey(username))
            {
                return CredentialCache[username];
            }

            string obusername = ProtectUsername(username);
            byte[] enccred = GetDatabaseCredentials(obusername);
            if (enccred != null)
            {
                try
                {
                    byte[] cred = ProtectedData.Unprotect(enccred, null, DataProtectionScope.LocalMachine);
                    if (cred != null)
                    {
                        SecureString secstr = new SecureString();

                        foreach (byte c in cred)
                        {
                            secstr.AppendChar((char)c);
                        }

                        secstr.MakeReadOnly();

                        return secstr;
                    }
                }
                catch (CryptographicException)
                {
                    InvalidateUserCredentials(username);
                }
            }

            return null;
        }

        private static void SetUserCredential(string username, SecureString password)
        {
            CredentialCache[username] = password;
            string obusername = ProtectUsername(username);
            IntPtr credptr = IntPtr.Zero;

            try
            {
                credptr = Marshal.SecureStringToGlobalAllocAnsi(password);
                byte[] cred = new byte[password.Length];
                Marshal.Copy(credptr, cred, 0, cred.Length);
                byte[] enccred = ProtectedData.Protect(cred, null, DataProtectionScope.LocalMachine);
                SetDatabaseCredentials(obusername, enccred);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocAnsi(credptr);
            }
        }

        private static void InvalidateUserCredentials(string username)
        {
            if (CredentialCache.ContainsKey(username))
            {
                CredentialCache.Remove(username);
            }

            DeleteDatabaseCredentials(ProtectUsername(username));
        }

        private static byte[] GetDatabaseCredentials(string obusername)
        {
            var cmd = CredentialDatabase.CreateCommand();
            cmd.CommandText = "SELECT encpassword FROM credentials WHERE obusername = @obusername";
            cmd.Parameters.AddWithValue("@obusername", obusername);
            var result = cmd.ExecuteScalar();
            return result == null ? null : Convert.FromBase64String(result.ToString());
        }

        private static void SetDatabaseCredentials(string obusername, byte[] credential)
        {
            var cmd = CredentialDatabase.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO credentials (obusername, encpassword) VALUES (@obusername, @encpassword)";
            cmd.Parameters.AddWithValue("@obusername", obusername);
            cmd.Parameters.AddWithValue("@encpassword", Convert.ToBase64String(credential));
            cmd.ExecuteNonQuery();
        }

        private static void DeleteDatabaseCredentials(string obusername)
        {
            var cmd = CredentialDatabase.CreateCommand();
            cmd.CommandText = "DELETE FROM credentials WHERE obusername = @obusername";
            cmd.Parameters.AddWithValue("@obusername", obusername);
            cmd.ExecuteNonQuery();
        }

        private static void InitCredentialsTable()
        {
            var cmd = CredentialDatabase.CreateCommand();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS credentials (obusername TEXT PRIMARY KEY, encpassword TEXT)";
            cmd.ExecuteNonQuery();
        }
        
        #endregion

        #region public methods

        public static WindowsIdentity GetWindowsIdentity(string username)
        {
            SecureString password = GetUserCredential(username);
            if (password != null)
            {
                try
                {
                    WindowsIdentity ident;
                    if (IdentityCache.ContainsKey(username))
                    {
                        ident = IdentityCache[username];
                    }
                    else
                    {
                        ident = Login(username, password);
                    }

                    if (ident != null)
                    {
                        using (ident.Impersonate())
                        {
                            return ident;
                        }
                    }
                }
                catch
                {
                }

                if (IdentityCache.ContainsKey(username))
                {
                    IdentityCache.Remove(username);
                }

                InvalidateUserCredentials(username);
            }

            return null;
        }

        public static bool HasWindowsIdentity(string username)
        {
            return GetWindowsIdentity(username) != null;
        }

        public static WindowsImpersonationContext Impersonate(string username)
        {
            return GetWindowsIdentity(username).Impersonate();
        }

        public static Process CreateProcessAsUser(string username, ProcessStartInfo startinfo)
        {
            Process proc = new Process();
            ProcessStartInfo _startinfo = new ProcessStartInfo
            {
                Arguments = startinfo.Arguments,
                CreateNoWindow = startinfo.CreateNoWindow,
                Domain = Domain,
                ErrorDialog = startinfo.ErrorDialog,
                ErrorDialogParentHandle = startinfo.ErrorDialogParentHandle,
                FileName = startinfo.FileName,
                LoadUserProfile = startinfo.LoadUserProfile,
                Password = GetUserCredential(username),
                RedirectStandardError = startinfo.RedirectStandardError,
                RedirectStandardInput = startinfo.RedirectStandardInput,
                RedirectStandardOutput = startinfo.RedirectStandardOutput,
                StandardErrorEncoding = startinfo.StandardErrorEncoding,
                StandardOutputEncoding = startinfo.StandardOutputEncoding,
                UserName = username,
                UseShellExecute = startinfo.UseShellExecute,
                Verb = startinfo.Verb,
                WindowStyle = startinfo.WindowStyle,
                WorkingDirectory = startinfo.WorkingDirectory
            };
            proc.StartInfo = _startinfo;
            return proc;
        }

        public static WindowsIdentity Login(string username, SecureString password)
        {
            IntPtr token;
            if (LogonUser(username, Domain, password, LogonType.LOGON32_LOGON_NETWORK, LogonProvider.LOGON32_PROVIDER_DEFAULT, out token))
            {
                WindowsIdentity ident = new WindowsIdentity(token);
                IdentityCache[username] = ident;
                SetUserCredential(username, password);
                return ident;
            }
            else
            {
                IdentityCache.Remove(username);
                InvalidateUserCredentials(username);
                return null;
            }
        }

        public static bool IsAcceptedDomain(string domain)
        {
            if (Config.CloudPrintAcceptDomains == null)
            {
                return false;
            }
            else
            {
                return Config.CloudPrintAcceptDomains.Contains(domain);
            }
        }

        #endregion
    }
}
