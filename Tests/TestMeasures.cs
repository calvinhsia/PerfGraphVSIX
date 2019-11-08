using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PerfGraphVSIX;
using System.Windows.Forms;
using System.IO;

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
            Assert.IsFalse(res, $"Expected no Regression");
        }
        [TestMethod]
        public async Task TestPCMeasurementHolder500kSensitive()
        {
            // too small to trigger threshold, but close to boundary, so making more sensitive triggers regression
            var res = await DoStressSimulation(nIter: 100, nArraySize: 1024 * 500, RatioThresholdSensitivity: .6f);
            Assert.IsTrue(res, $"Expected Regression");
        }

        [TestMethod]
        public async Task TestPCMeasurementHolder2Meg()
        {
            // Big triggers regression
            var res = await DoStressSimulation(nIter: 100, nArraySize: 1024 * 1024 * 2, RatioThresholdSensitivity: 1f);
            Assert.IsTrue(res, $"Expected Regression");
        }

        private async Task<bool> DoStressSimulation(int nIter, int nArraySize, float RatioThresholdSensitivity)
        {
            var lstPCs = new List<PerfCounterData>(PerfCounterData._lstPerfCounterDefinitionsForStressTest);
            foreach (var ctr in lstPCs)
            {
                ctr.IsEnabledForMeasurement = true;
                ctr.RatioThresholdSensitivity = RatioThresholdSensitivity;
            }
            var measurementHolder = new MeasurementHolder(nameof(DoStressSimulation), lstPCs, SampleType.SampleTypeIteration, this);

            var lstBigStuff = new List<byte[]>();
            LogMessage($"nIter={nIter:n0} ArraySize= {nArraySize:n0}");
            for (int i = 0; i < nIter; i++)
            {

                lstBigStuff.Add(new byte[nArraySize]);
                //                lstBigStuff.Add(new int[10000000]);
                var res = measurementHolder.TakeMeasurement($"iter {i}/{nIter}");
                LogMessage(res);
            }
            var filename = measurementHolder.DumpOutMeasurementsToTempFile(StartExcel: false);
            LogMessage($"Results file name = {filename}");

            return await measurementHolder.CalculateRegressionAsync(showGraph:true);
        }
    }
}
