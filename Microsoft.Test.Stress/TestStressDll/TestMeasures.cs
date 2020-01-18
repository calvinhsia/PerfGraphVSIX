using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Test.Stress;
using HANDLE = System.IntPtr;
using System.Diagnostics;

namespace TestStressDll
{
    public class BaseTestClass : ILogger
    {
        public TestContext TestContext { get; set; }

        public List<string> _lstLoggedStrings;

        [TestInitialize]
        public void TestInitialize()
        {
            _lstLoggedStrings = new List<string>();
            LogMessage($"Starting test {TestContext.TestName}");

        }
        public void LogMessage(string str, params object[] args)
        {
            var dt = string.Format("[{0}],",
                DateTime.Now.ToString("hh:mm:ss:fff")
                ) + $"{Thread.CurrentThread.ManagedThreadId,2} ";
            str = string.Format(dt + str, args);
            var msgstr = $" {str}";

            this.TestContext.WriteLine(msgstr);
            if (Debugger.IsAttached)
            {
                Debug.WriteLine(msgstr);
            }
            _lstLoggedStrings.Add(msgstr);
        }
    }


    [TestClass]
    public class TestMeasures : BaseTestClass
    {

        [TestMethod]
        public async Task TestNumSamplesRecommended()
        {
            if (StressUtilOptions.IsRunningOnBuildMachine())
            {
                return;
            }
            await Task.Yield();
            // data from https://dev.azure.com/devdiv/DevDiv/_releaseProgress?_a=release-environment-extension&releaseId=548609&environmentId=2872682&extensionId=ms.vss-test-web.test-result-in-release-environment-editor-tab&runId=10533790&resultId=100000&paneView=attachments
            var testData = new uint[]
            {
14520408,
15279504,
15409536,
15929568,
16189600,
16449632,
16709664,
16969696,
17229728,
17749760,
18009792,
18269824,
18529856,
18789888,
19049920,
19569952,
19829984,
20090016,
20610048,
15279504,
15279504,
14293824,
14553856,
15073888,
15370280,
15370280,
14070152,
15370248,
15370248,
15500280,
15760312,
14070152,
14070152,
14850216,
15370248,
15630280,
15630280,
16150312,
16410344,
16930376,
16930376,
17190408,
17710440,
14070152,
14070152,
14850216,
15370248,
15370248,
16150312,
16410344,
16930376,
16930376,
14460216,
14980248,
14070152,
14070152,
14330184,
14850216,
14850216,
15110248,
15630280,
16410344,
16410344,
16670376,
17221948,
17221948,
17513332,
18033364,
18293396,
18293396,
18813428,
19073460,
19593492,
19593492,
14133044,
14913108,
15433140,
15433140,
15953172,
14133044,
14133044,
14653076,
14913108,
14133044,
14133044,
14913108,
14133044,
14133044,
14913108,
14133044,
14913108,
14913108,
14133044,
14653076,
15043140,
15043140,
15563172,
15823204,
14133044,
14133044,
14653076,
14913108,
15433140,
15433140,
15693172,
16213204,
14133044,
14133044,
14393076,
14913108,
14913108,
15693172,
15953204,
14133044,
14133044,
14783108,
15043140,
15563172,
15831180,
16351212,
16611244,
16611244,
14401052,
15208252,
15208252,
15533496,
13973368,
15273464,
15294420,
15814452,
13994324,
13994324,
15424452,
13931844,
13970924,
15271020,
15684904,
15684904,
16293124,
13952964,
13952964,
15293268,
15813300,
15825808,
16345840,
16865872,
16865872,
14915776,
15435808,
15435808,
14005680,
15305776,
15305776,
14005680,
15305776,
15305776,
14005680,
15305776,
15305776,
15825808,
16345840,
16345840,
16865872,
17385904,
17385904,
17905936,
18425968,
18425968,
18946000,
19466032,
19466032,
19986064,
20506096,
20506096,
21026128,
21546160,
21546160,
22066192,
22586224,
22586224,
23106256,
14915776,
14915776,
15435808,
14005680,
14005680,
15305776,
15825808,
15825808,
16345840,
14005680,
14005680,
15305776,
15825808,
15825808,
16345840,
16865872,
16865872,
15207648,
15727680,
15727680,
16247712,
16767744,
16767744,
17287776,
17807808,
17807808,
18327840,
18847872,
18847872,
19367904,
19887936,
19887936,
20407968,
20928000,
20928000,
21448032,
21968064,
21968064,
            };

            using (var measurementHolder = new MeasurementHolder(
                new TestContextWrapper(TestContext),
                new StressUtilOptions()
                {
                    NumIterations = testData.Length,
                    logger = this,
                    pctOutliersToIgnore = 5,
                    lstPerfCountersToUse = PerfCounterData.GetPerfCountersToUse(Process.GetCurrentProcess(),
                    IsForStress: true).Where(p => p.perfCounterType == PerfCounterType.GCBytesInAllHeaps).ToList(),
                    PerfCounterOverrideSettings = new List<PerfCounterOverrideThreshold>()
                    {
                        new PerfCounterOverrideThreshold() { perfCounterType = PerfCounterType.GCBytesInAllHeaps, regressionThreshold = 3000}
                    }
                },
                SampleType.SampleTypeIteration))
            {
                for (int iter = 0; iter < testData.Length; iter++)
                {
                    foreach (var ctr in measurementHolder.LstPerfCounterData)
                    {
                        measurementHolder.measurements[ctr.perfCounterType].Add(testData[iter]);
                    }
                }
                LogMessage($"# Iter = {measurementHolder.measurements[PerfCounterType.GCBytesInAllHeaps].Count}");
                var leakAnalysisResults = await measurementHolder.CalculateLeaksAsync(showGraph: false);
                await measurementHolder.CalculateMinimumNumberOfIterationsAsync(leakAnalysisResults);
            }

        }

