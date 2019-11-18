using System;
using System.Collections.Generic;
using System.ComponentModel;
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


namespace StressTestUtility
{
    public class DumperViewerMain : ILogger
    {
        private readonly string[] args;
        internal readonly List<string> regexes = new List<string>();
        internal string _DumpFileName;
        public ILogger _logger;
        internal Process _procTarget;
        private bool _StartClrObjExplorer;

        [STAThread]
        public static void Main(string[] args)
        {
            var oDumper = new DumperViewerMain(args);
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

        public DumperViewerMain(string[] args)
        {
            this._logger = this;
            this.args = args;
        }

        public async Task DoitAsync()
        {
            await SendTelemetryAsync($"{nameof(DumperViewerMain)}");
            _logger.LogMessage($"in {nameof(DumperViewerMain)}  LoggerObj={_logger.ToString()} args = '{string.Join(" ", args)}'");

            if (args.Length == 0)
            {
                _procTarget = ChooseProcess("Choose a process to dump/analyze", fShow32BitOnly: true);
                _StartClrObjExplorer = true;
                if (_procTarget == null)
                {
                    DoShowHelp();
                    return;
                }
            }
            else
            {
                int iArg = 0;
                var argsGood = true;
                var extraErrInfo = string.Empty;
                try
                {
                    while (iArg < args.Length)
                    {
                        var curArg = args[iArg++].Trim();
                        if (curArg.Length > 1 && "-/".IndexOf(curArg[0]) == 0)
                        {
                            switch (curArg[1].ToString().ToLower())
                            {
                                case "?":
                                    DoShowHelp();
                                    return;
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
                                case "c": // start ClrObjExplorer 
                                    this._StartClrObjExplorer = true;
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
                            _logger.LogMessage($"Invalid arguments");
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
            }
            try
            {
                var sw = Stopwatch.StartNew();
                if (string.IsNullOrEmpty(_DumpFileName))
                {
                    throw new InvalidOperationException("Must specify dump filename");
                }
                await Task.Run(() =>
                {
                    var mdh = new MemoryDumpHelper();
                    _logger.LogMessage($"Starting to create dump {_procTarget.Id} {_procTarget.ProcessName} {_DumpFileName}");
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

                if (_StartClrObjExplorer)
                {
                    sw.Restart();
                    await Task.Run(() =>
                    {
                        LogMessage($"Loading dump in DumpAnalyzer {_DumpFileName}");
                        var x = new DumpAnalyzer(this);
                        //                    x.AnalyzeDump();
                        x.StartClrObjExplorer(_DumpFileName);
                    });
                    _logger.LogMessage($"Done Analyzing dump {_procTarget.Id} {_procTarget.ProcessName}  Secs={sw.Elapsed.TotalSeconds:f3}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"exception creating dump {ex.ToString()}");
            }
        }

        public static string EnsureResultsFolderExists()
        {
            var dirMyTemp = Path.Combine(Path.GetTempPath(), "PerfGraphVSIX");
            if (!Directory.Exists(dirMyTemp))
            {
                Directory.CreateDirectory(dirMyTemp);
            }
            return dirMyTemp;
        }
        public static string GetNewResultsFolderName(string baseFolderName)
        {
            var dirMyTemp = EnsureResultsFolderExists();
            int nIter = 0;
            string pathResultsFolder;
            while (true) // we want to let the user have multiple dumps open for comparison
            {
                var appendstr = nIter++ == 0 ? string.Empty : nIter.ToString();
                pathResultsFolder = Path.Combine(
                    dirMyTemp,
                    $"{baseFolderName}{appendstr}");
                if (!Directory.Exists(pathResultsFolder))
                {
                    Directory.CreateDirectory(pathResultsFolder);
                    break;
                }
            }
            return pathResultsFolder;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="baseFolderName"></param>
        /// <param name="baseDumpFileName"></param>
        /// <param name="ext">without the '.', defaults to 'dmp'</param>
        /// <returns></returns>
        public static string GetNewFileName(string baseFolderName, string baseDumpFileName, string ext = "dmp")
        {
            string pathDumpFile;
            int nIter = 0;
            while (true) // we want to let the user have multiple dumps open for comparison
            {
                pathDumpFile = Path.Combine(
                    baseFolderName,
                    $"{baseDumpFileName}_{nIter++}.{ext}");
                if (!File.Exists(pathDumpFile))
                {
                    break;
                }
            }
            return pathDumpFile;
        }

        private Process ChooseProcess(string desc, bool fShow32BitOnly = true)
        {
            Process procChosen = null;
            var q = from proc in Process.GetProcesses()
                    orderby proc.ProcessName
                    where fShow32BitOnly ? ProcessType(proc) == "32" : true
                    select new
                    {
                        proc.Id,
                        proc.ProcessName,
                        proc.MainWindowTitle,
                        proc.WorkingSet64,
                        proc.PrivateMemorySize64,
                        Is64 = ProcessType(proc),
                        _proc = proc
                    };

            var brPanel = new BrowsePanel(q, new[] { 40, 220, 400 });
            var w = new Window
            {
                Content = brPanel,
                Title = desc
            };
            brPanel.BrowseList.MouseDoubleClick += (o, e) =>
            {
                w.Close();
            };
            brPanel.BrowseList.KeyUp += (o, e) =>
              {
                  if (e.Key == System.Windows.Input.Key.Return)
                  {
                      w.Close();
                  }
              };
            w.Closed += (o, e) =>
              {
                  var p = brPanel.BrowseList.SelectedItem;
                  if (p != null)
                  {
                      var proc = TypeDescriptor.GetProperties(p)["_proc"].GetValue(p) as Process;
                      if (!fShow32BitOnly || ProcessType(proc) == "32")
                      {
                          procChosen = proc;
                      }
                  }
              };
            w.ShowDialog();
            return procChosen;
        }

        private string ProcessType(Process proc)
        {
            var typ = string.Empty;
            try
            {
                var IsRunningUnderWow64 = false;
                if (IsWow64Process(Process.GetCurrentProcess().Handle, ref IsRunningUnderWow64) && IsRunningUnderWow64)
                {
                    if (IsWow64Process(proc.Handle, ref IsRunningUnderWow64) && IsRunningUnderWow64)
                    {
                        typ = "32";
                    }
                    else
                    {
                        typ = "64";
                    }
                }
                else
                {
                    typ = "32";
                }
            }
            catch (Exception)
            {
            }
            return typ;
        }
        [DllImport("Kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        public static extern bool IsWow64Process(IntPtr hProcess, ref bool wow64Process);
        //<MarshalAs(UnmanagedType.Bool)> ByRef wow64Process As Boolean) As<MarshalAs(UnmanagedType.Bool)> Boolean

        private void DoShowHelp(string extrainfo = "")
        {
            try
            {

                var owin = new Window();
                var tb = new TextBlock()
                {
                    Text = $@"DumperViewer: 
{extrainfo}

This Program can
 - Create a dump of a specified process
 - analyze a dump for the counts of the specified types
 - show UI of the counts in ClrObjExplorer, allowing interactive exploration of objects, counts, references.
 
Command line:
DumpViewer -p 1234 -t .*TextBuffer.*
-?  Show this help
-p <pid>   Process id of process. Will take a dump of the specified process (e.g. Devenv). Devenv cannot take a dump of itself because it will result in deadlock 
        (creating a dump involves freezing threads) and continue executing launch DumperViewer
        (The creation of the dump and the subsequent analysis needs to be fast)
-t <regex><|regex>  a '|' separated list of regular expressions that specify what types the caller is 'interested in'. e.g. '.*textbuffer.*'|'.*codelens.*'
     can be '|' separated or there can be multiple '-t' arguments

-f <FileDumpname>  Base path of output '.dmp' for dump output. If exists, will add numerical suffix. Can be quoted.
-b <FileDumpname1> Filename of baseline dump taken N interations before the dump in -f. Will output   
-n # iterations baseline snap was taken before current dump

-c Start ClrObjExplorer after creating dump

-u  UI: Show the WPF UI. If a dumpfile is specified,  open a treeview for the dump (ClrObjExplorer). Else just show generic UI that will allow the user to choose a target process for which to view memory


"
                };

                owin.Content = tb;
                owin.ShowDialog();
            }
            catch (InvalidOperationException) //System.InvalidOperationException: The calling thread must be STA, because many UI components require this.
            {
            }
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
                var msgstr = DateTime.Now.ToString("hh:mm:ss:fff") + $" {Thread.CurrentThread.ManagedThreadId,2} {str}";

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
