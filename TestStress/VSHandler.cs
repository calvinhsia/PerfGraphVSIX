using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PerfGraphVSIX;

namespace LeakTestDatacollector
{
    public class VSHandler : IVisualStudio
    {
        public const string procToFind = "devenv"; // we need devenv to start, manipulate DTE. We may want to profile child processes
        private readonly ILogger logger;
        private readonly int _DelayMultiplier;
        public EnvDTE.DTE _vsDTE;
        /// <summary>
        /// The process we're monitoring
        /// </summary>
        public Process TargetProc => PerfCounterData.ProcToMonitor;
        TaskCompletionSource<int> _tcsSolution = new TaskCompletionSource<int>();

        EnvDTE.SolutionEvents _solutionEvents; // need a strong ref to survive GCs

        public VSHandler(ILogger logger, int delayMultiplier = 1)
        {
            this.logger = logger;
            this._DelayMultiplier = delayMultiplier;
        }


        public async Task<bool> EnsureGotDTE(TimeSpan timeSpan)
        {
            if (_vsDTE == null)
            {
                logger.LogMessage($"{nameof(EnsureGotDTE)}");
                await Task.Yield();
                var procDevEnv = Process.GetProcessesByName(procToFind).OrderByDescending(p => p.StartTime).FirstOrDefault();
                logger.LogMessage($"Latest devenv = {procDevEnv.Id} starttime = {procDevEnv.StartTime}");
                var diff = procDevEnv.StartTime > DateTime.Now - timeSpan;
                if (procDevEnv.StartTime < DateTime.Now - timeSpan) // the process start time must have started very recently
                {
                    throw new InvalidOperationException($"Couldn't find {procToFind}in {timeSpan.TotalSeconds} seconds {diff} PidLatest = {procDevEnv.Id} ");
                }
                PerfCounterData.ProcToMonitor = procDevEnv;
                _vsDTE = await GetDTEAsync(TargetProc.Id, TimeSpan.FromSeconds(30 * _DelayMultiplier));
                _solutionEvents = _vsDTE.Events.SolutionEvents;

                _solutionEvents.Opened += SolutionEvents_Opened; // can't get OnAfterBackgroundSolutionLoadComplete?
                _solutionEvents.AfterClosing += SolutionEvents_AfterClosing;
                logger.LogMessage($"{nameof(EnsureGotDTE)} done");
            }
            return true;
        }


        public async Task StartVSAsync(string vsPath)
        {
            logger.LogMessage($"{nameof(StartVSAsync)}");
            PerfCounterData.ProcToMonitor = Process.Start(vsPath);
            logger.LogMessage($"Started VS PID= {TargetProc.Id}");
            await EnsureGotDTE(TimeSpan.FromSeconds(3));
            logger.LogMessage($"done {nameof(StartVSAsync)}");
        }

        public async Task OpenSolution(string SolutionToLoad)
        {
            var timeoutVSSlnEventsSecs = 15 * _DelayMultiplier;
            //LogMessage($"Opening solution {SolutionToLoad}");
            _tcsSolution = new TaskCompletionSource<int>();
            _vsDTE.Solution.Open(SolutionToLoad);
            if (await Task.WhenAny(_tcsSolution.Task, Task.Delay(TimeSpan.FromSeconds(timeoutVSSlnEventsSecs))) != _tcsSolution.Task)
            {
                logger.LogMessage($"******************Solution Open event not fired in {timeoutVSSlnEventsSecs} seconds");
            }
            _tcsSolution = new TaskCompletionSource<int>();
            await Task.Delay(TimeSpan.FromSeconds(10 * _DelayMultiplier));
        }

        public async Task CloseSolution()
        {
            var timeoutVSSlnEventsSecs = 15 * _DelayMultiplier;
            _tcsSolution = new TaskCompletionSource<int>();
            _vsDTE.Solution.Close();
            if (await Task.WhenAny(_tcsSolution.Task, Task.Delay(TimeSpan.FromSeconds(timeoutVSSlnEventsSecs))) != _tcsSolution.Task)
            {
                logger.LogMessage($"******************Solution Close event not fired in {timeoutVSSlnEventsSecs} seconds");
            }
            await Task.Delay(TimeSpan.FromSeconds(5 * _DelayMultiplier));
        }

        private void SolutionEvents_AfterClosing()
        {
            //LogMessage($"{nameof(SolutionEvents_AfterClosing)}");
            _tcsSolution.TrySetResult(0);
        }

        private void SolutionEvents_Opened()
        {
            //LogMessage($"{nameof(SolutionEvents_Opened)}");
            _tcsSolution.TrySetResult(0);
        }

        public async Task ShutDownVSAsync()
        {
            await Task.Yield();
            if (_vsDTE != null)
            {
                _vsDTE.Events.SolutionEvents.Opened -= SolutionEvents_Opened;
                _vsDTE.Events.SolutionEvents.AfterClosing -= SolutionEvents_AfterClosing;
                var tcs = new TaskCompletionSource<int>();
                TargetProc.Exited += (o, e) => // doesn't fire reliably
                {
                    tcs.SetResult(0);
                };
                _vsDTE.Quit();
                var timeoutForClose = 15 * _DelayMultiplier;
                var taskOneSecond = Task.Delay(1000);

                while (timeoutForClose > 0)
                {
                    if (await Task.WhenAny(tcs.Task, taskOneSecond) != tcs.Task)
                    {
                        if (TargetProc.HasExited)
                        {
                            break;
                        }
                        taskOneSecond = Task.Delay(1000);
                    }
                }
                if (!TargetProc.HasExited)
                {
                    logger.LogMessage($"******************Did not close in {timeoutForClose} secs");
                }
                //MessageFilter.RevokeMessageFilter();
                _vsDTE = null;

            }
        }

        public void DoGarbageCollect()
        {
            _vsDTE.ExecuteCommand("Tools.ForceGC");
        }

        public async Task<EnvDTE.DTE> GetDTEAsync(int processId, TimeSpan timeout)
        {
            EnvDTE.DTE dte;
            var sw = Stopwatch.StartNew();
            while ((dte = GetDTE(processId)) == null)
            {
                if (sw.Elapsed > timeout)
                {
                    break;
                }
                await Task.Delay(1000); // one second (no multiplier needed)
            }
            return dte;
        }

        private EnvDTE.DTE GetDTE(int processId)
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

            //            MessageFilter.RegisterMessageFilter();
            return runningObject as EnvDTE.DTE;
        }

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);

    }
}
