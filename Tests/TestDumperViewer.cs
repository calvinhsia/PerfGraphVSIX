using DumperViewer;
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
            //pid = Process.GetCurrentProcess().Id;
            var pathDumpFile = Path.Combine(Environment.CurrentDirectory, "test dump.dmp");
            if (File.Exists(pathDumpFile))
            {
                File.Delete(pathDumpFile);
            }
            var args = new[] { 
                "-p", pid.ToString(),
                "-f",  "\"" + pathDumpFile + "\""
            };
            var odumper = new DumperViewer.DumperViewer(args)
            {
                _logger = this
            };
            odumper.DoMain();

            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("in DumperViewer")).FirstOrDefault());

            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("Done creating dump")).FirstOrDefault());
        }

        [TestMethod]
        public void TestDumperOutOfProc()
        {
            // todo

        }

        [TestMethod]
        public void TestDumperGetData()
        {
            var pid = Process.GetProcessesByName("devenv")[0].Id;
            pid = Process.GetCurrentProcess().Id;
            var pathDumpFile = Path.Combine(Environment.CurrentDirectory, "test dump.dmp");
            if (File.Exists(pathDumpFile))
            {
                File.Delete(pathDumpFile);
            }
            var args = new[] {
                "-p", pid.ToString(),
                "-f",  "\"" + pathDumpFile + "\""
            };
            var odumper = new DumperViewer.DumperViewer(args)
            {
                _logger = this
            };
            odumper.DoMain();

            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("in DumperViewer")).FirstOrDefault());

            Assert.IsNotNull(_lstLoggedStrings.Where(s => s.Contains("Done creating dump")).FirstOrDefault());
        }


    }
}