        [TestMethod]
        public async Task TestOutliers()
        {
            if (StressUtilOptions.IsRunningOnBuildMachine())
            {
                return;
            }
            await Task.Yield();
            // data from https://dev.azure.com/devdiv/DevDiv/_releaseProgress?_a=release-environment-extension&releaseId=548609&environmentId=2872682&extensionId=ms.vss-test-web.test-result-in-release-environment-editor-tab&runId=10533790&resultId=100000&paneView=attachments
            var testData = new uint[]
            {
1867008,
2713172,
2701928,
2701928,
2701800,
2701800,
2701700,
2701700,
2711368,
2711368,
2713228,
2713228,
2714876,
2714876,
2716588,
2716588,
2716588,
2732980,
2732980,
2734848,
2734848,
2736536,
2736536,
2738052,
2738052,
2739908,
2739908,
2741400,
2741400,
2742916,
2742916,
2744548,
2744548,
2744548,
2747340,
2747340,
2764696,
2764696,
2766384,
2766384,
2767888,
2767888,
2769756,
2769756,
2771260,
2771260,
2772764,
2772764,
2774268,
2774268,
2774268,
2776140,
2776140,
2777468,
2777468,
2779168,
2779168,
2779836,
2779836,
2797744,
2797744,
2799236,
2799236,
2800996,
2800996,
2803548,
2803548,
2899440,
2904492,
2904492,
2904492,
            };

            var resultsFolder = string.Empty;
            using (var measurementHolder = new MeasurementHolder(
                new TestContextWrapper(TestContext),
                new StressUtilOptions()
                {
                    NumIterations = -1,
                    logger = this,
                    pctOutliersToIgnore = 5,
                    lstPerfCountersToUse = PerfCounterData.GetPerfCountersToUse(Process.GetCurrentProcess(),
                    IsForStress: true).Where(p => p.perfCounterType == PerfCounterType.GCBytesInAllHeaps).ToList()
                },
                SampleType.SampleTypeIteration))
            {
                resultsFolder = measurementHolder.ResultsFolder;
                for (int iter = 0; iter < testData.Length; iter++)
                {
                    foreach (var ctr in measurementHolder.LstPerfCounterData)
                    {
                        measurementHolder.measurements[ctr.perfCounterType].Add(testData[iter]);
                    }
                }
                var leakAnalysisResults = await measurementHolder.CalculateLeaksAsync(showGraph: false);
                foreach (var res in leakAnalysisResults[0].lstData)
                {
                    LogMessage($"{res}");
                }
                Assert.IsTrue(leakAnalysisResults[0].lstData[0].IsOutlier);
                Assert.IsTrue(leakAnalysisResults[0].lstData[1].IsOutlier);
                Assert.IsTrue(leakAnalysisResults[0].lstData[2].IsOutlier);
                Assert.IsFalse(leakAnalysisResults[0].lstData[3].IsOutlier);
            }
        }

