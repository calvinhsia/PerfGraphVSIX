using LeakTestDatacollector;
using Microsoft.Test.Stress;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestStress
{
    [TestClass]
    public class StressVS
    {
        public const string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";

        public TestContext TestContext { get; set; }
        ILogger logger;
        IVSHandler _VSHandler;
        [TestInitialize]
        public async Task TestInitialize()
        {
            logger = new Logger(new TestContextWrapper(TestContext));
            _VSHandler = StressUtil.CreateVSHandler(logger);

            await _VSHandler.StartVSAsync();
            logger.LogMessage($"TestInit starting VS pid= {_VSHandler.VsProcess.Id}");
            await _VSHandler.EnsureGotDTE(TimeSpan.FromSeconds(60));
            await _VSHandler.DteExecuteCommandAsync("View.ErrorList");

        }

        [TestMethod]
        [ExpectedException(typeof(LeakException))]
        public async Task StressOpenCloseSln()
        {
            try
            {
                // the only change to existing test required: call to static method
                await StressUtil.DoIterationsAsync(this, stressUtilOptions: new StressUtilOptions()
                {
                    FailTestAsifLeaksFound = true,
                    NumIterations = 17,
                    actExecuteAfterEveryIterationAsync = async (nIter, measurementHolder) =>
                    {
                        await Task.Yield();
                        return true; //do the default action after iteration of checking iteration number and taking dumps, comparing.
                    }
                });

                await _VSHandler.OpenSolutionAsync(SolutionToLoad);

                await _VSHandler.CloseSolutionAsync();
            }
            catch (Exception ex)
            {
                logger.LogMessage($"Exception {ex}");
                throw;
            }
        }


        [TestMethod]
        [Ignore]
        [ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        public async Task StressVSOpenCloseWaitTilQuiet()
        {
            // we can't measure for quiet on the current process because the current process will be actively doing stuff.
            if (StressUtilOptions.IsRunningOnBuildMachine())
            {
                throw new LeakException("Throwing expected exception so test passes", null);
            }
            string didGetLeakException = "didGetLeakException";
            int numIter = 11;
            try
            {
                await StressUtil.DoIterationsAsync(
                    this,
                    new StressUtilOptions()
                    {
                        LoggerLogOutputToDestkop = true,
                        NumIterations = numIter,
                        ShowUI = false,
                        actExecuteAfterEveryIterationAsync = async (nIter, measurementHolder) =>
                        {
                            // we want to take measures in a circular buffer and wait til those are quiet
                            var circBufferSize = 5;
                            var numTimesToGetQuiet = 50;
                            var quietMeasure = new MeasurementHolder(
                                "Quiet",
                                new StressUtilOptions()
                                {
                                    NumIterations = 1, // we'll do 1 iteration 
                                    pctOutliersToIgnore = 0,
                                    logger = measurementHolder.Logger,
                                    VSHandler = measurementHolder.stressUtilOptions.VSHandler,
                                    lstPerfCountersToUse = measurementHolder.stressUtilOptions.lstPerfCountersToUse,
                                }, SampleType.SampleTypeIteration
                            );
                            // We just took a measurement, so copy those values to init our buffer
                            foreach (var pctrMeasure in measurementHolder.measurements.Keys)
                            {
                                var lastVal = measurementHolder.measurements[pctrMeasure][measurementHolder.nSamplesTaken - 1];
                                quietMeasure.measurements[pctrMeasure].Add(lastVal);
                            }
                            quietMeasure.nSamplesTaken++;

                            var isQuiet = false;
                            int nMeasurementsForQuiet = 0;
                            while (!isQuiet && nMeasurementsForQuiet < numTimesToGetQuiet)
                            {
                                await quietMeasure.DoForceGCAsync();
                                await Task.Delay(TimeSpan.FromSeconds(1 * measurementHolder.stressUtilOptions.DelayMultiplier)); // after GC, wait 1 before taking measurements
                                var sb = new StringBuilder($"Measure for Quiet iter = {nIter} QuietSamp#= {nMeasurementsForQuiet}");
                                quietMeasure.TakeRawMeasurement(sb);
                                measurementHolder.Logger.LogMessage(sb.ToString());///xxxremove
                                if (quietMeasure.nSamplesTaken == circBufferSize)
                                {
                                    var lk = await quietMeasure.CalculateLeaksAsync(
                                        showGraph: false,
                                        GraphsAsFilePrefix:
#if DEBUG
                                    "Graph"
#else
                                    null
#endif
                                    );
                                    isQuiet = true;
                                    foreach (var k in lk.Where(p => !p.IsQuiet()))
                                    {
                                        measurementHolder.Logger.LogMessage($"  !quiet {k}"); ///xxxremove
                                        isQuiet = false;
                                    }
                                    //                                    isQuiet = !lk.Where(k => !k.IsQuiet()).Any();

                                    foreach (var pctrMeasure in quietMeasure.measurements.Keys) // circular buffer: remove 1st item
                                    {
                                        quietMeasure.measurements[pctrMeasure].RemoveAt(0);
                                    }
                                    quietMeasure.nSamplesTaken--;
                                }
                                nMeasurementsForQuiet++;
                            }
                            if (isQuiet) // the counters have stabilized. We'll use the stabilized numbers as the sample value for the iteration
                            {
                                measurementHolder.Logger.LogMessage($"Gone quiet in {nMeasurementsForQuiet} measures");
                            }
                            else
                            {
                                measurementHolder.Logger.LogMessage($"Didn't go quiet in {numTimesToGetQuiet}");
                            }
                            // Whether or not it's quiet, we'll take the most recent measure as the iteration sample
                            foreach (var pctrMeasure in measurementHolder.measurements.Keys)
                            {
                                var lastVal = quietMeasure.measurements[pctrMeasure][quietMeasure.nSamplesTaken - 1];
                                measurementHolder.measurements[pctrMeasure][measurementHolder.nSamplesTaken - 1] = lastVal;
                            }

                            return true; // continue with normal iteration processing
                        }
                    });

                await _VSHandler.OpenSolutionAsync(SolutionToLoad);

                await _VSHandler.CloseSolutionAsync();
            }
            catch (LeakException)
            {
                TestContext.Properties[didGetLeakException] = 1;
                throw;
            }
        }


        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            await _VSHandler?.ShutDownVSAsync();
        }
    }

    [TestClass]
    public class StressExistingVS
    {
        ILogger logger;
        public TestContext TestContext { get; set; }
        IVSHandler _VSHandler;

        [TestInitialize]
        public async Task TestInitialize()
        {
            logger = new Logger(new TestContextWrapper(TestContext));
            _VSHandler = StressUtil.CreateVSHandler(logger);
            await _VSHandler.StartVSAsync();
            logger.LogMessage($"TestInit starting VS pid= {_VSHandler.VsProcess.Id}");
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            await _VSHandler.ShutDownVSAsync();
        }

        //        [TestMethod]
        public async Task StressStartVSApexSim() // Apex starts VS and we'll look for it. Simulate by starting vs directly in TestInitialize
        {
            // the only change to existing test required: call to static method
            await StressUtil.DoIterationsAsync(this, NumIterations: 3);

            await _VSHandler.OpenSolutionAsync(StressVS.SolutionToLoad);

            await _VSHandler.CloseSolutionAsync();
        }
    }

    [TestClass]
    public class StressExistingVSNoVSHandlerField
    {
        System.Diagnostics.Process procVS;
        ILogger logger;
        public TestContext TestContext { get; set; }
        [TestInitialize]
        public void TestInitialize()
        {
            logger = new Logger(new TestContextWrapper(TestContext));
            var vsHandler = StressUtil.CreateVSHandler(null);

            procVS = Process.Start(vsHandler.GetVSFullPath()); // simulate Apex starting VS
            logger.LogMessage($"TestInit starting VS pid= {procVS.Id}");
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            IVSHandler VSHandler = TestContext.Properties[StressUtil.PropNameVSHandler] as IVSHandler;
            await VSHandler.ShutDownVSAsync();
        }

        [TestMethod]
        public async Task StressStartVSApexSimNoVSHandler() // Apex starts VS and we'll look for it. Simulate by starting vs directly in TestInitialize
        {
            try
            {
                await StressUtil.DoIterationsAsync(this, NumIterations: 2);


                if (!(TestContext.Properties[StressUtil.PropNameVSHandler] is IVSHandler vSHandler))
                {
                    throw new InvalidOperationException("null vshandler");
                }
                await vSHandler.OpenSolutionAsync(StressVS.SolutionToLoad);

                await vSHandler.CloseSolutionAsync();

            }
            catch (Exception ex)
            {
                logger.LogMessage($"Exception {ex}");
                throw;
            }
        }

        [TestClass]
        public class StressExistingVSNoVSHandlerFieldLater
        {
            ILogger logger;
            public TestContext TestContext { get; set; }
            [TestInitialize]
            public void TestInitialize()
            {
                logger = new Logger(new TestContextWrapper(TestContext));
                logger.LogMessage($"TestInit : not starting VS immediately");
                var tsk = Task.Run(async () =>
                {
                    int nDelay = 5;
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        nDelay = 30;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(nDelay));
                    logger.LogMessage($"TestInit : starting VS after {nDelay} secs delay");
                    var vsHandler = StressUtil.CreateVSHandler(null);
                    Process.Start(vsHandler.GetVSFullPath()); // simulate Apex starting VS
                });

            }

            [TestCleanup]
            public async Task TestCleanupAsync()
            {
                IVSHandler VSHandler = TestContext.Properties[StressUtil.PropNameVSHandler] as IVSHandler;
                await VSHandler.ShutDownVSAsync();
            }

            [TestMethod]
            public async Task StressStartVSLaterApexSimNoVSHandler() // Apex starts VS and we'll look for it. Simulate by starting vs directly in TestInitialize
            {
                try
                {
                    // the only change to existing test required: call to static method
                    await StressUtil.DoIterationsAsync(this, NumIterations: 2);


                    if (!(TestContext.Properties[StressUtil.PropNameVSHandler] is IVSHandler vSHandler))
                    {
                        throw new InvalidOperationException("null vshandler");
                    }

                    await vSHandler.OpenSolutionAsync(StressVS.SolutionToLoad);

                    await vSHandler.CloseSolutionAsync();

                }
                catch (Exception ex)
                {
                    logger.LogMessage($"Exception {ex}");
                    throw;
                }
            }
        }

    }


}
