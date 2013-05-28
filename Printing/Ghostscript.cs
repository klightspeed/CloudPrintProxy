using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Printing;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Security.AccessControl;
using TSVCEO.CloudPrint.Util;

namespace TSVCEO.CloudPrint.Printing
{
    public class Ghostscript : JobPrinter
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        protected string EscapeCommandLineArgument(string arg)
        {
            StringBuilder sb = new StringBuilder();
            StringReader rdr = new StringReader(arg);
            int c;
            sb.Append('"');

            while ((c = rdr.Read()) > 0)
            {
                if (c == '"')
                {
                    sb.Append("\\\"");
                }
                else if (c == '\\')
                {
                    int nrbackslash = 1;

                    while (rdr.Peek() == '\\')
                    {
                        nrbackslash++;
                        rdr.Read();
                    }

                    if (rdr.Peek() == '"')
                    {
                        sb.Append(new String('\\', nrbackslash * 2));
                    }
                    else
                    {
                        sb.Append(new String('\\', nrbackslash));
                    }
                }
                else
                {
                    sb.Append((char)c);
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        protected string CreateCommandArguments(string[] args)
        {
            return String.Join(" ", args.Select(s => EscapeCommandLineArgument(s)).ToArray());
        }

        protected ProcessStartInfo CreateStartInfo(string command, string[] args)
        {
            return new ProcessStartInfo
            {
                Arguments = CreateCommandArguments(args),
                FileName = command,
                CreateNoWindow = true,
                ErrorDialog = false,
                LoadUserProfile = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                UseShellExecute = false,
                WorkingDirectory = Environment.GetEnvironmentVariable("SYSTEMDRIVE") + "\\"
            };
        }

        protected virtual int RunCommandAsUser(string username, string[] args, Stream stdin, Stream stdout, Stream stderr)
        {
            string gsexepath = Path.Combine(GetGhostscriptPath("gswin32c.exe"));
            using (Process proc = WindowsIdentityStore.CreateProcessAsUser(username, CreateStartInfo(gsexepath, args)))
            {
                Task proctask = Task.Factory.StartNew(() =>
                {
                    proc.Start();
                    proc.WaitForExit();
                });
                Task stdintask = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        proc.StandardInput.Write(new StreamReader(stdin, Encoding.UTF8, false).ReadToEnd());
                    }
                    catch
                    {
                    }
                });

                proctask.Wait();
                proc.StandardInput.Close();
                string stdoutstr = proc.StandardOutput.ReadToEnd();
                string stderrstr = proc.StandardError.ReadToEnd();
                byte[] stdoutdata = Encoding.UTF8.GetBytes(stdoutstr);
                stdout.Write(stdoutdata, 0, stdoutdata.Length);
                byte[] stderrdata = Encoding.UTF8.GetBytes(stderrstr);
                stderr.Write(stderrdata, 0, stderrdata.Length);
                return proc.ExitCode;
            }
        }

        protected string EscapePostscriptString(string str)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(");

            foreach (char c in str)
            {
                if (c >= 0377)
                {
                    sb.Append("\\377");
                }
                else if (c < ' ' || c >= 0x7F)
                {
                    sb.AppendFormat("\\{0}", Convert.ToString((int)c, 8));
                }
                else if (c == '\\' || c == '(' || c == ')')
                {
                    sb.AppendFormat("\\{0}", c);
                }
                else
                {
                    sb.Append(c);
                }
            }

            sb.Append(")");
            return sb.ToString();
        }

