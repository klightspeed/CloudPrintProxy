using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Security;
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

        private static Process CreateProcessAsUser(string username, string domain, SecureString password, string workdir, string exename, string[] args)
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

        public static int RunProcessAsUser(string username, string domain, SecureString password, Stream stdin, Stream stdout, Stream stderr, string workdir, string exename, string[] args)
        {
            return RunProcessAsUser(username, domain, password, new StreamReader(stdin, UTF8, false), new StreamWriter(stdout, UTF8), new StreamWriter(stderr, UTF8), workdir, exename, args);
        }

        public static int RunProcessAsUser(string username, string domain, SecureString password, TextReader stdin, TextWriter stdout, TextWriter stderr, string workdir, string exename, string[] args)
        {
            using (Process proc = CreateProcessAsUser(username, domain, password, workdir, exename, args))
            {
                proc.Start();
                Task stdintask = Task.Factory.StartNew(() => { try { proc.StandardInput.Write(stdin.ReadToEnd()); } catch { } });
                Task stdouttask = Task.Factory.StartNew(() => { try { stdout.Write(proc.StandardOutput.ReadToEnd()); } catch { } });
                Task stderrtask = Task.Factory.StartNew(() => { try { stderr.Write(proc.StandardError.ReadToEnd()); } catch { } });
                proc.WaitForExit();
                stdintask.Wait();
                stdouttask.Wait();
                stderrtask.Wait();
                stdout.Write(proc.StandardOutput.ReadToEnd());
                stderr.Write(proc.StandardError.ReadToEnd());
                stdout.Flush();
                stderr.Flush();
                return proc.ExitCode;
            }
        }

        public static int RunProcess(Stream stdin, Stream stdout, Stream stderr, string workdir, string exename, string[] args)
        {
            return RunProcessAsUser(null, null, null, stdin, stdout, stderr, workdir, exename, args);
        }

        public static int RunProcess(TextReader stdin, TextWriter stdout, TextWriter stderr, string workdir, string exename, string[] args)
        {
            return RunProcessAsUser(null, null, null, stdin, stdout, stderr, workdir, exename, args);
        }
    }
}
