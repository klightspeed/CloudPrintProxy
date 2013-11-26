using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace TSVCEO.CloudPrint.Util
{
    public class ProcessHelper
    {
        public static UTF8Encoding UTF8
        {
            get
            {
                return new UTF8Encoding(false, false);
            }
        }

        public static string EscapeCommandLineArgument(string arg)
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

        public static string CreateCommandArguments(string[] args)
        {
            return String.Join(" ", args.Select(s => EscapeCommandLineArgument(s)).ToArray());
        }

        public static Process CreateProcessAsUser(string username, string domain, SecureString password, string workdir, string exename, string[] args)
        {
            Process proc = new Process();

            ProcessStartInfo startinfo = new ProcessStartInfo
            {
                Arguments = CreateCommandArguments(args),
                FileName = exename,
                CreateNoWindow = true,
                ErrorDialog = false,
                LoadUserProfile = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = UTF8,
                StandardErrorEncoding = UTF8,
                UseShellExecute = false,
                WorkingDirectory = workdir,
                UserName = username,
                Domain = domain,
                Password = password
            };

            proc.StartInfo = startinfo;

            return proc;
        }

        protected static void CopyStream(Stream instream, Stream outstream)
        {
            try
            {
                instream.CopyTo(outstream);
                outstream.Flush();
            }
            catch
            {
            }
        }

        public static int RunProcessAsUser(string username, string domain, SecureString password, Stream stdin, Stream stdout, Stream stderr, string workdir, string exename, string[] args)
        {
            using (Process proc = CreateProcessAsUser(username, domain, password, workdir, exename, args))
            {
                Thread stdinthread = new Thread(new ThreadStart(() => CopyStream(stdin, proc.StandardInput.BaseStream)));
                Thread stdoutthread = new Thread(new ThreadStart(() => CopyStream(proc.StandardOutput.BaseStream, stdout)));
                Thread stderrthread = new Thread(new ThreadStart(() => CopyStream(proc.StandardError.BaseStream, stderr)));

                proc.Start();
                stdinthread.Start();
                stdoutthread.Start();
                stderrthread.Start();

                proc.WaitForExit();
                proc.StandardInput.BaseStream.Close();
                proc.StandardOutput.BaseStream.Close();
                proc.StandardError.BaseStream.Close();

                stdoutthread.Join();
                stderrthread.Join();

                stdout.Flush();
                stderr.Flush();

                return proc.ExitCode;
            }
        }

        public static int RunProcess(Stream stdin, Stream stdout, Stream stderr, string workdir, string exename, string[] args)
        {
            return RunProcessAsUser(null, null, null, stdin, stdout, stderr, workdir, exename, args);
        }
    }
}
