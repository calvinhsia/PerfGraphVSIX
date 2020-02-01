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

namespace Microsoft.Test.Stress
{
    /// <summary>
    /// Handles Visual Studio via DTE (used for Apex to do ForceGc between iterations)
    /// </summary>
    public class VSHandler
    {
        public const string procToFind = "devenv"; // we need devenv to start, manipulate DTE. We may want to monitor child processes like servicehub
        private readonly ILogger logger;
        public int _DelayMultiplier;
        private EnvDTE.DTE _vsDTE;
        /// <summary>
        /// The process we're monitoring may be different from vs: e.g. servicehub
        /// We need the vsProc to get DTE so we can force GC between iterations, automate via DTE, etc.
        /// </summary>
        public Process vsProc;
        TaskCompletionSource<int> _tcsSolution = new TaskCompletionSource<int>();

        EnvDTE.SolutionEvents _solutionEvents; // need a strong ref to survive GCs

        public VSHandler(ILogger logger, int delayMultiplier = 1)
        {
            this.logger = logger;
            this._DelayMultiplier = delayMultiplier;
        }


        /// <summary>
        /// Get the DTE for a specific devenv process.
        /// </summary>
        /// <param name="targetDevEnvProcessId"></param>
        /// <returns></returns>
        public async Task<bool> EnsureGotDTE(int targetDevEnvProcessId)
        {
            return await EnsureGotDTE(timeSpan: default, targetDevEnvProcessId);
        }

        /// <summary>
        /// We don't want an old VS session: we want to find the devenv process that was started by the test: +/- timeSpan seconds
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        public async Task<bool> EnsureGotDTE(TimeSpan timeSpan = default)
        {
            return await EnsureGotDTE(timeSpan, targetDevEnvProcessId: 0);
        }

        private async Task<bool> EnsureGotDTE(TimeSpan timeSpan = default, int targetDevEnvProcessId = 0)
        {
            if (_vsDTE == null)
            {
                if (timeSpan == default)
                {
                    timeSpan = TimeSpan.FromSeconds(50);
                }
                var dtStartChecking = DateTime.Now;
                await Task.Run(async () =>
                {
                    logger.LogMessage($"{nameof(EnsureGotDTE)}");
                    await Task.Yield();
                    Process procDevenv;
                    bool GetTargetDevenvProcess() // need to find devenv that is not currently running test, but was started by test (either before or after this code is called
                    {
                        bool fGotit = false;
                        procDevenv = Process.GetProcessesByName(procToFind).OrderByDescending(p => p.StartTime).FirstOrDefault();
                        // the process start time must have started very recently, but we need to exclude the case where user starts devenv, then immediately runs the test
                        // IOW, it must have started at most 30 seconds ago
                        if (procDevenv.StartTime > DateTime.Now - TimeSpan.FromSeconds(30)) 
                        {
                            logger.LogMessage($"Latest devenv PID= {procDevenv.Id} starttime = {procDevenv.StartTime}");
                            fGotit = true;
                        }
                        return fGotit;
                    }
                    if (targetDevEnvProcessId != 0)
                    {
                        procDevenv = Process.GetProcessById(targetDevEnvProcessId);
                        logger.LogMessage($"Targeting devenv PID= {procDevenv.Id} as specified by the caller");
                    }
                    else
                    {
                        while (!GetTargetDevenvProcess())
                        {
                            logger.LogMessage($"Didn't find Devenv. Waiting til it starts {timeSpan.TotalSeconds:n0} secs");
                            await Task.Delay(TimeSpan.FromSeconds(5));
                            if (DateTime.Now - dtStartChecking > timeSpan)
                            {
                                throw new InvalidOperationException($"Couldn't find {procToFind} in {timeSpan.TotalSeconds * 2:n0} seconds {timeSpan.TotalSeconds:n0} PidLatest = {procDevenv.Id} ");
                            }
                        }
                    }
                    vsProc = procDevenv;
                    _vsDTE = await GetDTEAsync(vsProc.Id, TimeSpan.FromSeconds(30 * _DelayMultiplier));
                    _solutionEvents = _vsDTE.Events.SolutionEvents;

                    _solutionEvents.Opened += SolutionEvents_Opened; // can't get OnAfterBackgroundSolutionLoadComplete?
                    _solutionEvents.AfterClosing += SolutionEvents_AfterClosing;
                    logger.LogMessage($"{nameof(EnsureGotDTE)} done PID={procDevenv.Id} {procDevenv.MainModule}  {procDevenv.MainModule.FileVersionInfo}");
                    foreach (var proc in Process.GetProcessesByName(procToFind).OrderByDescending(p=>p.StartTime))
                    {
                        logger.LogMessage($"   All {procToFind} instances running on machine: ID={proc.Id} '{proc.MainWindowTitle}' {proc.MainModule.FileName} StartTime: {proc.StartTime}");
                    }
                });
            }
            return true;
        }

