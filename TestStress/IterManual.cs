using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DumperViewer;

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
            await Task.Yield();
            AsyncPump.Run(async () =>
            {
                int NumIterations = 1;
                string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
                try
                {
                    LogMessage($"{nameof(StressIterateManually)} # iterations = {NumIterations}");
                    //                await TakeMeasurementAsync(this, -1);
                    await Task.Delay(TimeSpan.FromSeconds(5 * DelayMultiplier));
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
            });

        }
    }
}
