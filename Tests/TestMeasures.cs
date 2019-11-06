using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PerfGraphVSIX;
using static PerfGraphVSIX.PerfCounterData;

namespace Tests
{
    [TestClass]
    public class TestMeasures: BaseTestClass
    {
        [TestMethod]
        public void TestPCMeasures()
        {
            int nIter = 100;
            var lstData = new List<PointF>();
            for (int i = 0; i < nIter; i++)
            {
                if (i/2*2 != i)
                {
                    LogMessage($"ssss {i}");
                    lstData.Add(new PointF() { X = i, Y = 2.5 * i });
                }
                else
                {
                    lstData.Add(new PointF() { X = i, Y = 2 * i });
                }
            }
            var res = FindLinearLeastSquaresFit(lstData, out var m, out var b);
            Assert.Fail($"Err={res} m={m} b={b}");
        }
    }
}
