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
    public class DataCollectorStressTests
    {
        /// <summary>
        /// when iterating via DC, statics persist. 
        /// </summary>
        public static ILogger logger;

        static VSHandler vsHandler;
        static MeasurementHolder measurementHolder;
        static int IterationNumber;
        static string baseDumpFileName;

        static readonly int DelayMultiplier = 1;
        /// <summary>
        /// When executing a specific iteration, take a snapshot dump of the target process
        /// Then we can diff the counts from the final dump
        /// The snapshot will be taken at the end of iteration NumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot
        /// </summary>
        public int NumIterationsBeforeTotalToTakeBaselineSnapshot = 3;
        static readonly int NumITerationsAtWhichToTakeBaseSnapshot = 7;
        public static TestContext TestContext;
        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            // when iterating, a new TestContext for each iteration, so we can't store info on the TestContext Property bag
            TestContext = testContext;
            if (logger == null)
            {
                logger = new Logger(testContext);
                logger.LogMessage($"Starting {new StackTrace().GetFrames()[0].GetMethod().Name}");
            }
            measurementHolder = new MeasurementHolder(
                TestContext.TestName,
                PerfCounterData._lstPerfCounterDefinitionsForStressTest,
                SampleType.SampleTypeIteration,
                logger: logger,
                sensitivity: .1);


            vsHandler = new VSHandler(logger, DelayMultiplier);
            PerfCounterData.ProcToMonitor = vsHandler._targetProc;

            var vsPath = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\devenv.exe";

            vsHandler.StartVSAsync(vsPath).Wait();
        }
        /// <summary>
        /// after each iteration, take measurements
        /// </summary>
        /// <returns></returns>
        public async Task TakeMeasurementAsync(string desc)
        {
            //test.LogMessage($"{nameof(TakeMeasurementAsync)} {nIteration}");
            //                await Task.Delay(TimeSpan.FromSeconds(5 * test.DelayMultiplier));
            try
            {
                vsHandler._vsDTE?.ExecuteCommand("Tools.ForceGC");
                await Task.Delay(TimeSpan.FromSeconds(1 * DelayMultiplier));

                var res = measurementHolder.TakeMeasurement(desc);
                logger.LogMessage(res);
            }
            catch (Exception ex)
            {
                logger.LogMessage($"Exception in {nameof(TakeMeasurementAsync)}" + ex.ToString());
            }
        }

        //        [DeploymentItem("asdf")]
        [ClassCleanup]
        public static void ClassCleanup()
        {
            logger.LogMessage($"{nameof(ClassCleanup)}");

            var filenameResults = measurementHolder.DumpOutMeasurementsToTempFile(StartExcel: false);
            logger.LogMessage($"Measurement Results {filenameResults}");
            var task = measurementHolder.CalculateRegressionAsync(showGraph: true);
            task.Wait();
            var lstRegResults = task.Result.Where(r => r.IsRegression).ToList();
            if (lstRegResults.Count > 0)
            {
                var taskCreateFinalDump = measurementHolder.CreateDumpAsync(
                    vsHandler._targetProc.Id,
                    desc: TestContext.TestName + "_" + IterationNumber.ToString(),
                    memoryAnalysisType: MemoryAnalysisType.StartClrObjectExplorer);

                var currentDumpFile = taskCreateFinalDump.Result;
                if (!string.IsNullOrEmpty(baseDumpFileName))
                {
                    var oDumpAnalyzer = new DumperViewer.DumpAnalyzer(logger);
                    oDumpAnalyzer.GetDiff(baseDumpFileName, currentDumpFile, IterationNumber, IterationNumber - NumITerationsAtWhichToTakeBaseSnapshot);
                }
                foreach (var regres in lstRegResults)
                {
                    logger.LogMessage($"Regression!!!!! {regres}");
                }
                Assert.Fail($"Leaks found. Failing test");
            }

            vsHandler.ShutDownVSAsync().Wait();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            logger.LogMessage($"{nameof(TestInitialize)}");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            logger.LogMessage($"{nameof(TestCleanup)}");
        }

        static bool fDidit = false;
        [TestMethod]
        public async Task DCStressOpenCloseSolution()
        {
            if (!fDidit)
            {
                //                recur()
                fDidit = true;
                logger.LogMessage($"here i am in 1st iteration");
                //TestContext.Properties[nameof(MeasurementHolder)] = measurementHolder;
                //TestContext.Properties["ITERATION"] = 1;
            }
            logger.LogMessage($"Starting {nameof(DCStressOpenCloseSolution)}");
            string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";

            await vsHandler.OpenSolution(SolutionToLoad);
            await vsHandler.CloseSolution();
            IterationNumber++;
            await TakeMeasurementAsync($"Iteration # {IterationNumber}");
            if (IterationNumber == NumITerationsAtWhichToTakeBaseSnapshot)
            //            if (NumIterations > test.NumIterationsBeforeTotalToTakeBaselineSnapshot && iteration == NumIterations - test.NumIterationsBeforeTotalToTakeBaselineSnapshot - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(5 * DelayMultiplier));
                logger.LogMessage($"Taking base snapshot dump");
                baseDumpFileName = await measurementHolder.CreateDumpAsync(
                    vsHandler._targetProc.Id,
                    desc: TestContext.TestName + "_" + IterationNumber.ToString(),
                    memoryAnalysisType: MemoryAnalysisType.JustCreateDump);
            }
        }
    }
}
