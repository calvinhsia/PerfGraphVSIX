﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PerfGraphVSIX
{
    public interface IVisualStudio
    {
        void DoGarbageCollect();
    }

    public class StressUtil
    {
        /// <summary>
        /// Do it all: tests need only add a single call to turn the test into a stress test
        /// The call can be made from the TestInitialize or the beginning of the TestMethod
        /// </summary>
        /// <param name="test">pass the test itself</param>
        /// <param name="NumIterations">Specify the number of iterations to do. Some scenarios are large (open/close solution)
        /// Others are small (scroll up/down a file)</param>
        /// <param name="Sensitivity">Defaults to 1 (multiplier). Some perf counter thresholds are large (e.g. 1 megabyte for Private bytes). To find
        /// smaller leaks, set this to .1. If too sensitive, set this to 10 (for 10 Meg threshold) </param>
        /// <param name="DelayMultiplier">Defaults to 1. All delays (e.g. between iterations, after GC, are multiplied by this factor.
        /// Running the test with instrumented binaries (such as under http://Toolbox/MemSpect  will slow down the target process</param>
        /// <param name="NumIterationsBeforeTotalToTakeBaselineSnapshot"> Specifies the iteration # at which to take a baseline. 
        /// <paramref name="NumIterationsBeforeTotalToTakeBaselineSnapshot"/> is subtracted from <paramref name="NumIterations"/> to get the baseline iteration number
        /// e.g. 100 iterations, with <paramref name="NumIterationsBeforeTotalToTakeBaselineSnapshot"/>=4 means take a baseline at iteartion 100-4==96;
        /// </param>
        /// <returns></returns>
        public static async Task DoIterationsAsync(
            object test,
            int NumIterations,
            double Sensitivity = 1.0f,
            int DelayMultiplier = 1,
            string ProcNameToMonitor = "devenv",
            int NumIterationsBeforeTotalToTakeBaselineSnapshot = 4)
        {
            const string PropNameiteration = "IterationNumber";
            const string PropNameRecursionPrevention = "RecursionPrevention";
            var typ = test.GetType();
            var methGetContext = typ.GetMethod($"get_{nameof(TestContext)}");
            if (methGetContext == null || !(methGetContext.Invoke(test, null) is TestContext testContext))
            {
                throw new InvalidOperationException("can't get TestContext from test. Test must have 'public TestContext TestContext { get; set; }' (perhaps inherited)");
            }
            if (testContext.Properties[PropNameRecursionPrevention] != null)
            {
                return;
            }
            testContext.Properties[PropNameRecursionPrevention] = 1;

            var _theTestMethod = typ.GetMethods().Where(m => m.Name == testContext.TestName).First();
            ILogger logger = test as ILogger;
            if (logger == null)
            {
                var loggerFld = typ.GetField("logger");
                if (loggerFld != null)
                {
                    logger = loggerFld.GetValue(test) as ILogger;
                }
                if (logger == null)
                {
                    logger = new Logger(testContext);
                }
            }
            logger.LogMessage($"{nameof(DoIterationsAsync)} TestName = {testContext.TestName}");

            // we don't want to add DTE refs to this project
            var vsHandlerFld = typ.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(m => m.FieldType.Name == "VSHandler").FirstOrDefault();
            IVisualStudio ivisualStudio = null;
            if (vsHandlerFld != null)
            {
                ivisualStudio = vsHandlerFld.GetValue(test) as IVisualStudio;
            }

            var measurementHolder = new MeasurementHolder(
                testContext.TestName,
                PerfCounterData._lstPerfCounterDefinitionsForStressTest,
                SampleType.SampleTypeIteration,
                logger: logger,
                sensitivity: Sensitivity);

            var baseDumpFileName = string.Empty;
            testContext.Properties[PropNameiteration] = 0;
            for (int iteration = 0; iteration < NumIterations; iteration++)
            {
                var result = _theTestMethod.Invoke(test, parameters: null);
                if (_theTestMethod.ReturnType.Name == "Task")
                {
                    var resultTask = (Task)result;
                    await resultTask;
                }
                if (PerfCounterData.ProcToMonitor.Id == Process.GetCurrentProcess().Id)
                {
                    GC.Collect();
                }
                else
                {
                    ivisualStudio?.DoGarbageCollect();
                    //todo: forcegc                 test.VsDTE?.ExecuteCommand("Tools.ForceGC");
                }
                await Task.Delay(TimeSpan.FromSeconds(1 * DelayMultiplier));

                var res = measurementHolder.TakeMeasurement($"Iter {iteration + 1}/{NumIterations}");
                logger.LogMessage(res);

                if (NumIterations > NumIterationsBeforeTotalToTakeBaselineSnapshot &&
                    iteration == NumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5 * DelayMultiplier));
                    logger.LogMessage($"Taking base snapshot dump");
                    baseDumpFileName = await measurementHolder.CreateDumpAsync(
                        PerfCounterData.ProcToMonitor.Id,
                        desc: testContext.TestName + "_" + iteration.ToString(),
                        memoryAnalysisType: MemoryAnalysisType.JustCreateDump);
                }
                testContext.Properties[PropNameiteration] = (int)(testContext.Properties[PropNameiteration]) + 1;
            }
            var filenameResultsCSV = measurementHolder.DumpOutMeasurementsToTempFile(StartExcel: false);
            logger.LogMessage($"Measurement Results {filenameResultsCSV}");
            var lstRegResults = (await measurementHolder.CalculateRegressionAsync(showGraph: true))
                .Where(r => r.IsRegression).ToList();
            if (lstRegResults.Count > 0)
            {
                foreach (var regres in lstRegResults)
                {
                    logger.LogMessage($"Regression!!!!! {regres}");
                }
                var currentDumpFile = await measurementHolder.CreateDumpAsync(
                    PerfCounterData.ProcToMonitor.Id,
                    desc: testContext.TestName + "_" + NumIterations.ToString(),
                    memoryAnalysisType: MemoryAnalysisType.StartClrObjectExplorer);
                if (!string.IsNullOrEmpty(baseDumpFileName))
                {
                    var oDumpAnalyzer = new DumperViewer.DumpAnalyzer(logger);
                    oDumpAnalyzer.GetDiff(baseDumpFileName,
                                    currentDumpFile,
                                    NumIterations,
                                    NumIterationsBeforeTotalToTakeBaselineSnapshot);
                }
                else
                {
                    logger.LogMessage($"No baseline dump: not enough iterations {NumIterations} {NumIterationsBeforeTotalToTakeBaselineSnapshot}");
                }
                Assert.Fail($"Leaks found");
            }
        }
    }

    public class Logger : ILogger
    {
        public static List<string> _lstLoggedStrings = new List<string>();
        private readonly TestContext testContext;
        private string logFilePath;

        public Logger(TestContext testContext)
        {
            this.testContext = testContext;
        }

        public void LogMessage(string str, params object[] args)
        {
            var dt = string.Format("[{0}],",
                DateTime.Now.ToString("hh:mm:ss:fff")
                );
            str = string.Format(dt + str, args);
            var msgstr = DateTime.Now.ToString("hh:mm:ss:fff") + $" {Thread.CurrentThread.ManagedThreadId,2} {str}";

            testContext.WriteLine(msgstr);
            if (Debugger.IsAttached)
            {
                Debug.WriteLine(msgstr);
            }
            _lstLoggedStrings.Add(msgstr);

            try
            {
                if (string.IsNullOrEmpty(logFilePath))
                {
                    //   logFilePath = @"c:\Test\StressDataCollector.log";
                    logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "TestStressDataCollector.log"); //can't use the Test deployment folder because it gets cleaned up
                }
                File.AppendAllText(logFilePath, msgstr + Environment.NewLine);
            }
            catch (Exception)
            {
            }

        }
    }
}
