using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Printing;
using Microsoft.Win32;
using System.Security.AccessControl;

namespace TSVCEO.CloudPrint.Util
{
    public class Ghostscript : IDisposable
    {
        #region constructor / destructor

        public Ghostscript()
        {
        }

        ~Ghostscript()
        {
            Dispose(false);
        }

        #endregion

        #region protected methods

        protected static string GetGhostscriptPath(string filename)
        {
            string gspath = Config.GhostscriptPath;

            if (gspath == null || !File.Exists(gspath))
            {
                foreach (string envname in new string[] { "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432" })
                {
                    string dir = Environment.GetEnvironmentVariable(envname);
                    if (dir != null && Directory.Exists(dir) && Directory.Exists(Path.Combine(dir, "gs")))
                    {
                        foreach (string gsverdir in Directory.GetDirectories(Path.Combine(dir, "gs")))
                        {
                            if (File.Exists(Path.Combine(gsverdir, "bin", filename)))
                            {
                                gspath = Path.Combine(gsverdir, "bin", filename);
                                Config.GhostscriptPath = gspath;
                            }
                        }
                    }
                }
            }

            return gspath;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        protected virtual int RunCommand(string[] args, Stream stdin, Stream stdout, Stream stderr)
        {
            string gsexepath = GetGhostscriptPath("gswin32c.exe");
            return ProcessHelper.RunProcess(stdin, stdout, stderr, Path.GetDirectoryName(gsexepath), gsexepath, args);
        }

        protected virtual int RunCommandAsUser(string username, string[] args, Stream stdin, Stream stdout, Stream stderr)
        {
            string gsexepath = GetGhostscriptPath("gswin32c.exe");
            return WindowsIdentityStore.RunProcessAsUser(username, stdin, stdout, stderr, Path.GetDirectoryName(gsexepath), gsexepath, args);
        }

        protected IEnumerable<string> SetDeviceCommand(string outputfilename, string jobname, string driver)
        {
            yield return "mark";
            yield return "/NoCancel";
            yield return "true";
            yield return "/OutputFile";
            yield return PostscriptHelper.EscapePostscriptString(outputfilename);
            yield return "/UserSettings";
            yield return "<<";
            yield return "/DocumentName";
            yield return PostscriptHelper.EscapePostscriptString(jobname);
            yield return ">>";
            yield return PostscriptHelper.EscapePostscriptString(driver);
            yield return "finddevice";
            yield return "putdeviceprops";
            yield return "setdevice";
            yield return ".setsafe";
        }

        protected string GeneratePrinterPort(RegistryKey regkey)
        {
            int portnum = 0;
            Regex re = new Regex("^winspool,ne([0-9][0-9]):$");
            foreach (string name in regkey.GetValueNames())
            {
                string driver_port = regkey.GetValue(name, null) as string;
                Match match;

                if (driver_port != null && (match = re.Match(driver_port)).Success)
                {
                    int curportnum = Convert.ToInt32(match.Groups[1].Value, 10);
                    if (curportnum >= portnum)
                    {
                        portnum = curportnum + 1;
                    }
                }
            }

            return String.Format("Ne{0:D2}:", portnum);
        }

        protected string GetPrinterPort(string printername)
        {
            using (var regkey = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Print\Printers\" + printername))
            {
                string portname = regkey.GetValue("Port", null) as string;

                if (portname != null && portname.Length <= 5 && portname.EndsWith(":"))
                {
                    return portname;
                }

                return null;
            }
        }

        protected void SetupUserPrinter(string username, string printername)
        {
            var ident = WindowsIdentityStore.GetWindowsIdentity(username);
            string reguser = ".DEFAULT";
            string usersid = ident.User.Value;

            if (Registry.Users.GetSubKeyNames().Contains(usersid))
            {
                reguser = usersid;
            }

            string devregpath = reguser + @"\Software\Microsoft\Windows NT\CurrentVersion\Devices";

            using (var devregkey = Registry.Users.CreateSubKey(devregpath))
            {
                if (devregkey.GetValue(printername, null) == null)
                {
                    string portname = GetPrinterPort(printername);

                    if (portname == null)
                    {
                        portname = GeneratePrinterPort(devregkey);
                    }

                    devregkey.SetValue(printername, "winspool," + portname);
                }
            }
        }
        
        protected void ProcessData(string username, PrintTicket ticket, string tempfile, string datafile, string[] inargs, string[] extraargs, string[] devsetup)
        {
            string[] pagesetup = PostscriptHelper.SetPageDeviceCommand(ticket).ToArray();

            string[] args = new string[] { "-dNOPAUSE", "-dBATCH" }
                .Concat(inargs ?? new string[] { })
                .Concat(extraargs ?? new string[] { })
                .Concat(new string[] { "-c" })
                .Concat(devsetup ?? new string[] { })
                .Concat(pagesetup)
                .Concat(new string[] { "-f", datafile })
                .ToArray();

            File.WriteAllText(datafile + ".args", ProcessHelper.CreateCommandArguments(args));

            MemoryStream outstream = new MemoryStream();
            MemoryStream errstream = new MemoryStream();
            MemoryStream instream = new MemoryStream(new byte[0]);

            int exitcode;
            if (username != null)
            {
                exitcode = RunCommandAsUser(username, args, instream, outstream, errstream);
            }
            else
            {
                exitcode = RunCommand(args, instream, outstream, errstream);
            }

            string outstr = Encoding.UTF8.GetString(outstream.ToArray());
            string errstr = Encoding.UTF8.GetString(errstream.ToArray());

            if (exitcode != 0)
            {
                Logger.Log(LogLevel.Warning, "Ghostscript returned code {0}\n\nOutput:\n{1}\n\nError:\n{2}", exitcode, outstr, errstr);
                throw new InvalidOperationException(String.Format("Ghostscript error {0}\n{1}", exitcode, errstr));
            }
        }

        protected byte[] ProcessData(string username, PrintTicket ticket, byte[] data, string[] inargs, string[] extraargs, string[] devsetup)
        {
            string[] pagesetup = PostscriptHelper.SetPageDeviceCommand(ticket).ToArray();

            string[] args = new string[] { "-dNOPAUSE", "-dBATCH" }
                .Concat(inargs ?? new string[] { })
                .Concat(extraargs ?? new string[] { })
                .Concat(new string[] { "-sOutputFile=%stdout" })
                .Concat(new string[] { "-c" })
                .Concat(devsetup ?? new string[] { })
                .Concat(pagesetup)
                .Concat(new string[] { "-" })
                .ToArray();

            MemoryStream outstream = new MemoryStream();
            MemoryStream errstream = new MemoryStream();
            MemoryStream instream = new MemoryStream(data);

            int exitcode;
            if (username != null)
            {
                exitcode = RunCommandAsUser(username, args, instream, outstream, errstream);
            }
            else
            {
                exitcode = RunCommand(args, instream, outstream, errstream);
            }

            string errstr = Encoding.UTF8.GetString(errstream.ToArray());

            if (exitcode != 0)
            {
                Logger.Log(LogLevel.Warning, "Ghostscript returned code {0}\n\nError:\n{1}", exitcode, errstr);
                throw new InvalidOperationException(String.Format("Ghostscript error {0}\n{1}", exitcode, errstr));
            }

            return outstream.ToArray();
        }

        #endregion

        #region public methods

        public void PrintData(string username, PrintTicket ticket, string printername, string jobname, string datafile, string[] inargs)
        {
            string[] devsetup = SetDeviceCommand("%printer%" + printername, jobname, "mswinpr2").ToArray();
            string[] extraargs = new string[] { "-dNOSAFER" };

            SetupUserPrinter(username, printername);

            ProcessData(username, ticket, null, datafile, inargs, extraargs, devsetup);
        }

        public void PrintData(string username, PrintTicket ticket, string printername, string jobname, byte[] data, string[] inargs)
        {
            string[] devsetup = SetDeviceCommand("%printer%" + printername, jobname, "mswinpr2").ToArray();
            string[] extraargs = new string[] { "-dNOSAFER" };

            SetupUserPrinter(username, printername);

            ProcessData(username, ticket, data, inargs, extraargs, devsetup);
        }

        public void ProcessData(PrintTicket ticket, string tempfile, string datafile, string driver, string[] inargs, string[] devsetup)
        {
            string[] extraargs = new string[] { "-sDEVICE=" + driver, "-sOutputFile=" + tempfile };

            ProcessData(null, ticket, tempfile, datafile, inargs, extraargs, devsetup);
        }

        public byte[] ProcessData(PrintTicket ticket, byte[] data, string driver, string[] inargs, string[] devsetup)
        {
            string[] extraargs = new string[] { "-sDEVICE=" + driver };

            return ProcessData(null, ticket, data, inargs, extraargs, devsetup);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