        [TestMethod]
        public void TestXmlSerializeOptions()
        {
            var thresh = 1e6f;
            var stressUtilOptions = new StressUtilOptions()
            {
                PerfCounterOverrideSettings = new List<PerfCounterOverrideThreshold>
                    {
                        new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.GCBytesInAllHeaps, regressionThreshold = thresh } ,
                        new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.ProcessorPrivateBytes, regressionThreshold = 9 * thresh } , // use a very high thresh so this counter won't show as leak
                        new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.ProcessorVirtualBytes, regressionThreshold = 9 * thresh } ,
                        new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.KernelHandleCount, regressionThreshold = 9 * thresh } ,
                    },
                NumIterations = 7,
                ShowUI = false
            };

            var filename = Path.Combine(TestContext.DeploymentDirectory, "opts.xml");
            stressUtilOptions.WriteOptionsToFile(filename);
            LogMessage($"Output to {filename}");

            LogMessage(File.ReadAllText(filename));

            var newopts = new StressUtilOptions() { NumIterations = 321 };

            newopts.ReadOptionsFromFile(filename);
            Assert.AreEqual(stressUtilOptions.NumIterations, 7);
        }


        [TestMethod]
        [Ignore]
        public async Task TestMeasureRegressionVerifyGraph()
        {
            await Task.Yield();
            var resultsFolder = string.Empty;
            using (var measurementHolder = new MeasurementHolder(
                new TestContextWrapper(TestContext),
                new StressUtilOptions() { NumIterations = -1, logger = this, lstPerfCountersToUse = PerfCounterData.GetPerfCountersToUse(Process.GetCurrentProcess(), IsForStress: true).Where(p => p.perfCounterType == PerfCounterType.KernelHandleCount).ToList() },
                SampleType.SampleTypeIteration))
            {
                resultsFolder = measurementHolder.ResultsFolder;
                for (int iter = 0; iter < 10; iter++)
                {
                    foreach (var ctr in measurementHolder.LstPerfCounterData)
                    {
                        var val = 10000 + (uint)iter;
                        if (iter == 5 && ctr.perfCounterType == PerfCounterType.KernelHandleCount)
                        {
                            val += 1;
                        }
                        measurementHolder.measurements[ctr.perfCounterType].Add(val);
                    }
                }
                var res = await measurementHolder.CalculateLeaksAsync(showGraph: true);
            }
            var strHtml = @"
<a href=""file:\\C:\Users\calvinh\Source\repos\PerfGraphVSIX\TestResults\Deploy_calvinh 2019-11-19 11_00_13/Out/TestMeasureRegressionVerifyGraph/Graph Handle Count.png"">gr </a>
            ";
            var fileHtml = Path.Combine(resultsFolder, "IndexTest1.html");
            File.WriteAllText(fileHtml, strHtml);
            TestContext.AddResultFile(fileHtml);
            Assert.Fail("failing test so results aren't deleted");
        }

        [TestMethod]
        public async Task TestPCMeasurementHolder1k()
        {
            // too small to trigger threshold
            var res = await DoStressSimulation(nIter: 100, nArraySize: 1024, RatioThresholdSensitivity: 1f);
            Assert.IsFalse(res, $"Expected no Regression");
        }

        [TestMethod]
        public async Task TestPCMeasurementHolder500k()
        {
            // too small to trigger threshold, but close to boundary
            var res = await DoStressSimulation(nIter: 100, nArraySize: 1024 * 500, RatioThresholdSensitivity: 1f);
            Assert.IsFalse(res, $"Expected no Regression because lower than threshold");
        }
        [TestMethod]
        public async Task TestPCMeasurementHolder500kSensitive()
        {

            var eatmem = new byte[1024 * 1024 * 8];

            // too small to trigger threshold, but close to boundary, so making more sensitive triggers regression
            var res = await DoStressSimulation(nIter: 100, nArraySize: 1024 * 500, RatioThresholdSensitivity: 5f);
            Assert.IsTrue(res, $"Expected Regression because more sensitive");
        }

        [TestMethod]
        public async Task TestPCMeasurementHolder2Meg()
        {
            // Big triggers regression
            var res = await DoStressSimulation(nIter: 100, nArraySize: 1024 * 1024 * 2, RatioThresholdSensitivity: 1f);
            Assert.IsTrue(res, $"Expected Regression");
        }


        [TestMethod]
        public async Task TestPCMeasurementHolderLeakHandle()
        {
            //            var cts = new CancellationTokenSource();
            var lstHandles = new List<HANDLE>();
            var res = await DoStressSimulation(nIter: 10, nArraySize: 0, RatioThresholdSensitivity: 1, action: () =>
            {
                // see https://devdiv.visualstudio.com/DevDiv/_wiki/wikis/DevDiv.wiki/3803/CancellationToken-and-CancellationTokenSource-Leaks
                for (int i = 0; i < 1; i++)
                {
                    //var mre = new ManualResetEvent(initialState: false);// leaks mem and handles. Not a CTS leak (used internally by CTS)
                    var myevent = CreateEvent(IntPtr.Zero, false, false, $"aa{i}"); // leaks kernel handles, this is used internally in CTS
                    lstHandles.Add(myevent);
                    //CloseHandle(myevent); // must close else leaks kernel handles


                    //var newcts = new CancellationTokenSource();
                    //var handle = newcts.Token.WaitHandle; // this internally lazily instantiates a ManualResetEvent
                    //newcts.Dispose(); // must dispose, else leaks mem and handles. CTS Leak Type No. 4

                    //var tk = cts.Token;
                    //var cancellationTokenRegistration = tk.Register(() =>
                    //{

                    //});
                    //cancellationTokenRegistration.Dispose(); // must dispose else leaks. CTS Leak Type No. 2
                }
            });
            foreach (HANDLE h in lstHandles)
            {
                CloseHandle(h);
            }
            Assert.IsTrue(res, $"Expected Regression");
        }


        /// <summary>
        /// return true if regression found
        /// These tests will be affected by other tests running in the same instance of testhost because they share the same memory
        /// </summary>
        private async Task<bool> DoStressSimulation(int nIter, int nArraySize, float RatioThresholdSensitivity, Action action = null)
        {
            var lstPCs = PerfCounterData.GetPerfCountersToUse(Process.GetCurrentProcess(), IsForStress: true);
            foreach (var ctr in lstPCs)
            {
                ctr.IsEnabledForMeasurement = true;
            }
            List<LeakAnalysisResult> lstRegResults;
            using (var measurementHolder = new MeasurementHolder(
                new TestContextWrapper(TestContext),
                new StressUtilOptions() { NumIterations = -1, Sensitivity = RatioThresholdSensitivity, logger = this, lstPerfCountersToUse = lstPCs },
                SampleType.SampleTypeIteration))
            {
                var lstBigStuff = new List<byte[]>();
                LogMessage($"nIter={nIter:n0} ArraySize= {nArraySize:n0}");
                for (int i = 0; i < nIter; i++)
                {
                    if (action != null)
                    {
                        action();
                    }
                    else
                    {
                        lstBigStuff.Add(new byte[nArraySize]);
                    }
                    //                lstBigStuff.Add(new int[10000000]);
                    var res = await measurementHolder.TakeMeasurementAsync($"iter {i}/{nIter}");
                    LogMessage(res);
                }
                var filename = measurementHolder.DumpOutMeasurementsToTxtFile();
                LogMessage($"Results file name = {filename}");
                lstRegResults = (await measurementHolder.CalculateLeaksAsync(showGraph: false)).Where(r => r.IsLeak).ToList();
            }
            return lstRegResults.Count > 0;
        }

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Auto)]
        public static extern HANDLE CreateEvent(HANDLE lpEventAttributes, [In, MarshalAs(UnmanagedType.Bool)] bool bManualReset, [In, MarshalAs(UnmanagedType.Bool)] bool bIntialState, [In, MarshalAs(UnmanagedType.BStr)] string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hHandle);

    }
}
