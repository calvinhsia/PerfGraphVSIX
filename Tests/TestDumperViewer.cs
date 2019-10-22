using DumperViewer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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
            var args = new[] {"" };
            var od = new DumperViewer.DumperViewer(args);

        }
    }
}
