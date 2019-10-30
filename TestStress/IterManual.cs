using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TestStress
{

    [TestClass]
    public class IterManual : BaseStressTestClass
    {
        [TestInitialize]
        public async Task InitializeAsync()
        {
            LogMessage($"{nameof(InitializeAsync)}");
            await base.InitializeBaseAsync();
            await StartVSAsync();
            LogMessage($"done {nameof(InitializeAsync)}");
        }


        [TestCleanup]
        public async Task Cleanup()
        {
            LogMessage($"{nameof(Cleanup)}");
            await base.ShutDownVSAsync();
            LogMessage($"done {nameof(Cleanup)}");
        }

        [TestMethod]
        public async Task StressIterateManually()
        {
            int NumIterations = 2;
            try
            {
                LogMessage($"{nameof(StressIterateManually)} # iterations = {NumIterations}");
                for (int i = 0; i < NumIterations; i++)
                {
                    await OpenCloseSolutionOnce();
                    LogMessage($"  Iter # {i}/{NumIterations}");
                }
            }
            finally
            {

            }

            await IterationsFinishedAsync();
        }
    }
}
