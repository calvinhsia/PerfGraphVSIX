using DumperViewer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TestStress
{
    public class BaseStressTestClass : ILogger
    {
        protected Process _vsProc;
        protected EnvDTE.DTE _vsDTE;

        protected int DelayMultiplier = 1;

        public TestContext TestContext { get; set; }

        internal Task InitializeBaseAsync()
        {
            return Task.FromResult(0);
        }

        public async Task StartVSAsync()
        {
            LogMessage($"{nameof(StartVSAsync)}");
            var vsPath = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\devenv.exe";
            LogMessage($"Starting VS");
            _vsProc = Process.Start(vsPath);
            LogMessage($"Started VS PID= {_vsProc.Id}");

            _vsDTE = await GetDTEAsync(_vsProc.Id, TimeSpan.FromSeconds(30 * DelayMultiplier));
            _vsDTE.Events.SolutionEvents.Opened += SolutionEvents_Opened;
            _vsDTE.Events.SolutionEvents.AfterClosing += SolutionEvents_AfterClosing;
            LogMessage($"done {nameof(StartVSAsync)}");
        }
        internal async Task ShutDownVSAsync()
        {
            await Task.Yield();
            if (_vsDTE != null)
            {
                _vsDTE.Events.SolutionEvents.Opened -= SolutionEvents_Opened;
                _vsDTE.Events.SolutionEvents.AfterClosing -= SolutionEvents_AfterClosing;
                _vsDTE.Quit();
                _vsDTE = null;
            }
        }

        TaskCompletionSource<int> _tcsSolution = new TaskCompletionSource<int>();
        private void SolutionEvents_AfterClosing()
        {
            //            LogMessage($"{nameof(SolutionEvents_AfterClosing)}");
            _tcsSolution.TrySetResult(0);
        }

        private void SolutionEvents_Opened()
        {
            //            LogMessage($"{nameof(SolutionEvents_Opened)}");
            _tcsSolution.TrySetResult(0);
        }

        public async Task OpenCloseSolutionOnce()
        {
            string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
            _tcsSolution = new TaskCompletionSource<int>();
            _vsDTE.Solution.Open(SolutionToLoad);
            await _tcsSolution.Task;

            _tcsSolution = new TaskCompletionSource<int>();
            await Task.Delay(TimeSpan.FromSeconds(5 * DelayMultiplier));

            _vsDTE.Solution.Close();
            await _tcsSolution.Task;

            await Task.Delay(TimeSpan.FromSeconds(5 * DelayMultiplier));
        }

        public async Task IterationsFinishedAsync()
        {
            try
            {
                LogMessage($"{nameof(IterationsFinishedAsync)}");
                var pathDumpFile = DumperViewer.DumperViewerMain.GetNewDumpFileName(baseName: $"devenv_{TestContext.TestName}");
                _vsDTE.ExecuteCommand("Tools.ForceGC");
                await Task.Delay(TimeSpan.FromSeconds(5));

                LogMessage($"start clrobjexplorer {pathDumpFile}");
                var pid = _vsProc.Id;
                var args = new[] {
                "-p", pid.ToString(),
                "-f",  "\"" + pathDumpFile + "\"",
                "-c"
                    };
                var odumper = new DumperViewerMain(args)
                {
                    _logger = this
                };
                await odumper.DoitAsync();
            }
            catch (Exception ex)
            {
                LogMessage(ex.ToString());
            }
        }

        public List<string> _lstLoggedStrings = new List<string>();

        public void LogMessage(string str, params object[] args)
        {
            var dt = string.Format("[{0}],",
                DateTime.Now.ToString("hh:mm:ss:fff")
                );
            str = string.Format(dt + str, args);
            var msgstr = DateTime.Now.ToString("hh:mm:ss:fff") + $" {Thread.CurrentThread.ManagedThreadId} {str}";

            this.TestContext.WriteLine(msgstr);
            if (Debugger.IsAttached)
            {
                Debug.WriteLine(msgstr);
            }
            _lstLoggedStrings.Add(msgstr);
        }


        public async static Task<EnvDTE.DTE> GetDTEAsync(int processId, TimeSpan timeout)
        {
            EnvDTE.DTE dte;
            var sw = Stopwatch.StartNew();
            while ((dte = GetDTE(processId)) == null)
            {
                if (sw.Elapsed > timeout)
                {
                    break;
                }
                await Task.Delay(1000);
            }
            return dte;
        }

        private static EnvDTE.DTE GetDTE(int processId)
        {
            object runningObject = null;

            IBindCtx bindCtx = null;
            IRunningObjectTable rot = null;
            IEnumMoniker enumMonikers = null;

            try
            {
                Marshal.ThrowExceptionForHR(CreateBindCtx(reserved: 0, ppbc: out bindCtx));
                bindCtx.GetRunningObjectTable(out rot);
                rot.EnumRunning(out enumMonikers);

                IMoniker[] moniker = new IMoniker[1];
                IntPtr numberFetched = IntPtr.Zero;
                while (enumMonikers.Next(1, moniker, numberFetched) == 0)
                {
                    IMoniker runningObjectMoniker = moniker[0];

                    string name = null;

                    try
                    {
                        if (runningObjectMoniker != null)
                        {
                            runningObjectMoniker.GetDisplayName(bindCtx, null, out name);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Do nothing, there is something in the ROT that we do not have access to.
                    }

                    Regex monikerRegex = new Regex(@"!VisualStudio.DTE\.\d+\.\d+\:" + processId, RegexOptions.IgnoreCase); // VisualStudio.DTE.16.0:56668
                    if (!string.IsNullOrEmpty(name) && monikerRegex.IsMatch(name))
                    {
                        Marshal.ThrowExceptionForHR(rot.GetObject(runningObjectMoniker, out runningObject));
                        break;
                    }
                }
            }
            finally
            {
                if (enumMonikers != null)
                {
                    Marshal.ReleaseComObject(enumMonikers);
                }

                if (rot != null)
                {
                    Marshal.ReleaseComObject(rot);
                }

                if (bindCtx != null)
                {
                    Marshal.ReleaseComObject(bindCtx);
                }
            }

            return runningObject as EnvDTE.DTE;
        }

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);

    }
}