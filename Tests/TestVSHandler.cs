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
    public class TestVSHandler : BaseTestClass
    {
        [TestMethod]
        public void TestVSHandlerSettingsForLeakDetection()
        {
            var vsHandler = StressUtil.CreateVSHandler(this);
            LogMessage(vsHandler.DoVSRegEdit("read local HKCU General DelayTimeThreshold dword"));

            LogMessage(vsHandler.DoVSRegEdit("read local HKCU General MaxNavigationHistoryDepth dword"));

        }

    }
}
