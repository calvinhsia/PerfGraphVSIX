using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;



/*
  rd /s /q c:\test
  c:\DDTest\Microsoft.DevDiv.TestPlatform.Client.exe /RunSettings:Stress.runsettings /CleanupDeployment:Never  /DeploymentDirectory:c:\Test 

 * don't cleanup deplopyment
   /CleanupDeployment:Never  
  
 * */


namespace LeakTestDatacollector
{
    [TestClass]
    public class DataCollectorStressTestsManualIterLeaky
    {
        public ILogger logger;

        public TestContext TestContext { get; set; }
        [TestInitialize]
        public async Task TestInitialize()
        {
            // when iterating, a new TestContext for each iteration, so we can't store info on the TestContext Property bag
            if (logger == null)
            {
                logger = new Logger(TestContext);
                logger.LogMessage($"Starting {new StackTrace().GetFrames()[0].GetMethod().Name}");
            }
            await Task.Yield();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            logger.LogMessage($"{nameof(TestCleanup)}");
        }

        class BigStuffWithLongNameSoICanSeeItBetter
        {
            readonly byte[] arr = new byte[1024 * 1024];
            public byte[] GetArray => arr;
        }

        readonly List<BigStuffWithLongNameSoICanSeeItBetter> _lst = new List<BigStuffWithLongNameSoICanSeeItBetter>();

        [TestMethod]
        public async Task LeakyManual()
        {
            await StressUtil.DoIterationsAsync(this, NumIterations: 11, Sensitivity: 1);
            // to test if your code leaks, put it here. Repeat a lot to magnify the effect
            for (int i = 0; i < 1; i++)
            {
                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
            }
        }
    }
}