        // "c:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\VsRegEdit.exe"
        //vsregedit.exe set "D:\Program Files (x86)\Microsoft Visual Studio 15.0" HKCU General\AutoRecover "AutoRecover Enabled" dword 0
        //vsregedit.exe set "D:\Program Files (x86)\Microsoft Visual Studio 15.0" HKCU General DelayTimeThreshold dword 20000
        // vsregedit read local HKCU General DelayTimeThreshold dword
        // vsregedit set local HKCU General DelayTimeThreshold dword 20000
        // vsregedit set local HKCU General MaxNavigationHistoryDepth dword 2
        // vsregedit read local HKCU General MaxNavigationHistoryDepth dword
        /// <summary>
        /// set things like Navigtion history to small, so it doesn't look like a leak. changes settings that are read when VS Starts
        /// </summary>
        public static void PrepareVSSettingsForLeakDetection(string vsPath = null, ILogger logger = null)
        {
            var r = VSHandler.DoVSRegEdit("set local HKCU General DelayTimeThreshold dword 20000", vsPath);
            logger?.LogMessage(r);
            r = VSHandler.DoVSRegEdit("set local HKCU General MaxNavigationHistoryDepth dword 2", vsPath);
            logger?.LogMessage(r);
        }


        public static string DoVSRegEdit(string arg, string vsPath = null)
        {
            if (string.IsNullOrEmpty(vsPath))
            {
                vsPath = VSHandler.GetVSFullPath();
            }
            var vsRegEdit = Path.Combine(Path.GetDirectoryName(vsPath), "VsRegedit.exe");
            var sb = new StringBuilder();

            using (var proc = VSHandler.CreateProcess(vsRegEdit, arg, sb))
            {
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
            }
            return sb.ToString();
        }


        /// <summary>
        /// Execute the given process
        /// </summary>
        /// <param name="exeName">Process to be executed</param>
        /// <param name="arguments">Process arguments</param>
        /// <param name="sb">stringbuild for result</param>
        /// <returns></returns>
        internal static Process CreateProcess(string exeName, string arguments, StringBuilder sb)
        {
            ProcessStartInfo info = new ProcessStartInfo(exeName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Process process = new Process();
            process.OutputDataReceived += (o, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    sb.AppendLine(string.Format("==>StdOut: {0}", e.Data));
                }
            };
            process.ErrorDataReceived += (o, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    sb.AppendLine(string.Format("==>StdErr: {0}", e.Data));
                }
            };
            info.CreateNoWindow = true;
            process.StartInfo = info;
            return process;
        }

