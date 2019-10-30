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
            int NumIterations = 3;
            string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
            try
            {
                LogMessage($"{nameof(StressIterateManually)} # iterations = {NumIterations}");
                for (int iteration = 0; iteration < NumIterations; iteration++)
                {
                    await OpenCloseSolutionOnce(SolutionToLoad);
                    await TakeMeasurementAsync(this, iteration);
                    LogMessage($"  Iter # {iteration}/{NumIterations}");
                }
            }
            finally
            {

            }

            await AllIterationsFinishedAsync(this);
        }
    }
}
