using DumperViewer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class TestDumperViewer : BaseTestClass
    {
        [TestMethod]
        public void TestDumperViewerArgs()
        {
            var pid = Process.GetProcessesByName("devenv")[0].Id;
            pid = Process.GetCurrentProcess().Id;
            var args = new[] { "-p", pid.ToString() };
            var odumper = new DumperViewer.DumperViewer(args)
            {
                _logger = this
            };
            odumper.DoMain();


        }
    }
}
