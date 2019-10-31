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

            for (int iteration = 0; iteration < attr.NumIterations; iteration++)
            {
                var result =_theTestMethod.Invoke(test, parameters: null);
                if (_theTestMethod.ReturnType.Name == "Task")
                {
                    var resultTask = (Task)result;
                    await resultTask;
                }
                //                await OpenCloseSolutionOnce(SolutionToLoad);
                test.LogMessage($"  Iter # {iteration}/{attr.NumIterations}");
                testContext.Properties["TestIterationCount"] = ((int)testContext.Properties["TestIterationCount"]) + 1;
                await TakeMeasurementAsync(test, iteration);
            }
            await AllIterationsFinishedAsync(test);
        }
    }



    [TestClass]
    public class IterWithAttr : BaseTestWithAttribute
    {
        [TestInitialize]
        public async Task InitializeAsync()
        {
            LogMessage($"{nameof(InitializeAsync)}");
            await StartVSAsync();
            await ProcessAttributesAsync(TestContext, this);
        }
        [TestCleanup]
        public async Task Cleanup()
        {
            await base.ShutDownVSAsync();
        }

        [TestMethod]
        [MemSpectAttribute(NumIterations = 3)]
        public async Task StressTestWithAttribute()
        {
            string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
            await OpenCloseSolutionOnce(SolutionToLoad);
        }
        [TestMethod]
        [MemSpectAttribute(NumIterations = 3)]
        public void StressTestWithAttributeNotAsync()
        {
            LogMessage($"{nameof(StressTestWithAttributeNotAsync)}");
            string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
            var tsk = OpenCloseSolutionOnce(SolutionToLoad);
            tsk.Wait();
        }

    }
}