        /// <summary>
        /// Find the location of the latest VS instance. Return the full path of Devenv.exe
        /// </summary>
        /// <returns></returns>
        public static string GetVSFullPath()
        {
            var vsPath = string.Empty;
            var lstFileInfos = new List<FileInfo>();
            // get VS Path, like @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\devenv.exe";
            var lstProgFileDirs = new List<string>();
            var dprogFiles = @"D:\Program Files (x86)";
            var progfiles = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (progfiles.ToUpper().StartsWith("C"))
            {
                lstProgFileDirs.Add(progfiles);
                if (Directory.Exists(dprogFiles))
                {
                    lstProgFileDirs.Add(dprogFiles);
                }
            }
            foreach (var progFileDir in lstProgFileDirs)
            {
                progfiles = Path.Combine(progfiles, "Microsoft Visual Studio");
                foreach (var vsdir in Directory.GetDirectories(progfiles).Where(d => d.IndexOf("Installer") < 0 && d.IndexOf("Shared") < 0))
                {
                    foreach (var subdir in Directory.GetDirectories(vsdir))
                    {
                        var testVSPath = Path.Combine(subdir, @"Common7\IDE\devenv.exe");
                        if (File.Exists(testVSPath))
                        {
                            lstFileInfos.Add(new FileInfo(testVSPath));
                        }
                    }
                }
            }
            if (lstFileInfos.Count == 0)
            {
                throw new FileNotFoundException($"Could not find devenv under " + string.Join("|", lstProgFileDirs));
            }
            // FileVersion:      16.5.29713.161 built by: MASTER
            // ProductVersion:   16.5.29713.161

            vsPath = lstFileInfos.OrderByDescending(f => FileVersionInfo.GetVersionInfo(f.FullName).ProductVersion).First().FullName;
            return vsPath;
        }

        /* Apex: set env vars
            VisualStudio = Operations.CreateHost<VisualStudioHost>();
            VisualStudio.Configuration.Environment.Add("FooBar", "FooBar3");
            VisualStudio.Start();
         */
        public async Task<Process> StartVSAsync(string vsPath = null, MemSpectModeFlags memSpectModeFlags = MemSpectModeFlags.MemSpectModeNone, string MemSpectDllPath = "")
        {
            if (string.IsNullOrEmpty(vsPath))
            {
                vsPath = GetVSFullPath();
            }
            logger.LogMessage($"{ nameof(StartVSAsync)}  VSPath= {vsPath}");
            var startOptions = new ProcessStartInfo(vsPath);
            if (memSpectModeFlags != MemSpectModeFlags.MemSpectModeNone)
            {
                startOptions.UseShellExecute = false; // must be calse to use env
                StressUtil.SetEnvironmentForMemSpect(startOptions.Environment, memSpectModeFlags, MemSpectDllPath);
            }
            vsProc = Process.Start(startOptions);
            logger.LogMessage($"Started VS PID= {vsProc.Id}");
            await EnsureGotDTE(timeSpan: default);
            logger.LogMessage($"done {nameof(StartVSAsync)}");
            return vsProc;
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
                vsProc.Exited += (o, e) => // doesn't fire reliably
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
                        if (vsProc.HasExited)
                        {
                            break;
                        }
                        taskOneSecond = Task.Delay(1000);
                    }
                }
                if (!vsProc.HasExited)
                {
                    logger.LogMessage($"******************Did not close in {timeoutForClose} secs");
                }
                //MessageFilter.RevokeMessageFilter();
                _vsDTE = null;

            }
        }
        public async Task DteExecuteCommand(string strCommand, int timeoutSecs = 60)
        {
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSecs));
            var didGC = false;
            while (!timeoutTask.IsCompleted && !didGC)
            {
                try
                {
                    _vsDTE.ExecuteCommand(strCommand);
                    didGC = true;
                }
                catch (COMException) // System.Runtime.InteropServices.COMException (0x8001010A): The message filter indicated that the application is busy. (Exception from HRESULT: 0x8001010A (RPC_E_SERVERCALL_RETRYLATER))
                {
                    logger.LogMessage($"Couldn't do {strCommand}: retry");
                    await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                }
            }
            if (!didGC)
            {
                logger.LogMessage($"Couldn't do {strCommand} in {timeoutSecs} secs");
            }
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
            if (dte == null)
            {
                logger.LogMessage($"Couldn't get DTE in {timeout.TotalSeconds:n0} secs");
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
            // can't register message filter because need to set STA apartment, which means the test needs to be run on STA
            //            MessageFilter.RegisterMessageFilter();
            return runningObject as EnvDTE.DTE;
        }

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);

    }
}
