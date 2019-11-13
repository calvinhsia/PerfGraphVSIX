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
        public Process TargetProc => _VSHandler.TargetProc;
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
                        test.TargetProc.Id,
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
                    test.TargetProc.Id,
                    desc: test.TestContext.TestName + "_" + NumIterations.ToString(),
                    memoryAnalysisType: MemoryAnalysisType.StartClrObjectExplorer);
                if (!string.IsNullOrEmpty(baseDumpFileName))
                {
                    var oDumpAnalyzer = new DumperViewer.DumpAnalyzer(test);
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
    }
}