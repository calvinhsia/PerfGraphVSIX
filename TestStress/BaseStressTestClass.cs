using DumperViewer;
using LeakTestDatacollector;
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
        public const string vsPath = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\devenv.exe";

        public VSHandler _VSHandler;
        /// <summary>
        /// The process we're monitoring
        /// </summary>
        public Process TargetProc => _VSHandler.vsProc;
        protected EnvDTE.DTE VsDTE => _VSHandler._vsDTE;

        protected int DelayMultiplier = 1;
        /// <summary>
        /// When executing a specific iteration, take a snapshot dump of the target process
        /// Then we can diff the counts from the final dump
        /// The snapshot will be taken at the end of iteration NumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot
        /// </summary>
        public int NumIterationsBeforeTotalToTakeBaselineSnapshot = 3;

        public TestContext TestContext { get; set; }

        public virtual async Task InitializeAsync()
        {
            _VSHandler = new VSHandler(this, DelayMultiplier);
            await _VSHandler.StartVSAsync(vsPath);
        }

        public virtual async Task CleanupAsync()
        {
            await _VSHandler.ShutDownVSAsync();
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
                test.VsDTE?.ExecuteCommand("Tools.ForceGC");
                await Task.Delay(TimeSpan.FromSeconds(1 * test.DelayMultiplier));

                var res = measurementHolder.TakeMeasurement(desc);
                test.LogMessage(res);
            }
            catch (Exception ex)
            {
                test.LogMessage($"Exception in {nameof(TakeMeasurementAsync)}" + ex.ToString());
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
    }
}