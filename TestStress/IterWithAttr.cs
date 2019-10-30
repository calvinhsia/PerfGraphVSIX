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
        internal void ProcessAttributes()
        {
            LogMessage($"{nameof(ProcessAttributes)} TestName = {TestContext.TestName}");
            var _theTestMethod = this.GetType().GetMethods().Where(m => m.Name == TestContext.TestName).First();

            MemSpectAttribute attr = (MemSpectAttribute)_theTestMethod.GetCustomAttribute(typeof(MemSpectAttribute));
            LogMessage($"Got attr {attr}");
            TestContext.Properties.Add("TestIterationCount", 0);

            for (int iteration = 0; iteration < attr.NumIterations; iteration++)
            {



                TestContext.Properties["TestIterationCount"] = ((int)TestContext.Properties["TestIterationCount"]) + 1;

            }
        }
    }



    [TestClass]
    public class IterWithAttr : BaseTestWithAttribute
    {
        [TestInitialize]
        public async Task InitializeAsync()
        {
            await StartVSAsync();
            LogMessage($"");
            base.ProcessAttributes();
        }
        [TestCleanup]
        public async Task Cleanup()
        {
            await base.ShutDownVSAsync();
        }

        [TestMethod]
        [MemSpectAttribute(NumIterations = 2)]
        public async Task StressTestWithAttribute()
        {
            await OpenCloseSolutionOnce();
        }
    }
}
