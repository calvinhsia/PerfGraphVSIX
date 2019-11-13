using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    [TestClass]
    public class DataCollectorStressTestsManualIterLeaky
    {
        public ILogger logger;

        public TestContext TestContext { get; set; }
        [TestInitialize]
        public async Task TestInitialize()
        {
            // when iterating, a new TestContext for each iteration, so we can't store info on the TestContext Property bag
            if (logger == null)
            {
                logger = new Logger(TestContext);
                logger.LogMessage($"Starting {new StackTrace().GetFrames()[0].GetMethod().Name}");
            }
            await DoIterationsAsync(this, NumIterations: 11, Sensitivity: 1);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            logger.LogMessage($"{nameof(TestCleanup)}");
        }

        class BigStuffWithLongNameSoICanSeeItBetter
        {
            readonly byte[] arr = new byte[1024 * 1024];
            public byte[] GetArray => arr;
        }

        static readonly List<BigStuffWithLongNameSoICanSeeItBetter> _lst = new List<BigStuffWithLongNameSoICanSeeItBetter>();

        [TestMethod]
        public void Leaky()
        {
            // to test if your code leaks, put it here. Repeat a lot to magnify the effect
            for (int i = 0; i < 1; i++)
            {
                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
            }
        }


        /// <summary>
        /// Do it all: tests need only add a single line to TestInitialize to turn a normal test into a stress test
        /// </summary>
        /// <param name="stressWithNoInheritance"></param>
        /// <param name="NumIterations"></param>
        /// <returns></returns>
        public static async Task DoIterationsAsync(
            object test, 
            int NumIterations, 
            double Sensitivity = 1.0f,
            int DelayMultiplier = 1,
            int NumIterationsBeforeTotalToTakeBaselineSnapshot = 4)
        {
            var typ = test.GetType();
            var methGetContext = typ.GetMethod("get_TestContext");
            if (methGetContext == null)
            {
                throw new InvalidOperationException("can't get TestContext from test. Test must have   'public TestContext TestContext { get; set; }'");
            }
            var testContext = methGetContext.Invoke(test, null) as TestContext;

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
                    throw new InvalidOperationException("Couldn't find ILogger");
                }
            }
            logger.LogMessage($"{nameof(DoIterationsAsync)} TestName = {testContext.TestName}");
            var measurementHolder = new MeasurementHolder(
                testContext.TestName,
                PerfCounterData._lstPerfCounterDefinitionsForStressTest,
                SampleType.SampleTypeIteration,
                logger: logger,
                sensitivity: Sensitivity);
            testContext.Properties[nameof(MeasurementHolder)] = measurementHolder;

            var baseDumpFileName = string.Empty;
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
                    await Task.Delay(TimeSpan.FromSeconds(1 * DelayMultiplier));
                }

                var res = measurementHolder.TakeMeasurement($"Iter {iteration + 1}/{NumIterations}");
                logger.LogMessage(res);

                if (NumIterations > NumIterationsBeforeTotalToTakeBaselineSnapshot && iteration == NumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5 * DelayMultiplier));
                    logger.LogMessage($"Taking base snapshot dump");
                    baseDumpFileName = await measurementHolder.CreateDumpAsync(
                        PerfCounterData.ProcToMonitor.Id,
                        desc: testContext.TestName + "_" + iteration.ToString(),
                        memoryAnalysisType: MemoryAnalysisType.JustCreateDump);
                }
            }
            var filenameResults = measurementHolder.DumpOutMeasurementsToTempFile(StartExcel: false);
            logger.LogMessage($"Measurement Results {filenameResults}");
            var lstRegResults = (await measurementHolder.CalculateRegressionAsync(showGraph: true)).Where(r => r.IsRegression).ToList();
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
                    oDumpAnalyzer.GetDiff(baseDumpFileName, currentDumpFile, NumIterations, NumIterationsBeforeTotalToTakeBaselineSnapshot);
                }
                Assert.Fail($"Leaks found");
            }
        }
    }
}