        protected IEnumerable<string> SetPageDeviceCommand(PrintTicket ticket)
        {
            yield return "<<";

            double width = (ticket.PageMediaSize.Width ?? (210 * 96)) * 72.0 / 96.0;
            double height = (ticket.PageMediaSize.Height ?? (297 * 96)) * 72.0 / 96.0;

            /*
            yield return "/PageSize";
            yield return "[";
            yield return width.ToString();
            yield return height.ToString();
            yield return "]";
             */

            if (ticket.PageMediaType != null && ticket.PageMediaType != PageMediaType.Unknown)
            {
                yield return "/MediaType";
                yield return EscapePostscriptString(ticket.PageMediaType.ToString());
            }

            if (ticket.InputBin != null && ticket.InputBin == InputBin.Manual)
            {
                yield return "/ManualFeed";
                yield return "true";
            }

            if (ticket.Collation != null && ticket.Collation != Collation.Unknown)
            {
                yield return "/Collate";
                yield return ticket.Collation == Collation.Collated ? "true" : "false";
            }

            if (ticket.CopyCount != null)
            {
                yield return "/NumCopies";
                yield return ticket.CopyCount.ToString();
            }

            if (ticket.Duplexing != null && ticket.Duplexing != Duplexing.Unknown)
            {
                yield return "/Duplex";
                yield return ticket.Duplexing == Duplexing.OneSided ? "true" : "false";
                yield return "/Tumble";
                yield return ticket.Duplexing == Duplexing.TwoSidedShortEdge ? "true" : "false";
            }

            /*
            if (ticket.PageResolution.X != null && ticket.PageResolution.Y != null)
            {
                yield return "/HWResolution";
                yield return "[";
                yield return ticket.PageResolution.X.ToString();
                yield return ticket.PageResolution.Y.ToString();
                yield return "]";
            }
             */

            if (ticket.PageOrientation != null && ticket.PageOrientation != PageOrientation.Unknown)
            {
                int orientation = 0;
                bool pagesizelandscape = width > height;

                switch (ticket.PageOrientation)
                {
                    case PageOrientation.Portrait: orientation = pagesizelandscape ? 3 : 0; break;
                    case PageOrientation.Landscape: orientation = pagesizelandscape ? 0 : 1; break;
                    case PageOrientation.ReversePortrait: orientation = pagesizelandscape ? 2 : 1; break;
                    case PageOrientation.ReverseLandscape: orientation = pagesizelandscape ? 2 : 3; break;
                }

                yield return "/Orientation";
                yield return orientation.ToString();
            }

            if (ticket.OutputColor != null && ticket.OutputColor != OutputColor.Unknown && ticket.OutputColor != OutputColor.Color)
            {
                yield return "/ProcessColorModel";
                yield return "/DeviceGray";
                yield return "/BitsPerPixel";
                yield return (ticket.OutputColor == OutputColor.Grayscale) ? "8" : "1";
            }

            yield return ">>";
            yield return "setpagedevice";
        }

        protected IEnumerable<string> SetDeviceCommand(string printername, string jobname)
        {
            yield return "mark";
            yield return "/NoCancel";
            yield return "true";
            yield return "/OutputFile";
            yield return EscapePostscriptString("%printer%" + printername);
            yield return "/UserSettings";
            yield return "<<";
            yield return "/DocumentName";
            yield return EscapePostscriptString(jobname);
            yield return ">>";
            yield return "(mswinpr2)";
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

        protected void PrintData(string username, PrintTicket ticket, string printername, string jobname, Stream datastream)
        {
            string jobfilename = null;

            try
            {
                string tmpdir = Path.Combine(Environment.GetEnvironmentVariable("SystemRoot"), "Temp");
                SetupUserPrinter(username, printername);
                string[] setup = SetPageDeviceCommand(ticket).ToArray();
                string basename = Path.Combine(tmpdir, Guid.NewGuid().ToString());
                jobfilename = basename + ".pdf";

                using (Stream jobfile = File.Open(jobfilename, FileMode.CreateNew))
                {
                    datastream.CopyTo(jobfile);
                }

                var jobfilesecurity = File.GetAccessControl(jobfilename);
                jobfilesecurity.AddAccessRule(new FileSystemAccessRule(WindowsIdentityStore.GetWindowsIdentity(username).User, FileSystemRights.ReadAndExecute, AccessControlType.Allow));
                File.SetAccessControl(jobfilename, jobfilesecurity);

                string[] args = new string[]
                {
                    "-dNOPAUSE",
                    "-dBATCH",
                    "-dNOSAFER",
                    "-c"
                }.Concat(SetDeviceCommand(printername, jobname))
                 .Concat(SetPageDeviceCommand(ticket))
                 .Concat(new string[]
                {
                    "-f",
                    jobfilename
                }).ToArray();

                MemoryStream outstream = new MemoryStream();
                MemoryStream errstream = new MemoryStream();
                MemoryStream instream = new MemoryStream(new byte[0]);

                int exitcode = RunCommandAsUser(username, args, instream, outstream, errstream);

                string outstr = Encoding.UTF8.GetString(outstream.ToArray());
                string errstr = Encoding.UTF8.GetString(errstream.ToArray());

                if (exitcode != 0)
                {
                    Logger.Log(LogLevel.Warning, "Ghostscript returned code {0}\n\nOutput:\n{1}\n\nError:\n{2}", exitcode, outstr, errstr);
                    throw new InvalidOperationException(String.Format("Ghostscript error {0}\n{1}", exitcode, errstr));
                }
            }
            finally
            {
                if (jobfilename != null && File.Exists(jobfilename))
                {
                    File.Delete(jobfilename);
                }
            }
        }

        #endregion

        #region public methods

        public override void Print(CloudPrintJob job)
        {
            PrintTicket printTicket = job.GetPrintTicket();
            Stream printDataStream = job.GetPrintDataStream();
            PrintData(job.Username, printTicket, job.Printer.Name, job.JobTitle, printDataStream);
        }

        #endregion
    }
}
