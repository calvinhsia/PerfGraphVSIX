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

namespace Tests
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
        [Ignore]
        public async Task TestMeasureRegressionVerifyGraph()
        {
            await Task.Yield();
            var resultsFolder = string.Empty;
            using (var measurementHolder = new MeasurementHolder(
                new TestContextWrapper(TestContext),
                PerfCounterData.GetPerfCountersForStress().Where(p => p.perfCounterType == PerfCounterType.KernelHandleCount).ToList(),
                SampleType.SampleTypeIteration,
                NumTotalIterations: -1,
                logger: this))
            {
                resultsFolder = measurementHolder.ResultsFolder;
                for (int iter = 0; iter < 10; iter++)
                {
                    foreach (var ctr in measurementHolder.lstPerfCounterData)
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
            var res = await DoStressSimulation(nIter: 100, nArraySize: 1024 * 500, RatioThresholdSensitivity: 2.5f);
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
            var res = await DoStressSimulation(nIter: 10, nArraySize: 0, RatioThresholdSensitivity: 1, () =>
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
            var lstPCs = PerfCounterData.GetPerfCountersForStress();
            foreach (var ctr in lstPCs)
            {
                ctr.IsEnabledForMeasurement = true;
            }
            List<LeakAnalysisResult> lstRegResults;
            using (var measurementHolder = new MeasurementHolder(
                new TestContextWrapper(TestContext),
                lstPCs,
                SampleType.SampleTypeIteration,
                this,
                NumTotalIterations: -1,
                sensitivity: RatioThresholdSensitivity))
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
                var filename = measurementHolder.DumpOutMeasurementsToCsv();
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
