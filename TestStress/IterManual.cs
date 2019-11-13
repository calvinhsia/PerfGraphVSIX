using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DumperViewer;
using PerfGraphVSIX;
using System.Linq;

namespace TestStress
{

    [TestClass]
    public class IterManual : BaseStressTestClass
    {
        [TestInitialize]
        public override async Task InitializeAsync()
        {
            LogMessage($"{nameof(InitializeAsync)}");
            await base.InitializeAsync();
            LogMessage($"done {nameof(InitializeAsync)}");
        }


        [TestCleanup]
        public override async Task CleanupAsync()
        {
            LogMessage($"{nameof(CleanupAsync)}");
            await base.CleanupAsync();
            LogMessage($"done {nameof(CleanupAsync)}");
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
                        logger: this, 
                        sensitivity: 1);


                    for (int iteration = 0; iteration < NumIterations; iteration++)
                    {
                        await _VSHandler.OpenSolution(SolutionToLoad);

                        await _VSHandler.CloseSolution();

                        await TakeMeasurementAsync(this, measurementHolder, $"Start of Iter {iteration + 1}/{NumIterations}");
                    }
                    measurementHolder.DumpOutMeasurementsToTempFile(StartExcel: false);
                    var lstRegResults = (await measurementHolder.CalculateRegressionAsync(showGraph: true)).Where(r => r.IsRegression).ToList();
                    if (lstRegResults.Count > 0)
                    {
                        foreach (var regres in lstRegResults)
                        {
                            LogMessage($"Regression!!!!! {regres}");
                        }
                    }
                }
                finally
                {

                }

            });

        }
    }
}
