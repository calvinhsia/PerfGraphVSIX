using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PerfGraphVSIX;

namespace Tests
{
    [TestClass]
    public class TestMeasures : BaseTestClass
    {
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
            Assert.Fail($"RmsErr={rmsError} m={m} b={b}");
        }

        [TestMethod]
        public void TestPCMeasurementHolder1k()
        {
            // too small to trigger threshold
            var res = DoStressSimulation(nIter: 100, nArraySize: 1024, RatioThresholdSensitivity: 1f);
            Assert.IsFalse(res, $"Expected no Regression");
        }

        [TestMethod]
        public void TestPCMeasurementHolder500k()
        {
            // too small to trigger threshold, but close to boundary
            var res = DoStressSimulation(nIter: 100, nArraySize: 1024 * 500, RatioThresholdSensitivity: 1f);
            Assert.IsFalse(res, $"Expected no Regression");
        }
        [TestMethod]
        public void TestPCMeasurementHolder500kSensitive()
        {
            // too small to trigger threshold, but close to boundary, so making more sensitive triggers regression
            var res = DoStressSimulation(nIter: 100, nArraySize: 1024 * 500, RatioThresholdSensitivity: .6f);
            Assert.IsTrue(res, $"Expected Regression");
        }

        [TestMethod]
        public void TestPCMeasurementHolder2Meg()
        {
            // Big triggers regression
            var res = DoStressSimulation(nIter: 100, nArraySize: 1024 * 1024 * 2, RatioThresholdSensitivity: 1f);
            Assert.IsTrue(res, $"Expected Regression");
        }

        private bool DoStressSimulation(int nIter, int nArraySize, float RatioThresholdSensitivity)
        {
            var lstPCs = new List<PerfCounterData>(PerfCounterData._lstPerfCounterDefinitionsForStressTest);
            foreach (var ctr in lstPCs)
            {
                ctr.IsEnabledForMeasurement = true;
                ctr.RatioThresholdSensitivity = RatioThresholdSensitivity;
            }
            var measurementHolder = new MeasurementHolder(nameof(DoStressSimulation), lstPCs, this);
            var lstBigStuff = new List<byte[]>();
            LogMessage($"nIter={nIter:n0} ArraySize= {nArraySize:n0}");
            for (int i = 0; i < nIter; i++)
            {

                lstBigStuff.Add(new byte[nArraySize]);
                //                lstBigStuff.Add(new int[10000000]);
                var res = measurementHolder.TakeMeasurement($"iter {i}/{nIter}", SampleType.SampleTypeIteration);
                //LogMessage(res);
            }
            var filename = measurementHolder.DumpOutMeasurementsToTempFile(StartExcel: false);
            LogMessage($"Results file name = {filename}");

            return measurementHolder.CalculateRegression();
        }
    }
}
