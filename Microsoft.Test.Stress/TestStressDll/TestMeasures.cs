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
using System.IO.Compression;

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
    [Ignore]
    public class TestMeasures : BaseTestClass
    {
        [TestMethod]
        //        [Ignore]
        public void TestZip()
        {
            var destFolder = Path.Combine(Environment.CurrentDirectory, "outputTest");
            var zipFile = @"C:\Users\calvinh\source\repos\Stress\Microsoft.Test.Stress\Microsoft.Test.Stress\Resources\ClrObjExplorer.zip";
            //            var ClrObjExplorerExe = Path.Combine(clrObjDir, "ClrObjExplorer.exe");
            LogMessage($"Creating {destFolder}");
            Directory.CreateDirectory(destFolder); // succeeds even if dir exists

            //                        logger.LogMessage($"Extracting zip {tempZipFile}");
            using (var archive = ZipFile.Open(zipFile, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    string destfilename = destFolder;
                    string restName = entry.FullName;
                    while (true)
                    {
                        var ndx = restName.IndexOf('/'); // subdir separator == '/'
                        if (ndx < 0)
                        {
                            destfilename = Path.Combine(destfilename, entry.Name);
                            break;
                        }
                        var subfolder = restName.Substring(0, ndx);
                        restName = restName.Substring(ndx + 1);
                        Directory.CreateDirectory(Path.Combine(destfilename, subfolder));
                        destfilename = Path.Combine(destfilename, subfolder);
                    }
                    if (!File.Exists(destfilename) || new FileInfo(destfilename).LastWriteTime != entry.LastWriteTime)
                    {
                        entry.ExtractToFile(destfilename, overwrite: true);
                    }
                }
            }
            //                ZipFile.ExtractToDirectory(tempZipFile, clrObjDir);
            LogMessage($"Done Extracting zip {zipFile}");
        }

        [TestMethod]
        public async Task TestOutliersAsync()
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
                var leakAnalysisResults = await measurementHolder.CalculateLeaksAsync(showGraph: false, GraphsAsFilePrefix: "Graph");
                LogMessage($"Yint = {leakAnalysisResults[0].yintercept:n0}");
                foreach (var res in leakAnalysisResults[0].lstData)
                {
                    LogMessage($"{res} dist = {res.distance} {res}");
                }
                Assert.IsTrue(leakAnalysisResults[0].lstData[0].IsOutlier);
                Assert.IsTrue(leakAnalysisResults[0].lstData[1].IsOutlier);
                Assert.IsTrue(!leakAnalysisResults[0].lstData[2].IsOutlier);
                Assert.IsTrue(leakAnalysisResults[0].lstData[68].IsOutlier);
            }
        }

        [TestMethod]
        public async Task TestXmlSerializeOptionsAsync()
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
            var didlambda = false;
            var newopts = new StressUtilOptions()
            {
                NumIterations = 321,
                actExecuteAfterEveryIterationAsync = async (nIter, measurementHolder) => // test that lambda doesn't get overridden
                    {
                        await Task.Yield();
                        didlambda = true;
                        return false;
                    }
            };

            newopts.ReadOptionsFromFile(filename);
            Assert.AreEqual(stressUtilOptions.NumIterations, 7);

            await newopts.actExecuteAfterEveryIterationAsync(0, null);
            Assert.IsTrue(didlambda);

        }


        [TestMethod]
        [Ignore]
        public async Task TestMeasureRegressionVerifyGraphAsync()
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
                var res = await measurementHolder.CalculateLeaksAsync(showGraph: true, GraphsAsFilePrefix: "Graph");
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
        public async Task TestPCMeasurementHolder1kAsync()
        {
            // too small to trigger threshold
            var res = await DoStressSimulationAsync(nIter: 100, nArraySize: 1024, RatioThresholdSensitivity: 1f);
            Assert.IsFalse(res, $"Expected no Regression");
        }

        [TestMethod]
        public async Task TestPCMeasurementHolder500kAsync()
        {
            // too small to trigger threshold, but close to boundary
            var res = await DoStressSimulationAsync(nIter: 100, nArraySize: 1024 * 500, RatioThresholdSensitivity: 1f);
            Assert.IsFalse(res, $"Expected no Regression because lower than threshold");
        }
        [TestMethod]
        public async Task TestPCMeasurementHolder500kSensitiveAsync()
        {

            var eatmem = new byte[1024 * 1024 * 8];

            // too small to trigger threshold, but close to boundary, so making more sensitive triggers regression
            var res = await DoStressSimulationAsync(nIter: 100, nArraySize: 1024 * 500, RatioThresholdSensitivity: 5f);
            Assert.IsTrue(res, $"Expected Regression because more sensitive");
        }

        [TestMethod]
        public async Task TestPCMeasurementHolder2MegAsync()
        {
            // Big triggers regression
            var res = await DoStressSimulationAsync(nIter: 100, nArraySize: 1024 * 1024 * 2, RatioThresholdSensitivity: 1f);
            Assert.IsTrue(res, $"Expected Regression");
        }


        [TestMethod]
        public async Task TestPCMeasurementHolderLeakHandleAsync()
        {
            //            var cts = new CancellationTokenSource();
            var lstHandles = new List<HANDLE>();
            var res = await DoStressSimulationAsync(nIter: 10, nArraySize: 0, RatioThresholdSensitivity: 1, action: () =>
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
        private async Task<bool> DoStressSimulationAsync(int nIter, int nArraySize, float RatioThresholdSensitivity, Action action = null)
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
                    var res = await measurementHolder.TakeMeasurementAsync($"iter {i}/{nIter}", DoForceGC: true);
                    LogMessage(res);
                }
                var filename = measurementHolder.DumpOutMeasurementsToTxtFile();
                LogMessage($"Results file name = {filename}");
                lstRegResults = (await measurementHolder.CalculateLeaksAsync(showGraph: false, GraphsAsFilePrefix: "Graph")).Where(r => r.IsLeak).ToList();
            }
            return lstRegResults.Count > 0;
        }

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Auto)]
        public static extern HANDLE CreateEvent(HANDLE lpEventAttributes, [In, MarshalAs(UnmanagedType.Bool)] bool bManualReset, [In, MarshalAs(UnmanagedType.Bool)] bool bIntialState, [In, MarshalAs(UnmanagedType.BStr)] string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hHandle);

    }
}
