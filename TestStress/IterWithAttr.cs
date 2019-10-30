using DumperViewer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        internal async Task ProcessAttributesAsync()
        {
            LogMessage($"{nameof(ProcessAttributesAsync)} TestName = {TestContext.TestName}");
            var _theTestMethod = this.GetType().GetMethods().Where(m => m.Name == TestContext.TestName).First();

            MemSpectAttribute attr = (MemSpectAttribute)_theTestMethod.GetCustomAttribute(typeof(MemSpectAttribute));
            LogMessage($"Got attr {attr}");
            TestContext.Properties.Add("TestIterationCount", 0);

            for (int iteration = 0; iteration < attr.NumIterations; iteration++)
            {
                var result =_theTestMethod.Invoke(this, parameters: null);
                if (_theTestMethod.ReturnType.Name == "Task")
                {
                    var resultTask = (Task)result;
                    await resultTask;
                }
                //                await OpenCloseSolutionOnce(SolutionToLoad);
                LogMessage($"  Iter # {iteration}/{attr.NumIterations}");
                TestContext.Properties["TestIterationCount"] = ((int)TestContext.Properties["TestIterationCount"]) + 1;
                await Task.Delay(TimeSpan.FromSeconds(5 * DelayMultiplier));
            }
            await IterationsFinishedAsync();
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
            await base.ProcessAttributesAsync();
        }
        [TestCleanup]
        public async Task Cleanup()
        {
            await base.ShutDownVSAsync();
        }

        [TestMethod]
        [MemSpectAttribute(NumIterations = 1)]
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
