using Microsoft.Test.Stress;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class TestDumperViewer : BaseTestClass
    {
        [TestMethod]
        public void TestDumperInProc()
        {
            var pid = Process.GetProcessesByName("devenv")[0].Id;
            var pathDumpFile = Path.Combine(Environment.CurrentDirectory, "test dump.dmp");
            if (File.Exists(pathDumpFile))
            {
                File.Delete(pathDumpFile);
            }
            var args = new[] { 
                "-p", pid.ToString(),
                "-f",  "\"" + pathDumpFile + "\""
            };
            var odumper = new DumperViewerMain(args)
            {
                _logger = this
            };
            odumper.DoMain();

            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("in DumperViewer")).FirstOrDefault());

            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("Done creating dump")).FirstOrDefault());
        }

        [TestMethod]
        public async Task TestDumperMain()
        {
            var pid = Process.GetCurrentProcess().Id;
            var pathDumpFile = Path.Combine(Environment.CurrentDirectory, "test dump.dmp");
            if (File.Exists(pathDumpFile))
            {
                File.Delete(pathDumpFile);
            }
            var args = new[] {
                "-p", pid.ToString(),
                "-f",  "\"" + pathDumpFile + "\"",
                "-c"
            };
            //            DumperViewerMain.Main(args);
            var x = new DumperViewerMain(args);
            await x.DoitAsync();

        }

        [TestMethod]
        public void TestDumperAnalyzeDump()
        {
            var pathDumpFileBaseline = @"C:\StressNoInheritance_7_0.dmp";
            var pathDumpFileCurrent = @"C:\StressNoInheritance_11_0.dmp";
            int TotNumIterations = 11;
            int NumIterationsBeforeTotalToTakeBaselineSnapshot = 3;
            var oAnalyzer = new DumpAnalyzer(this);

            oAnalyzer.GetDiff(pathDumpFileBaseline, pathDumpFileCurrent, TotNumIterations, NumIterationsBeforeTotalToTakeBaselineSnapshot);

        }




    }
}
