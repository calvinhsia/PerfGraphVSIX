using Microsoft.Test.Stress;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestStress
{
    [TestClass]
    public class StressVSMemSpect
    {

        public TestContext TestContext { get; set; }
        ILogger logger;
        IVSHandler _VSHandler;
        [TestInitialize]
        public async Task TestInitialize()
        {
            logger = new Logger(new TestContextWrapper(TestContext));
            _VSHandler = new VSHandlerCreator().CreateVSHandler(logger, delayMultiplier: 10);

            await _VSHandler.StartVSAsync(flags: MemSpectModeFlags.MemSpectModeFull);
            logger.LogMessage($"TestInit starting VS pid= {_VSHandler.VsProcess.Id}");
        }

        [TestMethod]
        [Ignore]
        [ExpectedException(typeof(LeakException))]
        public async Task StressMemSpectOpenCloseSln()
        {
            try
            {
                // 30 min for 7 iter
                await StressUtil.DoIterationsAsync(this, new StressUtilOptions() { ShowUI = true, NumIterations = 3 });

                await _VSHandler.OpenSolutionAsync(StressVS.SolutionToLoad);

                await _VSHandler.CloseSolutionAsync();
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
            // we leave VS alive so can be controlled via MemSpect UI
//            _VSHandler.ShutDownVSAsync().Wait();

        }

    }
}
