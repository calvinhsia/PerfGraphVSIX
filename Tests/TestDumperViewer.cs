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
            var args = new[] { "-p", pid.ToString() };
            var od = new DumperViewer.DumperViewer(args);
            od._logger = this;


        }
    }
}
