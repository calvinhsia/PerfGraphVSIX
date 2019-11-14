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
        ILogger logger;
        public TestContext TestContext { get; set; }
        VSHandler _VSHandler;
        [TestInitialize]
        public void TestInitialize()
        {
            logger = new Logger(TestContext);
            var procVS = System.Diagnostics.Process.Start(BaseStressTestClass.vsPath);
            logger.LogMessage($"TestInit starting VS pid= {procVS.Id}");
            _VSHandler = new VSHandler(logger);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _VSHandler.ShutDownVSAsync().Wait();
        }

//        [TestMethod]
        public async Task StressStartVSApexSim() // Apex starts VS and we'll look for it. Simulate by starting vs directly in TestInitialize
        {
            // the only change to existing test required: call to static method
            await StressUtil.DoIterationsAsync(this, NumIterations: 3);

            string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
            await _VSHandler.OpenSolution(SolutionToLoad);

            await _VSHandler.CloseSolution();
        }
    }

    [TestClass]
    public class StressExistingVSNoVSHandlerField
    {
        System.Diagnostics.Process procVS;
        ILogger logger;
        public TestContext TestContext { get; set; }
        [TestInitialize]
        public void TestInitialize()
        {
            logger = new Logger(TestContext);
            procVS = System.Diagnostics.Process.Start(BaseStressTestClass.vsPath); // simulate Apex starting VS
            logger.LogMessage($"TestInit starting VS pid= {procVS.Id}");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            VSHandler VSHandler =  TestContext.Properties[StressUtil.PropNameVSHandler]  as VSHandler;
            VSHandler.ShutDownVSAsync().Wait();
        }

        [TestMethod]
        public async Task StressStartVSApexSimNoVSHandler() // Apex starts VS and we'll look for it. Simulate by starting vs directly in TestInitialize
        {
            try
            {
                // the only change to existing test required: call to static method
                await StressUtil.DoIterationsAsync(this, NumIterations: 3);


                string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
                if (!(TestContext.Properties[StressUtil.PropNameVSHandler] is VSHandler vSHandler))
                {
                    throw new InvalidOperationException("null vshandler");
                }
                await vSHandler.OpenSolution(SolutionToLoad);

                await vSHandler.CloseSolution();

            }
            catch (Exception ex)
            {
                logger.LogMessage($"Exception {ex}");
            }
        }

        [TestClass]
        public class StressExistingVSNoVSHandlerFieldLater
        {
            ILogger logger;
            public TestContext TestContext { get; set; }
            [TestInitialize]
            public void TestInitialize()
            {
                logger = new Logger(TestContext);
                logger.LogMessage($"TestInit : not starting VS immediately");
                var tsk = Task.Run(async () =>
                {
                    int nDelay = 5;
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        nDelay = 30;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(nDelay));
                    logger.LogMessage($"TestInit : starting VS after {nDelay} secs delay");
                    System.Diagnostics.Process.Start(BaseStressTestClass.vsPath); // simulate Apex starting VS
                });

            }

            [TestCleanup]
            public void TestCleanup()
            {
                VSHandler VSHandler = TestContext.Properties[StressUtil.PropNameVSHandler] as VSHandler;
                VSHandler.ShutDownVSAsync().Wait();
            }

            [TestMethod]
            public async Task StressStartVSLaterApexSimNoVSHandler() // Apex starts VS and we'll look for it. Simulate by starting vs directly in TestInitialize
            {
                try
                {
                    // the only change to existing test required: call to static method
                    await StressUtil.DoIterationsAsync(this, NumIterations: 3);


                    if (!(TestContext.Properties[StressUtil.PropNameVSHandler] is VSHandler vSHandler))
                    {
                        throw new InvalidOperationException("null vshandler");
                    }

                    string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
                    await vSHandler.OpenSolution(SolutionToLoad);

                    await vSHandler.CloseSolution();

                }
                catch (Exception ex)
                {
                    logger.LogMessage($"Exception {ex}");
                }
            }
        }

    }


}
