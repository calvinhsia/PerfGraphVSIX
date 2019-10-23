using PerfGraphVSIX;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace DumperViewer
{
    public class DumperViewer : ILogger
    {
        private readonly string[] args;
        internal readonly List<string> regexes = new List<string>();
        internal string _DumpFileName;
        internal ILogger _logger;
        internal Process _procTarget;

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
            _logger.LogMessage($"in {nameof(DumperViewer)}  LoggerObj={_logger.ToString()} args = {string.Join(" ", args)}");
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
                                var pid = int.Parse(args[iArg++]);
                                _procTarget = Process.GetProcessById(pid);
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
                                if (this._DumpFileName.StartsWith("\"") && this._DumpFileName.EndsWith("\""))
                                {
                                    this._DumpFileName = this._DumpFileName.Replace("\"", string.Empty);
                                }
                                this._DumpFileName = Path.ChangeExtension(this._DumpFileName, "dmp");
                                if (File.Exists(this._DumpFileName))
                                {
                                    throw new InvalidOperationException($"{this._DumpFileName} already exists. Aborting");
                                }
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
                return;
            }
            try
            {
                var sw = Stopwatch.StartNew();
                await Task.Run(() =>
                {
                    var mdh = new MemoryDumpHelper();
                    _logger.LogMessage($"Starting to create dump {_procTarget.Id} {_procTarget.ProcessName}");
                    if (_procTarget.Id == Process.GetCurrentProcess().Id)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            Marshal.CleanupUnusedObjectsInCurrentContext();
                        }
                    }
                    mdh.CollectDump(process: _procTarget, dumpFilePath: _DumpFileName, fIncludeFullHeap: true);
                });
                _logger.LogMessage($"Done creating dump {_procTarget.Id} {_procTarget.ProcessName} {new FileInfo(_DumpFileName).Length:n0}   Secs={sw.Elapsed.TotalSeconds:f3}");

                sw.Restart();
                await Task.Run(() =>
                {
                    LogMessage($"Loading dump in DumpAnalyzer {_DumpFileName}");
                    var x = new DumpAnalyzer(this);
//                    x.AnalyzeDump();
                    x.StartClrObjectExplorer(_DumpFileName);
                });
                _logger.LogMessage($"Done Analyzing dump {_procTarget.Id} {_procTarget.ProcessName}  Secs={sw.Elapsed.TotalSeconds:f3}");
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"exception creating dump {ex.ToString()}");
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
This EXE will optionally show UI. 

Command line:
DumpViewer -p 1234 -t .*TextBuffer.*
-p <pid>   Process id of process. Will take a dump of the specified process (e.g. Devenv). Devenv cannot take a dump of itself because it will result in deadlock 
        (creating a dump involves freezing threads) and continue executing launch DumperViewer
        (The creation of the dump and the subsequent analysis needs to be fast)
-i  <n>    iteration #. The dump and the results will be tagged with the iteration prefix. E.G. iterating 700 times will yield dump0.dmp, dump1.dmp,...dump700.dmp.
-t <regex><|regex>  a '|' separated list of regular expressions that specify what types the caller is 'interested in'. e.g. '.*textbuffer.*'|'.*codelens.*'
     can be '|' separated or there can be multiple '-t' arguments

-f <FileDumpname>  Base path of output. Will change Extension to '.dmp' for dump output, '.log' for log output. Can be quoted.

-u  UI: Show the WPF UI treeview for the dump (ClrObjectExplorer). 


"
            };

            owin.Content = tb;
            owin.ShowDialog();
        }

        readonly List<string> _lstLoggedStrings = new List<string>();

        public void LogMessage(string str, params object[] args)
        {
            if (!_logger.Equals(this))
            {
                _logger.LogMessage(str, args);
            }
            else
            {
                var dt = string.Format("[{0}],",
                    DateTime.Now.ToString("hh:mm:ss:fff")
                    );
                str = string.Format(dt + str, args);
                var msgstr = DateTime.Now.ToString("hh:mm:ss:fff") + $" {Thread.CurrentThread.ManagedThreadId} {str}";

                if (Debugger.IsAttached)
                {
                    Debug.WriteLine(msgstr);
                }
                _lstLoggedStrings.Add(msgstr);
            }
        }
        async static public Task<string> SendTelemetryAsync(string msg, params object[] args)
        {
            var result = string.Empty;
            try
            {
                //var exe = Process.GetCurrentProcess().ProcessName.ToLowerInvariant(); // like windbg or clrobjexplorer or vstest.executionengine.x86
                if (Environment.GetEnvironmentVariable("username") != "calvinh")
                {

                    var mTxt = args != null ? string.Format(msg, args) : msg;
                    mTxt = System.Web.HttpUtility.UrlEncode(mTxt);

                    var baseurl = "http://calvinh6/PerfGraph.asp?";
                    var url = string.Format("{0}{1}", baseurl, mTxt);

                    await Task.Run(() =>
                    {
                        var wclient = new WebClient
                        {
                            UseDefaultCredentials = true
                        };
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
