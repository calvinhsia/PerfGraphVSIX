using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;



/*
  rd /s /q c:\test
  c:\DDTest\Microsoft.DevDiv.TestPlatform.Client.exe /RunSettings:Stress.runsettings /CleanupDeployment:Never  /DeploymentDirectory:c:\Test 


 * don't cleanup deplopyment
   /CleanupDeployment:Never  
  
  
 * */


namespace LeakTestDatacollector
{
    //[TestClass]
    //public class foobar
    //{
    //    public static TestContext TestContext;
    //    [ClassInitialize]
    //    public static void ClassInitialize(TestContext testContext)
    //    {
    //        TestContext = testContext;
    //        //StartVSAsync().Wait();
    //    }

    //    [TestMethod]
    //    public void Foo()
    //    {
    //        Assert.Fail();
    //    }
    //}

    [TestClass]
    public class UnitTest1
    {
        /// <summary>
        /// The process we're monitoring
        /// </summary>
        public static Process _targetProc;
        protected static EnvDTE.DTE _vsDTE;
        protected static EnvDTE.SolutionEvents _solutionEvents; // need a strong ref to survive GCs

        protected static int DelayMultiplier = 1;
        /// <summary>
        /// When executing a specific iteration, take a snapshot dump of the target process
        /// Then we can diff the counts from the final dump
        /// The snapshot will be taken at the end of iteration NumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot
        /// </summary>
        public int NumIterationsBeforeTotalToTakeBaselineSnapshot = 3;
        public static TestContext TestContext;
        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            TestContext = testContext;
            StartVSAsync().Wait();
        }

        //        [DeploymentItem("asdf")]
        [ClassCleanup]
        public static void ClassCleanup()
        {
            ShutDownVSAsync().Wait();
        }

        bool fDidit = false;
        [TestMethod]
        public void TestMethod1()
        {
            if (!fDidit)
            {
//                recur()
                fDidit = true;
                //StartVSAsync().Wait();
                LogMessage($"here i am with vs started");
            }
            string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
            OpenCloseSolutionOnce(SolutionToLoad).Wait();
        }

        static TaskCompletionSource<int> _tcsSolution = new TaskCompletionSource<int>();
        private static void SolutionEvents_AfterClosing()
        {
            //LogMessage($"{nameof(SolutionEvents_AfterClosing)}");
            _tcsSolution.TrySetResult(0);
        }

        private static void SolutionEvents_Opened()
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

        public Dictionary<string, List<uint>> _measurements = new Dictionary<string, List<uint>>(); // ctrname=> measurements per iteration

        public static async Task StartVSAsync()
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
        internal static async Task ShutDownVSAsync()
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

        public static List<string> _lstLoggedStrings = new List<string>();

        public static void LogMessage(string str, params object[] args)
        {
            var dt = string.Format("[{0}],",
                DateTime.Now.ToString("hh:mm:ss:fff")
                );
            str = string.Format(dt + str, args);
            var msgstr = DateTime.Now.ToString("hh:mm:ss:fff") + $" {Thread.CurrentThread.ManagedThreadId,2} {str}";

            TestContext.WriteLine(msgstr);
            if (Debugger.IsAttached)
            {
                Debug.WriteLine(msgstr);
            }
            _lstLoggedStrings.Add(msgstr);
        }

    }



}
