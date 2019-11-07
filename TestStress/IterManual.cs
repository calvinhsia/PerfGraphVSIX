using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DumperViewer;
using PerfGraphVSIX;

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
                int NumIterations = 3;
                string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
                try
                {
                    LogMessage($"{nameof(StressIterateManually)} # iterations = {NumIterations}");
                    //                await TakeMeasurementAsync(this, -1);
                    await Task.Delay(TimeSpan.FromSeconds(5 * DelayMultiplier));

                    var measurementHolder = new MeasurementHolder(
                        nameof(StressIterateManually), 
                        PerfCounterData._lstPerfCounterDefinitionsForStressTest, 
                        SampleType.SampleTypeIteration,
                        logger: this);


                    for (int iteration = 0; iteration < NumIterations; iteration++)
                    {
                        await OpenCloseSolutionOnce(SolutionToLoad);
                        await TakeMeasurementAsync(this, measurementHolder, $"Start of Iter {iteration + 1}/{NumIterations}");
                    }
                    measurementHolder.DumpOutMeasurementsToTempFile(StartExcel: false);
                    if (measurementHolder.CalculateRegression())
                    {
                        LogMessage("Regression!!!!!");
                    }
                }
                finally
                {

                }

            });

        }
    }
}
