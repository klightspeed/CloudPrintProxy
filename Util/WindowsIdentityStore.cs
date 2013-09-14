using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Diagnostics;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace TSVCEO.CloudPrint.Util
{
    public static class WindowsIdentityStore
    {
        #region enums

        private enum LogonType : int
        {
            Interactive = 2,
            Network = 3,
            Batch = 4,
            Service = 5,
            Unlock = 7,
            Cleartext = 8,
            NewCredentials = 9
        }

        private enum LogonProvider : int
        {
            Default = 0,
            Winnt35 = 1,
            Winnt40 = 2,
            Winnt50 = 3
        }

        [Flags]
        private enum LogonFlags : int
        {
            WithProfile = 1,
            NetcredentialsOnly = 2
        }

        private enum JoinStatus : int
        {
            Unknown = 0,
            UnJoined = 1,
            Workgroup = 2,
            Domain = 3
        }

        [Flags]
        private enum StartFlags : int
        {
            UseShowWindow    = 0x00000001,
            UseSize          = 0x00000002,
            UsePosition      = 0x00000004,
            UseCountChars    = 0x00000008,
            UseFillAttribute = 0x00000010,
            RunFullscreen    = 0x00000020,
            ForceOnFeedback  = 0x00000040,
            ForceOffFeedback = 0x00000080,
            UseStdHandles    = 0x00000100,
            UseHotkey        = 0x00000200,
            TitleIsLinkName  = 0x00000800,
            TitleIsAppId     = 0x00001000,
            PreventPinning   = 0x00002000
        }

        private enum ShowWindow : int
        {
            Hide,
            ShowNormal,
            ShowMinimized,
            ShowMaximized,
            ShowNoActive,
            Show,
            Minimize,
            ShowMinNoActive,
            ShowNA,
            Restore,
            ShowDefault,
            ForceMinimize
        }

        [Flags]
        private enum ProcessCreationFlags : int
        {
            DebugProcess                 = 0x00000001,
            DebugOnlyThisProcess         = 0x00000002,
            CreateSuspended              = 0x00000004,
            DetachedProcess              = 0x00000008,
            CreateNewConsole             = 0x00000010,
            CreateNewProcessGroup        = 0x00000200,
            CreateUnicodeEnvironment     = 0x00000400,
            CreateSeparateWowVdm         = 0x00000800,
            CreateSharedWowVdm           = 0x00001000,
            InheritParentAffinity        = 0x00010000,
            CreateProtectedProcess       = 0x00040000,
            ExtendedStartupInfoPresent   = 0x00080000,
            CreateBreakawayFromJob       = 0x01000000,
            CreatePreserveCodeAuthzLevel = 0x02000000,
            CreateDefaultErrorMode       = 0x04000000,
            CreateNoWindow               = 0x08000000
        }

        [Flags]
        public enum WindowStationRights
        {
            EnumDesktops       = 0x00000001,
            ReadAttributes     = 0x00000002,
            AccessClipboard    = 0x00000004,
            CreateDesktop      = 0x00000008,
            WriteAttributes    = 0x00000010,
            AccessGlobalAtoms  = 0x00000020,
            ExitWindows        = 0x00000040,
            Enumerate          = 0x00000100,
            ReadScreen         = 0x00000200,
            Delete             = 0x00010000,
            ReadPermissions    = 0x00020000,
            WritePermissions   = 0x00040000,
            TakeOwnership      = 0x00080000,
            Synchronize        = 0x00100000,

            AllAccess = EnumDesktops | ReadAttributes | AccessClipboard | CreateDesktop | WriteAttributes | AccessGlobalAtoms | ExitWindows | Enumerate | ReadScreen | Delete | ReadPermissions | WritePermissions | TakeOwnership | Synchronize
        }

        [Flags]
        public enum DesktopRights
        {
            ReadObjects       = 0x00000001,
            CreateWindow      = 0x00000002,
            CreateMenu        = 0x00000004,
            HookControl       = 0x00000008,
            JournalRecord     = 0x00000010,
            JournalPlayback   = 0x00000020,
            Enumerate         = 0x00000040,
            WriteObjects      = 0x00000080,
            SwitchDesktop     = 0x00000100,
            Delete            = 0x00010000,
            ReadPermissions   = 0x00020000,
            WritePermissions  = 0x00040000,
            TakeOwnership     = 0x00080000,
            Synchronize       = 0x00100000,
            AllAccess = ReadObjects | CreateWindow | CreateMenu | HookControl | JournalRecord | JournalPlayback | Enumerate | WriteObjects | SwitchDesktop | Delete | ReadPermissions | WritePermissions | TakeOwnership | Synchronize
        }

        #endregion

        #region interop handles

        private class GenericSafeHandle : SafeHandle
        {
            private Func<IntPtr, bool> _ReleaseCallback;

            public GenericSafeHandle(IntPtr handle, Func<IntPtr, bool> releasecallback)
                : base(IntPtr.Zero, true)
            {
                this._ReleaseCallback = releasecallback;
                this.handle = handle;
            }

            protected override bool ReleaseHandle()
            {
                return _ReleaseCallback == null ? true : _ReleaseCallback(handle);
            }

            public override bool IsInvalid { get { return handle == IntPtr.Zero; } }
        }

        private class GenericObjectSecurity<T> : ObjectSecurity<T>
            where T: struct
        {
            private GenericSafeHandle handle;

            public GenericObjectSecurity(bool isContainer, ResourceType restype, GenericSafeHandle handle, AccessControlSections sections)
                : base(isContainer, restype, handle, sections)
            {
                this.handle = handle;
            }

            public void Commit()
            {
                Persist(handle);
            }

            public IEnumerable<AccessRule<T>> GetAccessRules()
            {
                return GetAccessRules(true, true, typeof(SecurityIdentifier)).OfType<AccessRule<T>>();
            }
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
        
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetProcessWindowStation();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetThreadDesktop(int dwThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int GetCurrentThreadId();

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

        private static void AddUserToCurrentWindowStationDesktop(string username)
        {
            IntPtr winsta = GetProcessWindowStation();
            IntPtr desktop = GetThreadDesktop(GetCurrentThreadId());
            SecurityIdentifier ident = GetWindowsIdentity(username).User;

            GenericObjectSecurity<WindowStationRights> winsec = new GenericObjectSecurity<WindowStationRights>(false, ResourceType.WindowObject, new GenericSafeHandle(winsta, null), AccessControlSections.Access);
            if (winsec.GetAccessRules().Where(r => r.IdentityReference == ident).Count() == 0)
            {
                winsec.AddAccessRule(new AccessRule<WindowStationRights>(ident, WindowStationRights.AllAccess, AccessControlType.Allow));
                winsec.Commit();
            }

            GenericObjectSecurity<DesktopRights> desksec = new GenericObjectSecurity<DesktopRights>(false, ResourceType.WindowObject, new GenericSafeHandle(desktop, null), AccessControlSections.Access);
            if (desksec.GetAccessRules().Where(r => r.IdentityReference == ident).Count() == 0)
            {
                desksec.AddAccessRule(new AccessRule<DesktopRights>(GetWindowsIdentity(username).User, DesktopRights.AllAccess, AccessControlType.Allow));
                desksec.Commit();
            }
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

        public static NTAccount GetWindowsIdentityReference(string username)
        {
            return new NTAccount((Domain == null ? "" : Domain + "\\") + username);
        }

        public static void SetFileAccess(string filename, string username)
        {
            try
            {
                //Type privilegeType = Type.GetType("System.Security.AccessControl.Privilege");
                //object privilege = Activator.CreateInstance(privilegeType, "SeBackupPrivilege");

                NTAccount idref = GetWindowsIdentityReference(username);
                FileSecurity fs = File.GetAccessControl(filename, AccessControlSections.Access);
                //fs.SetOwner(idref);
                fs.AddAccessRule(new FileSystemAccessRule(idref, FileSystemRights.FullControl, AccessControlType.Allow));

                //privilegeType.GetMethod("Enable").Invoke(privilege, null);
                File.SetAccessControl(filename, fs);
                //privilegeType.GetMethod("Revert").Invoke(privilege, null);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Info, "Error setting owner on file {0} to {1}:\n{2}", filename, username, ex.ToString());
            }
        }

        public static bool HasWindowsIdentity(string username)
        {
            return GetWindowsIdentity(username) != null;
        }

        public static WindowsImpersonationContext Impersonate(string username)
        {
            return GetWindowsIdentity(username).Impersonate();
        }

        public static int RunProcessAsUser(string username, Stream stdin, Stream stdout, Stream stderr, string exename, string[] args)
        {
            AddUserToCurrentWindowStationDesktop(username);

            return ProcessHelper.RunProcessAsUser(username, Domain, GetUserCredential(username), stdin, stdout, stderr, exename, args);
        }

        public static int RunProcessAsUser(string username, TextReader stdin, TextWriter stdout, TextWriter stderr, string exename, string[] args)
        {
            AddUserToCurrentWindowStationDesktop(username);

            return ProcessHelper.RunProcessAsUser(username, Domain, GetUserCredential(username), stdin, stdout, stderr, exename, args);
        }

        public static WindowsIdentity Login(string username, SecureString password)
        {
            IntPtr token;
            if (LogonUser(username, Domain, password, LogonType.Network, LogonProvider.Default, out token))
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
