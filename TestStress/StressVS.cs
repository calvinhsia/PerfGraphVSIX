using LeakTestDatacollector;
using Microsoft.Test.Stress;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestStress
{
    [TestClass]
    public class StressVS
    {
        public const string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";

        public TestContext TestContext { get; set; }
        ILogger logger;
        VSHandler _VSHandler;
        [TestInitialize]
        public async Task TestInitialize()
        {
            logger = new Logger(new TestContextWrapper(TestContext));
            _VSHandler = new VSHandler(logger);

            await _VSHandler.StartVSAsync();
            logger.LogMessage($"TestInit starting VS pid= {_VSHandler.vsProc.Id}");
        }

        [TestMethod]
        [ExpectedException(typeof(LeakException))]
        public async Task StressOpenCloseSln()
        {
            try
            {
                // the only change to existing test required: call to static method
                await StressUtil.DoIterationsAsync(this, NumIterations: 7);

                await _VSHandler.OpenSolution(SolutionToLoad);

                await _VSHandler.CloseSolution();
            }
            catch (Exception ex)
            {
                logger.LogMessage($"Exception {ex}");
                throw;
            }
        }


        [TestCleanup]
        public void TestCleanup()
        {
            _VSHandler.ShutDownVSAsync().Wait();
        }
    }

    [TestClass]
    public class StressExistingVS
    {
        ILogger logger;
        public TestContext TestContext { get; set; }
        VSHandler _VSHandler;

        [TestInitialize]
        public async Task TestInitialize()
        {
            logger = new Logger(new TestContextWrapper(TestContext));
            _VSHandler = new VSHandler(logger);
            await _VSHandler.StartVSAsync();
            logger.LogMessage($"TestInit starting VS pid= {_VSHandler.vsProc.Id}");
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

            await _VSHandler.OpenSolution(StressVS.SolutionToLoad);

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
            logger = new Logger(new TestContextWrapper(TestContext));
            procVS = Process.Start(VSHandler.GetVSFullPath()); // simulate Apex starting VS
            logger.LogMessage($"TestInit starting VS pid= {procVS.Id}");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            VSHandler VSHandler = TestContext.Properties[StressUtil.PropNameVSHandler] as VSHandler;
            VSHandler.ShutDownVSAsync().Wait();
        }

        [TestMethod]
        public async Task StressStartVSApexSimNoVSHandler() // Apex starts VS and we'll look for it. Simulate by starting vs directly in TestInitialize
        {
            try
            {
                await StressUtil.DoIterationsAsync(this, NumIterations: 2);


                if (!(TestContext.Properties[StressUtil.PropNameVSHandler] is VSHandler vSHandler))
                {
                    throw new InvalidOperationException("null vshandler");
                }
                await vSHandler.OpenSolution(StressVS.SolutionToLoad);

                await vSHandler.CloseSolution();

            }
            catch (Exception ex)
            {
                logger.LogMessage($"Exception {ex}");
                throw;
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
                logger = new Logger(new TestContextWrapper(TestContext));
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
                    Process.Start(VSHandler.GetVSFullPath()); // simulate Apex starting VS
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
                    await StressUtil.DoIterationsAsync(this, NumIterations: 2);


                    if (!(TestContext.Properties[StressUtil.PropNameVSHandler] is VSHandler vSHandler))
                    {
                        throw new InvalidOperationException("null vshandler");
                    }

                    await vSHandler.OpenSolution(StressVS.SolutionToLoad);

                    await vSHandler.CloseSolution();

                }
                catch (Exception ex)
                {
                    logger.LogMessage($"Exception {ex}");
                    throw;
                }
            }
        }

    }


}
