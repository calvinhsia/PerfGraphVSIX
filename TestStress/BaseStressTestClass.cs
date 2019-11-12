using DumperViewer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TestStress
{
    public class BaseStressTestClass : ILogger
    {
        /// <summary>
        /// The process we're monitoring
        /// </summary>
        public Process _targetProc;
        protected EnvDTE.DTE _vsDTE;
        protected EnvDTE.SolutionEvents _solutionEvents; // need a strong ref to survive GCs

        protected int DelayMultiplier = 1;
        /// <summary>
        /// When executing a specific iteration, take a snapshot dump of the target process
        /// Then we can diff the counts from the final dump
        /// The snapshot will be taken at the end of iteration NumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot
        /// </summary>
        public int NumIterationsBeforeTotalToTakeBaselineSnapshot = 3;

        public TestContext TestContext { get; set; }

        internal Task InitializeBaseAsync()
        {
            return Task.CompletedTask;
        }


        public async Task StartVSAsync()
        {
            LogMessage($"{nameof(StartVSAsync)}");
            var vsPath = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\devenv.exe";
            LogMessage($"Starting VS");
            _targetProc = Process.Start(vsPath);
            LogMessage($"Started VS PID= {_targetProc.Id}");
            PerfCounterData.ProcToMonitor = _targetProc;

            _vsDTE = await GetDTEAsync(_targetProc.Id, TimeSpan.FromSeconds(30 * DelayMultiplier));
            _solutionEvents = _vsDTE.Events.SolutionEvents;

            _solutionEvents.Opened += SolutionEvents_Opened; // can't get OnAfterBackgroundSolutionLoadComplete?
            _solutionEvents.AfterClosing += SolutionEvents_AfterClosing;

            LogMessage($"done {nameof(StartVSAsync)}");
        }
        internal async Task ShutDownVSAsync()
        {
            await Task.Yield();
            if (_vsDTE != null)
            {
                _vsDTE.Events.SolutionEvents.Opened -= SolutionEvents_Opened;
                _vsDTE.Events.SolutionEvents.AfterClosing -= SolutionEvents_AfterClosing;
                var tcs = new TaskCompletionSource<int>();
                _targetProc.Exited += (o, e) => // doesn't fire reliably
                 {
                     tcs.SetResult(0);
                 };
                _vsDTE.Quit();
                var timeoutForClose = 15 * DelayMultiplier;
                var taskOneSecond = Task.Delay(1000);

                while (timeoutForClose > 0)
                {
                    if (await Task.WhenAny(tcs.Task, taskOneSecond) != tcs.Task)
                    {
                        if (_targetProc.HasExited)
                        {
                            break;
                        }
                        taskOneSecond = Task.Delay(1000);
                    }
                }
                if (!_targetProc.HasExited)
                {
                    LogMessage($"******************Did not close in {timeoutForClose} secs");
                }
                _vsDTE = null;

            }
        }

        TaskCompletionSource<int> _tcsSolution = new TaskCompletionSource<int>();
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

        public async Task OpenCloseSolutionOnce(string SolutionToLoad)
        {
            var timeoutVSSlnEventsSecs = 15 * DelayMultiplier;
            //LogMessage($"Opening solution {SolutionToLoad}");
            _tcsSolution = new TaskCompletionSource<int>();
            _vsDTE.Solution.Open(SolutionToLoad);
            if (await Task.WhenAny(_tcsSolution.Task, Task.Delay(TimeSpan.FromSeconds(timeoutVSSlnEventsSecs))) != _tcsSolution.Task)
            {
                LogMessage($"******************Solution Open event not fired in {timeoutVSSlnEventsSecs} seconds");
            }

            _tcsSolution = new TaskCompletionSource<int>();
            await Task.Delay(TimeSpan.FromSeconds(10 * DelayMultiplier));

            //LogMessage($"Closing solution");
            _vsDTE.Solution.Close();
            if (await Task.WhenAny(_tcsSolution.Task, Task.Delay(TimeSpan.FromSeconds(timeoutVSSlnEventsSecs))) != _tcsSolution.Task)
            {
                LogMessage($"******************Solution Close event not fired in {timeoutVSSlnEventsSecs} seconds");
            }

            await Task.Delay(TimeSpan.FromSeconds(5 * DelayMultiplier));
        }

        /// <summary>
        /// after each iteration, take measurements
        /// </summary>
        /// <returns></returns>
        public static async Task TakeMeasurementAsync(BaseStressTestClass test, MeasurementHolder measurementHolder, string desc)
        {
            //test.LogMessage($"{nameof(TakeMeasurementAsync)} {nIteration}");
            //                await Task.Delay(TimeSpan.FromSeconds(5 * test.DelayMultiplier));
            try
            {
                test._vsDTE?.ExecuteCommand("Tools.ForceGC");
                await Task.Delay(TimeSpan.FromSeconds(1 * test.DelayMultiplier));

                var res = measurementHolder.TakeMeasurement(desc);
                test.LogMessage(res);
            }
            catch (Exception ex)
            {
                test.LogMessage($"Exception in {nameof(TakeMeasurementAsync)}" + ex.ToString());
            }
        }

        //public static async Task AllIterationsFinishedAsync(BaseStressTestClass test, bool createDump = false, bool startClrObjExplorer = false)
        //{
        //    try
        //    {
        //        test.LogMessage($"{nameof(AllIterationsFinishedAsync)}");
        //        if (createDump)
        //        {
        //            var pathDumpFile = DumperViewer.DumperViewerMain.GetNewDumpFileName(baseName: $"devenv_{test.TestContext.TestName}");
        //            await Task.Delay(TimeSpan.FromSeconds(5 * test.DelayMultiplier));

        //            test.LogMessage($"start clrobjexplorer {pathDumpFile}");
        //            var pid = test._targetProc.Id;
        //            var args = new List<string>
        //        {
        //            $" -p {pid}"
        //        };
        //            args.Add($" -f \"{pathDumpFile}\"");
        //            if (startClrObjExplorer)
        //            {
        //                args.Add($"-c");
        //            }
        //            var odumper = new DumperViewerMain(args.ToArray())
        //            {
        //                _logger = test
        //            };
        //            await odumper.DoitAsync();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        test.LogMessage(ex.ToString());
        //    }
        //}

        /// <summary>
        /// Do it all: tests need only add a single line to TestInitialize to turn a normal test into a stress test
        /// </summary>
        /// <param name="stressWithNoInheritance"></param>
        /// <param name="NumIterations"></param>
        /// <returns></returns>
        public static async Task DoIterationsAsync(BaseStressTestClass test, int NumIterations, double Sensitivity = 1.0f)
        {
            test.LogMessage($"{nameof(DoIterationsAsync)} TestName = {test.TestContext.TestName}");
            var _theTestMethod = test.GetType().GetMethods().Where(m => m.Name == test.TestContext.TestName).First();

            var measurementHolder = new MeasurementHolder(
                test.TestContext.TestName,
                PerfCounterData._lstPerfCounterDefinitionsForStressTest,
                SampleType.SampleTypeIteration,
                logger: test,
                sensitivity: Sensitivity);
            test.TestContext.Properties[nameof(MeasurementHolder)] = measurementHolder;

            var baseDumpFileName = string.Empty;
            for (int iteration = 0; iteration < NumIterations; iteration++)
            {
                var ret = _theTestMethod.Invoke(test, parameters: null);
                await BaseStressTestClass.TakeMeasurementAsync(test, measurementHolder, $"Iter {iteration + 1}/{NumIterations}");
                if (NumIterations > test.NumIterationsBeforeTotalToTakeBaselineSnapshot && iteration == NumIterations - test.NumIterationsBeforeTotalToTakeBaselineSnapshot - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5 * test.DelayMultiplier));
                    test.LogMessage($"Taking base snapshot dump");
                    baseDumpFileName = await measurementHolder.CreateDumpAsync(
                        test._targetProc.Id,
                        desc: test.TestContext.TestName + "_" + iteration.ToString(),
                        memoryAnalysisType: MemoryAnalysisType.JustCreateDump);
                }
            }
            var filenameResults = measurementHolder.DumpOutMeasurementsToTempFile(StartExcel: false);
            test.LogMessage($"Measurement Results {filenameResults}");
            var lstRegResults = (await measurementHolder.CalculateRegressionAsync(showGraph: true)).Where(r => r.IsRegression).ToList();
            if (lstRegResults.Count > 0)
            {
                foreach (var regres in lstRegResults)
                {
                    test.LogMessage($"Regression!!!!! {regres}");
                }
                var currentDumpFile = await measurementHolder.CreateDumpAsync(
                    test._targetProc.Id,
                    desc: test.TestContext.TestName + "_" + NumIterations.ToString(),
                    memoryAnalysisType: MemoryAnalysisType.StartClrObjectExplorer);
                if (!string.IsNullOrEmpty(baseDumpFileName))
                {
                    var oDumpAnalyzer= new DumperViewer.DumpAnalyzer(test);
                    oDumpAnalyzer.GetDiff(baseDumpFileName, currentDumpFile, NumIterations, test.NumIterationsBeforeTotalToTakeBaselineSnapshot);
                }
            }
        }

        public List<string> _lstLoggedStrings = new List<string>();

        public void LogMessage(string str, params object[] args)
        {
            var dt = string.Format("[{0}],",
                DateTime.Now.ToString("hh:mm:ss:fff")
                );
            str = string.Format(dt + str, args);
            var msgstr = DateTime.Now.ToString("hh:mm:ss:fff") + $" {Thread.CurrentThread.ManagedThreadId,2} {str}";

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
                await Task.Delay(1000); // one second (no multiplier needed)
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