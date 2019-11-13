using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestStress
{
    [TestClass]
    public class StressWithNoInheritance : BaseStressTestClass // inheritance is only used for logging and vs automation mechanics, not stress test mechanics
    {
        [TestInitialize]
        public async Task InitAsync()
        {
            await base.InitializeAsync();

            // the only change to existing test required: call to static method
            await BaseStressTestClass.DoIterationsAsync(this, NumIterations: 3, Sensitivity: 1);
        }
        [TestCleanup]
        public async Task Cleanup()
        {
            await base.CleanupAsync();
        }

        [TestMethod]
        public async Task StressNoInheritance()
        {
            string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
            await _VSHandler.OpenSolution(SolutionToLoad);

            await _VSHandler.CloseSolution();
        }
    }
}
