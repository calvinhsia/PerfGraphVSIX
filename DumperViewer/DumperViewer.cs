using PerfGraphVSIX;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace DumperViewer
{
    public class DumperViewer : ILogger
    {
        private readonly string[] args;
        private int _pid;
        private readonly List<string> regexes = new List<string>();
        private string _DumpFileName;
        internal ILogger _logger;

        [STAThread]
        public static void Main(string[] args)
        {
            var oDumper = new DumperViewer(args);
            oDumper.DoMain();
        }

        internal void DoMain()
        {
            AsyncPump.Run(async () =>
            {
                await DoitAsync();
            }
            );
        }

        public DumperViewer(string[] args)
        {
            this._logger = this;
            this.args = args;
        }

        private async Task DoitAsync()
        {
            if (args.Length == 0)
            {
                await Task.Delay(1);
                DoShowHelp();
                return;
            }
            await SendTelemetryAsync($"{nameof(DumperViewer)}");
            _logger.LogMessage($"in {nameof(DumperViewer)}  args = {string.Join(" ", args)}");
            int iArg = 0;
            var argsGood = true;
            var extraErrInfo = string.Empty;
            try
            {
                while (iArg < args.Length)
                {
                    var curArg = args[iArg++];
                    if (curArg.Length > 1 && "-/".IndexOf(curArg[0]) == 0)
                    {
                        switch (curArg[1].ToString().ToLower())
                        {
                            case "p":
                                if (iArg == args.Length)
                                {
                                    throw new ArgumentException("Expected process id");
                                }
                                this._pid = int.Parse(args[iArg++]);
                                break;
                            case "r":
                                if (iArg == args.Length)
                                {
                                    throw new ArgumentException("Expected regex");
                                }
                                var splitRegExes = args[iArg++].Split(new[] { '|' });
                                foreach (var split in splitRegExes)
                                {
                                    this.regexes.Add(split);
                                }
                                break;
                            case "f":
                                if (iArg == args.Length)
                                {
                                    throw new ArgumentException("dump filename");
                                }
                                this._DumpFileName = args[iArg++];
                                break;
                            case "d":
                                break;
                        }
                    }
                    else
                    {
                        argsGood = false;
                    }
                }
            }
            catch (Exception ex)
            {
                argsGood = false;
                extraErrInfo = ex.ToString();
            }
            if (!argsGood)
            {
                DoShowHelp(extraErrInfo);
            }
        }

        private void DoShowHelp(string extrainfo = "")
        {
            var owin = new Window();
            var tb = new TextBlock()
            {
                Text = $@"DumperViewer: 
{extrainfo}

Create a dump of the speficied process, analyze the dump for the counts of the specified types, 
Command line:
DumpViewer -p 1234 -t .*TextBuffer.*
-p <pid>   Process id of process. Will take a dump of the specified process (e.g. Devenv). Devenv cannot take a dump of itself because it will result in deadlock 
        (creating a dump involves freezing threads) and continue executing launch DumperViewer
        (The creation of the dump and the subsequent analysis needs to be fast)
-i  <n>    iteration #. The dump and the results will be tagged with the iteration prefix. E.G. iterating 700 times will yield dump0.dmp, dump1.dmp,...dump700.dmp.
-t <regex><|regex>  a '|' separated list of regular expressions that specify what types the caller is 'interested in'. e.g. '.*textbuffer.*'|'.*codelens.*'
     can be '|' separated or there can be multiple '-t' arguments

-d <dumpname>  Show the WPF UI treeview for the dump (ClrObjectExplorer)


"
            };

            owin.Content = tb;
            owin.ShowDialog();
        }

        public void LogMessage(string msg, params object[] args)
        {
            if (!_logger.Equals(this))
            {
                _logger.LogMessage(msg, args);
            }

        }
        async static  public Task<string> SendTelemetryAsync(string msg, params object[] args)
        {
            var result = string.Empty;
            try
            {
                //var exe = Process.GetCurrentProcess().ProcessName.ToLowerInvariant(); // like windbg or clrobjexplorer or vstest.executionengine.x86
                if (Environment.GetEnvironmentVariable("username") != "calvinhsss")
                {

                    var mTxt = args != null ? string.Format(msg, args) : msg;
                    mTxt = System.Web.HttpUtility.UrlEncode(mTxt);

                    var baseurl = "http://calvinh6/PerfGraph.asp?";
                    var url = string.Format("{0}{1}", baseurl, mTxt);

                    await Task.Run(() =>
                    {
                        var wclient = new WebClient();
                        wclient.UseDefaultCredentials = true;
                        //                    wclient.Credentials = CredentialCache.DefaultCredentials;
                        result = wclient.DownloadString(url);
                    });
                }
            }
            catch (Exception)
            {
                //                LogString("Telemetry exception {0}", ex);
            }
            return result;
        }

    }
}
