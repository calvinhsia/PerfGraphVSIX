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
        VSHandler _VSHandler;
        [TestInitialize]
        public async Task TestInitialize()
        {
            logger = new Logger(new TestContextWrapper(TestContext));
            _VSHandler = new VSHandler(logger, delayMultiplier: 10);

            await _VSHandler.StartVSAsync(memSpectModeFlags: MemSpectModeFlags.MemSpectModeFull);
            logger.LogMessage($"TestInit starting VS pid= {_VSHandler.vsProc.Id}");
        }

        [TestMethod]
        [ExpectedException(typeof(LeakException))]
        public async Task StressMemSpectOpenCloseSln()
        {
            try
            {
                // 30 min for 7 iter
                await StressUtil.DoIterationsAsync(this, new StressUtilOptions() { ShowUI = true, NumIterations = 3 });

                await _VSHandler.OpenSolution(StressVS.SolutionToLoad);

                await _VSHandler.CloseSolution();
            }
            catch (Exception ex)
            {
                logger.LogMessage($"Exception {ex}");
                throw;
            }
        }

    }
}
