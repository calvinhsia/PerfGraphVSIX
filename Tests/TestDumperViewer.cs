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
        public void TestGetVSPath()
        {
            var vsHandler = StressUtil.CreateVSHandler(null);

            Assert.IsNotNull(vsHandler.GetVSFullPath());
        }

        [TestMethod]
        [Ignore]
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
        [Ignore]
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
    }
}
