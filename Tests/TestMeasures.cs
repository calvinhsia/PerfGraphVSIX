using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.StressTest;
using HANDLE = System.IntPtr;

namespace Tests
{
    [TestClass]
    public class TestMeasures : BaseTestClass
    {
        [TestMethod]
        [Ignore]
        public void TestMeasureStartExcel()
        {

            var filenamecsv = @"C:\t.csv";
            var text = File.ReadAllText(filenamecsv);
            try
            {
                DataObject dataObject = new DataObject();
                dataObject.SetText(text);
                Clipboard.SetDataObject(dataObject, false);
            }
            catch (Exception) { }


            var typeExcel = Type.GetTypeFromProgID("Excel.Application");
            dynamic oExcel = Activator.CreateInstance(typeExcel); // need to add ref to Microsoft.CSharp
            oExcel.Visible = true;
            dynamic workbook = oExcel.Workbooks.Add();
            workbook.ActiveSheet.Paste();
            // xlSrcRange=1
            //            workbook.ActiveSheet.ListObjects.Add(1,Range)

        }


        [TestMethod]
//        [Ignore]
        public async Task TestMeasureRegressionVerifyGraph()
        {
            await Task.Yield();
            using (var x = new MeasurementHolder(
                TestContext,
                PerfCounterData._lstPerfCounterDefinitionsForStressTest.Where(p => p.perfCounterType == PerfCounterType.KernelHandleCount).ToList(),
                SampleType.SampleTypeIteration, this))
            {
                for (int iter = 0; iter < 10; iter++)
                {
                    foreach (var ctr in x.lstPerfCounterData)
                    {
                        var val = 10000 + (uint)iter;
                        if (iter == 5 && ctr.perfCounterType == PerfCounterType.KernelHandleCount)
                        {
                            val += 1;
                        }
                        x.measurements[ctr.perfCounterType].Add(val);
                    }
                }
                var res = await x.CalculateLeaksAsync(showGraph: true);
            }
        }

        [TestMethod]
        public void TestPCMeasures()
        {
            int nIter = 100;
            var lstData = new List<PointF>();
            for (int i = 0; i < nIter; i++)
            {
                if (i == 5)
                //                    if (i / 2 * 2 != i)
                {
                    lstData.Add(new PointF() { X = i, Y = 2.01 * i });
                }
                else
                {
                    lstData.Add(new PointF() { X = i, Y = 2 * i });
                }
            }
            var rmsError = MeasurementHolder.FindLinearLeastSquaresFit(lstData, out var m, out var b);
            var pctRms = (int)(100 * rmsError / m);
            LogMessage($"RmsErr={rmsError,16:n3} RmsPctErr={pctRms,4} m={m,18:n3} b={b,18:n3}");
            //Assert.Fail($"RmsErr={rmsError} m={m} b={b}");
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
            var res = await DoStressSimulation(nIter: 100, nArraySize: 1024 * 500, RatioThresholdSensitivity: .4f);
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
            var lstPCs = new List<PerfCounterData>(PerfCounterData._lstPerfCounterDefinitionsForStressTest);
            foreach (var ctr in lstPCs)
            {
                ctr.IsEnabledForMeasurement = true;
            }
            List<LeakAnalysisResult> lstRegResults;
            using (var measurementHolder = new MeasurementHolder(TestContext, lstPCs, SampleType.SampleTypeIteration, this, sensitivity: RatioThresholdSensitivity))
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
                    var res = measurementHolder.TakeMeasurement($"iter {i}/{nIter}");
                    LogMessage(res);
                }
                var filename = measurementHolder.DumpOutMeasurementsToCsv();
                LogMessage($"Results file name = {filename}");
                lstRegResults = (await measurementHolder.CalculateLeaksAsync(showGraph: true)).Where(r => r.IsLeak).ToList();
            }
            return lstRegResults.Count > 0;
        }

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Auto)]
        public static extern HANDLE CreateEvent(HANDLE lpEventAttributes, [In, MarshalAs(UnmanagedType.Bool)] bool bManualReset, [In, MarshalAs(UnmanagedType.Bool)] bool bIntialState, [In, MarshalAs(UnmanagedType.BStr)] string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hHandle);

    }
}
