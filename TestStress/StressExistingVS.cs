using EnvDTE;
using LeakTestDatacollector;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestStress
{
    [TestClass]
    public class StressExistingVS
    {
        System.Diagnostics.Process procVS;
        ILogger logger;
        public TestContext TestContext { get; set; }
        VSHandler _VSHandler;
        [TestInitialize]
        public void TestInitialize()
        {
            logger = new Logger(TestContext);
            procVS = System.Diagnostics.Process.Start(BaseStressTestClass.vsPath);
            logger.LogMessage($"TestInit starting VS pid= {procVS.Id}");
            _VSHandler = new VSHandler(logger);
        }

        [TestCleanup]
        public void TestCleanup()
        {

        }

        [TestMethod]
        public async Task StressStartVSApexSim() // Apex starts VS and we'll look for it. Simulate by starting vs directly in TestInitialize
        {
            // the only change to existing test required: call to static method
            await StressUtil.DoIterationsAsync(this, NumIterations: 3);

            string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
            await _VSHandler.OpenSolution(SolutionToLoad);

            await _VSHandler.CloseSolution();


        }
    }
}
