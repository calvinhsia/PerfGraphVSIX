using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestStress
{
    [TestClass]
    public class StressWithNoInheritance: BaseStressTestClass // inheritance is only used for logging and vs automation mechanics, not stress test mechanics
    {
        [TestInitialize]
        public async Task InitAsync()
        {
            await StartVSAsync();

            // the only change to existing test required: call to static method
            await BaseStressTestClass.DoIterationsAsync(this, NumIterations:7);
        }
        [TestCleanup]
        public async Task Cleanup()
        {
            await ShutDownVSAsync();
        }

        [TestMethod]
        public async Task StressNoInheritance()
        {
            string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
            await OpenCloseSolutionOnce(SolutionToLoad);
        }
    }
}
