using DumperViewer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TestStress
{

    public class BaseTestWithAttribute : BaseStressTestClass
    {
        public static async Task ProcessAttributesAsync(TestContext testContext, BaseStressTestClass test)
        {
            test.LogMessage($"{nameof(ProcessAttributesAsync)} TestName = {testContext.TestName}");
            var _theTestMethod = test.GetType().GetMethods().Where(m => m.Name == testContext.TestName).First();

            MemSpectAttribute attr = (MemSpectAttribute)_theTestMethod.GetCustomAttribute(typeof(MemSpectAttribute));
            test.LogMessage($"Got attr {attr}");
            testContext.Properties.Add("TestIterationCount", 0);

            var measurementHolder = new MeasurementHolder(
                test.TestContext.TestName,
                PerfCounterData._lstPerfCounterDefinitionsForStressTest,
                SampleType.SampleTypeIteration,
                logger: test);
            test.TestContext.Properties[nameof(MeasurementHolder)] = measurementHolder;

            for (int iteration = 0; iteration < attr.NumIterations; iteration++)
            {
                var result = _theTestMethod.Invoke(test, parameters: null);
                if (_theTestMethod.ReturnType.Name == "Task")
                {
                    var resultTask = (Task)result;
                    await resultTask;
                }
                testContext.Properties["TestIterationCount"] = ((int)testContext.Properties["TestIterationCount"]) + 1;
                await TakeMeasurementAsync(test, measurementHolder, $"Start of Iter {iteration + 1}/{attr.NumIterations}");
            }
            var filenameResults = measurementHolder.DumpOutMeasurementsToTempFile(StartExcel: false);
            test.LogMessage($"Measurement Results {filenameResults}");
            var lstRegResults = (await measurementHolder.CalculateRegressionAsync(showGraph: true)).Where(r => r.IsRegression).ToList();
            if (lstRegResults.Count > 0)
            {
                foreach (var regres in lstRegResults)
                {
                    test.LogMessage($"Regression!!!!! {regres}");
                }
                await measurementHolder.CreateDumpAsync(
                    test.TargetProc.Id,
                    desc: test.TestContext.TestName + "_" + attr.NumIterations.ToString(),
                    memoryAnalysisType: MemoryAnalysisType.StartClrObjectExplorer);
            }

        }
    }



    [TestClass]
    public class IterWithAttr : BaseTestWithAttribute
    {
        [TestInitialize]
        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            LogMessage($"{nameof(InitializeAsync)}");
            await ProcessAttributesAsync(TestContext, this);
        }
        [TestCleanup]
        public override async Task CleanupAsync()
        {
            await base.CleanupAsync();
        }

        [TestMethod]
        [MemSpectAttribute(NumIterations = 3)]
        public async Task StressTestWithAttribute()
        {
            string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
            await _VSHandler.OpenSolution(SolutionToLoad);

            await _VSHandler.CloseSolution();
        }
        [TestMethod]
        [MemSpectAttribute(NumIterations = 3, Sensitivity=1)]
        public void StressTestWithAttributeNotAsync()
        {
            LogMessage($"{nameof(StressTestWithAttributeNotAsync)}");
            string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
            _VSHandler.OpenSolution(SolutionToLoad).Wait();

            _VSHandler.CloseSolution().Wait();
        }

    }
}
