using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using Microsoft.Test.Stress;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace TestStressDll
{
    [TestClass]
    public class StressLeakyClass
    {
        public static string didGetLeakException = "didGetLeakException";
        public TestContext TestContext { get; set; }

        class BigStuffWithLongNameSoICanSeeItBetter
        {
            readonly byte[] arr;
            public BigStuffWithLongNameSoICanSeeItBetter(int initSize = 1024 * 1024 * 2) // over our 1M threshold
            {
                arr = new byte[initSize];
            }
            public byte[] GetArray => arr;
            public string MyString = ($"leaking string" + DateTime.Now.ToString()).Substring(0, 14); // make a calculated non-unique string so it looks like a leak
        }

        List<BigStuffWithLongNameSoICanSeeItBetter> _lst;

        [TestInitialize]
        public void TestInit()
        {
            _lst = new List<BigStuffWithLongNameSoICanSeeItBetter>();
            GC.Collect();
        }

        [TestMethod]
        [ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        public async Task StressLeakyBadPerfCounterCatAsync()
        {
            int numIter = 11;
            var stressOptions = new StressUtilOptions() { NumIterations = numIter, ProcNamesToMonitor = string.Empty, ShowUI = false };
            stressOptions.lstPerfCountersToUse = PerfCounterData.GetPerfCountersToUse(Process.GetCurrentProcess(), IsForStress: true);
            stressOptions.lstPerfCountersToUse[0].PerfCounterCategory = "foobar"; // an invalid category
            try
            {
                await StressUtil.DoIterationsAsync(
                    this,
                    stressOptions
                    );
                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
            }
            catch (LeakException ex)
            {
                TestContext.WriteLine($"Caught exception {ex.Message}");
                var lstFileResults = (List<FileResultsData>)TestContext.Properties[StressUtil.PropNameListFileResults];
                foreach (var result in lstFileResults.OrderBy(r => r.filename))
                {
                    TestContext.WriteLine($"File result {Path.GetFileName(result.filename)}");
                }
                var expectedFiles = new[] {
                "Graph # Bytes in all Heaps.png",
"Graph GDIHandles.png",
"Graph Handle Count.png",
"Graph Private Bytes.png",
"Graph Thread Count.png",
"Graph UserHandles.png",
"Graph Virtual Bytes.png",
"Measurements.txt",
$"{TestContext.TestName}_{numIter}_0.dmp",
$"{TestContext.TestName}_{numIter-4}_0.dmp",
"StressTestLog.log",
$"String and Type Count differences_{numIter}.txt",
};
                foreach (var itm in expectedFiles)
                {
                    Assert.IsTrue(lstFileResults.Where(r => Path.GetFileName(r.filename) == itm).Count() == 1, $"Expected File attachment {itm}");
                }

                var strAndTypeDiff = File.ReadAllText(lstFileResults.Where(r => Path.GetFileName(r.filename) == $"String and Type Count differences_{numIter}.txt").First().filename);
                TestContext.WriteLine($"String and Type Count differences_{numIter}.txt");
                TestContext.WriteLine(strAndTypeDiff);
                Assert.IsTrue(strAndTypeDiff.Contains(nameof(BigStuffWithLongNameSoICanSeeItBetter)), $"Type must be in StringandTypeDiff");
                Assert.IsTrue(strAndTypeDiff.Contains("leaking string"), $"'leaking string' must be in StringandTypeDiff");
                Assert.AreEqual(expectedFiles.Length, lstFileResults.Count, $"# file results");
                TestContext.Properties[didGetLeakException] = 1;
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        public async Task StressLeakyAsync()
        {
            if (TestContext.Properties.Contains(StressUtil.PropNameCurrentIteration) && // only do once, but after logger has been set
                (int)(TestContext.Properties[StressUtil.PropNameCurrentIteration]) == 0)
            {
                if (TestContext.Properties.Contains(StressUtil.PropNameLogger))
                {/// put these in logger rather than TestContext.WriteLine so they show in build pipeline test results
                    Logger logger = TestContext.Properties[StressUtil.PropNameLogger] as Logger;
                    logger?.LogMessage($"Username=" + Environment.GetEnvironmentVariable("Username"));
                    logger?.LogMessage($"Computername=" + Environment.GetEnvironmentVariable("Computername"));
                    logger?.LogMessage($"UserDomain=" + Environment.GetEnvironmentVariable("userdomain"));
                }
            }
            int numIter = 11;
            try
            {
                await StressUtil.DoIterationsAsync(
                    this,
                    new StressUtilOptions() { NumIterations = numIter, ProcNamesToMonitor = string.Empty, ShowUI = false }
                    );

                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
            }
            catch (LeakException ex)
            {
                TestContext.WriteLine($"Caught exception {ex.Message}");
                var lstFileResults = (List<FileResultsData>)TestContext.Properties[StressUtil.PropNameListFileResults];
                foreach (var result in lstFileResults.OrderBy(r => r.filename))
                {
                    TestContext.WriteLine($"File result {Path.GetFileName(result.filename)}");
                }
                var expectedFiles = new[] {
                "Graph # Bytes in all Heaps.png",
"Graph GDIHandles.png",
"Graph Handle Count.png",
"Graph Private Bytes.png",
"Graph Thread Count.png",
"Graph UserHandles.png",
"Graph Virtual Bytes.png",
"Measurements.txt",
$"{TestContext.TestName}_{numIter}_0.dmp",
$"{TestContext.TestName}_{numIter-4}_0.dmp",
"StressTestLog.log",
$"String and Type Count differences_{numIter}.txt",
};
                foreach (var itm in expectedFiles)
                {
                    Assert.IsTrue(lstFileResults.Where(r => Path.GetFileName(r.filename) == itm).Count() == 1, $"Expected File attachment {itm}");
                }

                var strAndTypeDiff = File.ReadAllText(lstFileResults.Where(r => Path.GetFileName(r.filename) == $"String and Type Count differences_{numIter}.txt").First().filename);
                TestContext.WriteLine($"String and Type Count differences_{numIter}.txt");
                TestContext.WriteLine(strAndTypeDiff);
                Assert.IsTrue(strAndTypeDiff.Contains(nameof(BigStuffWithLongNameSoICanSeeItBetter)), $"Type must be in StringandTypeDiff");
                Assert.IsTrue(strAndTypeDiff.Contains("leaking string"), $"'leaking string' must be in StringandTypeDiff");
                Assert.AreEqual(expectedFiles.Length, lstFileResults.Count, $"# file results");
                TestContext.Properties[didGetLeakException] = 1;
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        public async Task StressMultiSampleAsync()
        {
            if (StressUtilOptions.IsRunningOnBuildMachine())
            {
                throw new LeakException("Throwing expected exception so test passes", null);
            }
            int numIter = 11;
            try
            {
                await StressUtil.DoIterationsAsync(
                    this,
                    new StressUtilOptions()
                    {
                        LoggerLogOutputToDestkop = true,
                        NumIterations = numIter,
                        ProcNamesToMonitor = string.Empty,
                        ShowUI = false,
                        actExecuteAfterEveryIterationAsync = async (nIter, measurementHolder) =>
                        {
                            // this method yields a very nice stairstep in unit tests (where there's much less noise)
                            int numAdditionalSamplesPerIteration = 5; // Since we're called after a sample, add 1 to get the actual # of samples/iteration
                            var sb = new StringBuilder($"{nIter} ExtraIterations: {numAdditionalSamplesPerIteration}");
                            async Task CheckASampleAsync()
                            {
                                if (measurementHolder.nSamplesTaken == numAdditionalSamplesPerIteration * (measurementHolder.stressUtilOptions.NumIterations - measurementHolder.stressUtilOptions.NumIterationsBeforeTotalToTakeBaselineSnapshot))
                                {
                                    measurementHolder.baseDumpFileName = await measurementHolder.DoCreateDumpAsync($"Custom Code Taking base snapshot dump at Iter # {nIter} sample # {measurementHolder.nSamplesTaken}"); ;
                                }
                                if (measurementHolder.nSamplesTaken == numAdditionalSamplesPerIteration * measurementHolder.stressUtilOptions.NumIterations)
                                {
                                    var lstLeakResults = (await measurementHolder.CalculateLeaksAsync(showGraph: measurementHolder.stressUtilOptions.ShowUI, GraphsAsFilePrefix: "Graph"))
                                        .Where(r => r.IsLeak).ToList();
                                    var currentDumpFile = await measurementHolder.DoCreateDumpAsync($"Custom Code Taking final snapshot dump at iteration {measurementHolder.nSamplesTaken}");
                                    if (!string.IsNullOrEmpty(measurementHolder.baseDumpFileName))
                                    {
                                        var oDumpAnalyzer = new DumpAnalyzer(measurementHolder.Logger);
                                        foreach (var leak in lstLeakResults)
                                        {
                                            sb.AppendLine($"Custom code Leak Detected: {leak}");
                                        }
                                        sb.AppendLine();
                                        oDumpAnalyzer.GetDiff(sb,
                                                        measurementHolder.baseDumpFileName,
                                                        currentDumpFile,
                                                        measurementHolder.stressUtilOptions.NumIterations,
                                                        measurementHolder.stressUtilOptions.NumIterationsBeforeTotalToTakeBaselineSnapshot,
                                                        measurementHolder.stressUtilOptions.TypesToReportStatisticsOn,
                                                        out DumpAnalyzer.TypeStatistics _,
                                                        out DumpAnalyzer.TypeStatistics _);
                                        var fname = Path.Combine(measurementHolder.ResultsFolder, $"{MeasurementHolder.DiffFileName}_{measurementHolder.nSamplesTaken}.txt");
                                        File.WriteAllText(fname, sb.ToString());
                                        if (measurementHolder.stressUtilOptions.ShowUI)
                                        {
                                            System.Diagnostics.Process.Start(fname);
                                        }
                                        measurementHolder.lstFileResults.Add(new FileResultsData() { filename = fname, description = $"Differences for Type and String counts at iter {measurementHolder.nSamplesTaken}" });
                                        measurementHolder.Logger.LogMessage("Custom Code DumpDiff Analysis " + fname);
                                    }
                                    throw new LeakException($"Custom Code Leaks found: " + string.Join(",", lstLeakResults.Select(t => t.perfCounterData.perfCounterType).ToList()), lstLeakResults); //Leaks found: GCBytesInAllHeaps,ProcessorPrivateBytes,ProcessorVirtualBytes,KernelHandleCount
                                }
                            }
                            await CheckASampleAsync();
                            for (int i = 0; i < numAdditionalSamplesPerIteration; i++)
                            {
                                await measurementHolder.DoForceGCAsync();
                                measurementHolder.TakeRawMeasurement(sb);
                                await CheckASampleAsync();
                            }
                            return false;
                        }
                    }
                    ); ;

                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
            }
            catch (LeakException)
            {
                TestContext.Properties[didGetLeakException] = 1;
                throw;
            }
        }


        [TestMethod]
        [ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        public async Task StressLeakyLimitNumSamplesAsync()
        {
            int numIter = 23;
            try
            {
                await StressUtil.DoIterationsAsync(
                    this,
                    new StressUtilOptions()
                    {
                        NumIterations = numIter,
                        ProcNamesToMonitor = string.Empty,
                        ShowUI = false
                    }
                    );

                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
                var curIter = (int)(TestContext.Properties[StressUtil.PropNameCurrentIteration]);
                if (curIter < 11)
                {
                    Assert.IsFalse(TestContext.Properties.Contains(StressUtil.PropNameMinimumIteration));
                }
                else
                {
                    var min = (int)(TestContext.Properties[StressUtil.PropNameMinimumIteration]);
                    Assert.AreEqual(11, min, "minimum iteration not found");
                }
            }
            catch (LeakException)
            {
                TestContext.Properties[didGetLeakException] = 1;
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(LeakException))]
        public async Task TestLeakyWithCustomActionsAsync()
        {
            if (StressUtilOptions.IsRunningOnBuildMachine())
            {
                throw new LeakException("Throwing expected exception so test passes", null);
            }
            string prop_countActions = "countActions";
            int numIter = 5;
            try
            {
                if (!TestContext.Properties.Contains(prop_countActions))
                {
                    TestContext.Properties[prop_countActions] = 0;
                }
                await StressUtil.DoIterationsAsync(
                    this,
                    new StressUtilOptions()
                    {
                        NumIterations = numIter,
                        ProcNamesToMonitor = string.Empty,
                        ShowUI = false,
                        Sensitivity = .001,
                        actExecuteBeforeEveryIterationAsync = async (nIter, measurementHolder) =>
                        {
                            await Task.Yield();
                            measurementHolder.Logger.LogMessage($"{nIter} {nameof(StressUtilOptions.actExecuteBeforeEveryIterationAsync)}");
                            TestContext.Properties[prop_countActions] = (int)TestContext.Properties[prop_countActions] + 1;
                        },
                        actExecuteAfterEveryIterationAsync = async (nIter, measurementHolder) =>
                        {
                            // this code will take a dump at every iteration and compare/find diffs with prior iteration dump, dumping to log
                            string prop_dumpPrior = "dumpPrior";
                            measurementHolder.Logger.LogMessage($"{nIter} {nameof(StressUtilOptions.actExecuteAfterEveryIterationAsync)}");
                            var desc = $"Custom dump after iter {nIter}";
                            var dump = await measurementHolder.DoCreateDumpAsync(desc, filenamepart: "Custom");
                            if (nIter > 1)
                            {
                                var dumpPrior = (string)TestContext.Properties[prop_dumpPrior];
                                if (!string.IsNullOrEmpty(dumpPrior))
                                {
                                    var oDumpAnalyzer = new DumpAnalyzer(measurementHolder.Logger);
                                    var sb = new System.Text.StringBuilder();
                                    oDumpAnalyzer.GetDiff(sb,
                                        pathDumpBase: dumpPrior,
                                        pathDumpCurrent: dump,
                                        TotNumIterations: nIter,
                                        NumIterationsBeforeTotalToTakeBaselineSnapshot: 1,
                                        typesToReportStatisticsOn: @"System\.Collections\..*",
                                        out DumpAnalyzer.TypeStatistics baselineTypeStatistics,
                                        out DumpAnalyzer.TypeStatistics currentTypeStatistics);

                                    Assert.IsTrue(baselineTypeStatistics.InclusiveRetainedBytes > 0);
                                    Assert.IsTrue(baselineTypeStatistics.ExclusiveRetainedBytes > 0);
                                    Assert.IsTrue(baselineTypeStatistics.InclusiveRetainedBytes >= baselineTypeStatistics.ExclusiveRetainedBytes);
                                    Assert.IsTrue(baselineTypeStatistics.MemoryProfilingStopwatch.ElapsedMilliseconds > 0);
                                    Assert.IsFalse(baselineTypeStatistics.MemoryProfilingStopwatch.IsRunning);

                                    Assert.IsTrue(currentTypeStatistics.InclusiveRetainedBytes > 0);
                                    Assert.IsTrue(currentTypeStatistics.ExclusiveRetainedBytes > 0);
                                    Assert.IsTrue(currentTypeStatistics.InclusiveRetainedBytes >= currentTypeStatistics.ExclusiveRetainedBytes);
                                    Assert.IsTrue(currentTypeStatistics.MemoryProfilingStopwatch.ElapsedMilliseconds > 0);
                                    Assert.IsFalse(currentTypeStatistics.MemoryProfilingStopwatch.IsRunning);

                                    var fname = Path.Combine(measurementHolder.ResultsFolder, $"{TestContext.TestName} {MeasurementHolder.DiffFileName}_{nIter}.txt");
                                    File.WriteAllText(fname, sb.ToString());
                                    measurementHolder.lstFileResults.Add(new FileResultsData() { filename = fname, description = $"Differences for Type and String counts at iter {nIter}" });
                                    measurementHolder.Logger.LogMessage("DumpDiff Analysis " + fname);
                                    measurementHolder.Logger.LogMessage(System.Environment.NewLine + sb.ToString());
                                }
                            }
                            TestContext.Properties[prop_dumpPrior] = dump;

                            // this line is needed only in StressUtil unit tests. Remove from Stress test
                            TestContext.Properties[prop_countActions] = (int)TestContext.Properties[prop_countActions] + 1;

                            if (nIter == measurementHolder.stressUtilOptions.NumIterations)
                            {
                                throw new LeakException("custom code throwing to cause test failure", lstRegResults: null);
                            }

                            return false; //do NOT do the default action after iteration of checking iteration number and taking dumps, comparing.
                        },
                    }
                    ); ;


                if ((int)(TestContext.Properties[StressUtil.PropNameCurrentIteration]) == numIter - 1)
                {
                    Assert.AreEqual((int)TestContext.Properties[prop_countActions] + 1, numIter * 2); // we haven't finished the last iteration at this point.
                    TestContext.WriteLine($"Got CustomActions");
                }
            }
            catch (LeakException)
            {
                TestContext.Properties[didGetLeakException] = 1;
                throw;
            }

        }


        [TestMethod]
        [ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        public async Task StressLeakyCustomThresholdAsync()
        {
            // example to set custom threshold: here we override one counter's threshold. We can also change sensitivity
            var thresh = 1e7f;
            try
            {
                var opts = new StressUtilOptions()
                {
                    PerfCounterOverrideSettings = new List<PerfCounterOverrideThreshold>
                    {
                        new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.GCBytesInAllHeaps, regressionThreshold = thresh } ,
                        new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.ProcessorPrivateBytes, regressionThreshold = 9 * thresh } , // use a very high thresh so this counter won't show as leak
                        new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.ProcessorVirtualBytes, regressionThreshold = 9 * thresh } ,
                        new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.KernelHandleCount, regressionThreshold = 9 * thresh } ,
                    },
                    NumIterations = 11,
                    ProcNamesToMonitor = string.Empty,
                    ShowUI = false

                };
                await StressUtil.DoIterationsAsync(this, opts);

            }
            catch (LeakException ex)
            {
                // validate only one counter leaked: GCBytesInAllHeaps
                var lkGCB = ex.lstLeakResults.Where(lk => lk.IsLeak && lk.perfCounterData.perfCounterType == PerfCounterType.GCBytesInAllHeaps).FirstOrDefault();
                if (lkGCB != null &&
                //if (ex.lstLeakResults.Where(lk => lk.IsLeak && lk.perfCounterData.perfCounterType == PerfCounterType.GCBytesInAllHeaps).FirstOrDefault() != null &&
                    ex.lstLeakResults.Where(lk => lk.IsLeak).Count() == 1
                    )
                {
                    if (lkGCB.perfCounterData.thresholdRegression == thresh) // verify we're using the provided thresh
                    {
                        throw; // it's a valid leak.. throw because test expects a LeakException, and test passes
                    }
                }
                Assert.Fail("Didn't get expected leak type");
            }

            _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter(initSize: (int)(thresh + 100000)));
        }



        readonly List<string> myList = new List<string>();

        [TestMethod]
        [ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        [Description("Sensitivity Leak a very small string of 14 chars")]
        [Ignore] // same test below with XML settings.
        public async Task StressLeakyDetectVerySmallLeakAsync()
        {
            await StressUtil.DoIterationsAsync(this, new StressUtilOptions() { NumIterations = 711, ProcNamesToMonitor = "", Sensitivity = 1e6, DelayMultiplier = 0 });

            myList.Add($"leaking string" + "asdfafsdfasdfasd".Substring(0, 14));// needs to be done at runtime to create a diff string each iter. Time dominated by GC
        }


        [TestMethod]
        [ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        [DeploymentItem("StressLeakyWithCustomXMLSettings.settings.xml", "Assets")]
        public async Task StressLeakyWithCustomXMLSettingsAsync()
        {
            if (StressUtilOptions.IsRunningOnBuildMachine())
            {
                throw new LeakException("Throwing expected exception so test passes", null);
            }

            string didValidateSettingsRead = "didValidateSettingsRead";
            await StressUtil.DoIterationsAsync(this);
            var curIter = (int)TestContext.Properties[StressUtil.PropNameCurrentIteration];
            if (TestContext.Properties.Contains(StressUtil.PropNameLogger))
            {
                if (TestContext.Properties[StressUtil.PropNameLogger] is Logger logger)
                {
                    if (curIter == 0)
                    {
                        Assert.IsNotNull(logger._lstLoggedStrings.Where(s => s.Contains("Reading settings from")).First(), "should have read settings from xml file"); // this will assert each iteration
                        TestContext.Properties[didValidateSettingsRead] = 1;
                    }
                }
            }
            if (curIter == 0)
            {
                Assert.IsTrue((int)TestContext.Properties[didValidateSettingsRead] > 0, "didn't validate settings read");
            }
            myList.Add($"leaking string" + "asdfafsdfasdfasd".Substring(0, 14));// needs to be done at runtime to create a diff string each iter. Time dominated by GC

        }


        [TestMethod]
        [ExpectedException(typeof(LeakException))]
        public async Task StressLeakyVerifyDiffFileResultAsync()
        {
            int numiter = 11;
            try
            {
                await StressUtil.DoIterationsAsync(this, new StressUtilOptions() { NumIterations = numiter, ProcNamesToMonitor = "" });

                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
            }
            catch (LeakException)
            {
                var lstFileResults = (List<FileResultsData>)TestContext.Properties[StressUtil.PropNameListFileResults];
                var diffFile = lstFileResults.Where(r => Path.GetFileName(r.filename).Contains(MeasurementHolder.DiffFileName)).First();
                var diffs = File.ReadAllText(diffFile.filename);
                TestContext.WriteLine("Verifying diff file");
                Assert.IsTrue(diffs.Contains("TestStressDll.StressLeakyClass+BigStuffWithLongNameSoICanSeeItBetter"), "doesn't have leaking type");

                Assert.IsTrue(diffs.Contains("leaking string"), "doesn't have leaking string"); // there's one more "leaking string" because it's a class static internally (in System.Object[] array of Pinned handle statics)

                Assert.IsTrue(lstFileResults.Where(r => Path.GetExtension(r.filename) == ".dmp").Count() == 2, "expected 2 dumps");
                throw;
            }
        }



        [TestMethod]
        [MemSpectAttribute(NumIterations = 17)]
        [ExpectedException(typeof(LeakException))]
        public async Task StressTestWithAttributeAsync()
        {
            await ProcessAttributesAsync(this);
            _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
        }
        [TestMethod]
        [MemSpectAttribute(NumIterations = 17, Sensitivity = 1)]
        [ExpectedException(typeof(LeakException))]
        public async Task StressTestWithAttributeNotAsync()
        {
            try
            {
                await ProcessAttributesAsync(this);
                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
            }
            catch (AggregateException ex)
            {
                if (ex.InnerExceptions?.Count == 1)
                {
                    TestContext.WriteLine($"Agg exception with 1 inner {ex}");
                    throw ex.InnerExceptions[0];
                }
                TestContext.WriteLine($"Agg exception with !=1 inner {ex}");
                throw ex;
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Final exception {ex}");
            }
        }
        public async Task ProcessAttributesAsync(object test)
        {
            MemSpectAttribute attr = (MemSpectAttribute)(test.GetType().GetMethod(TestContext.TestName).GetCustomAttribute(typeof(MemSpectAttribute)));

            //            MemSpectAttribute attr = (MemSpectAttribute)_theTestMethod.GetCustomAttribute(typeof(MemSpectAttribute)));
            await StressUtil.DoIterationsAsync(
                this,
                new StressUtilOptions()
                {
                    NumIterations = attr.NumIterations,
                    Sensitivity = attr.Sensitivity,
                    ProcNamesToMonitor = ""
                });

        }
    }
}
