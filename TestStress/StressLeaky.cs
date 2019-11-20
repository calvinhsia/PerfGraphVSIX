using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using StressTestUtility;

namespace TestStress
{
    [TestClass]
    public class StressLeakyClass
    {
        public TestContext TestContext { get; set; }

        class BigStuffWithLongNameSoICanSeeItBetter
        {
            readonly byte[] arr = new byte[1024 * 1024];
            public byte[] GetArray => arr;
        }

        readonly List<BigStuffWithLongNameSoICanSeeItBetter> _lst = new List<BigStuffWithLongNameSoICanSeeItBetter>();

        [TestMethod]
        //[ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        public async Task StressLeaky()
        {
            // Need add only 1 line in test (either at beginning of TestMethod or at end of TestInitialize)
            await StressUtil.DoIterationsAsync(this, NumIterations: 11, ProcNamesToMonitor: "", ShowUI:true);

            // to test if your code leaks, put it here. Repeat a lot to magnify the effect
            for (int i = 0; i < 1; i++)
            {
                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
            }
        }


        [TestMethod]
        [MemSpectAttribute(NumIterations = 7)]
        [ExpectedException(typeof(LeakException))]
        public async Task StressTestWithAttribute()
        {
            await ProcessAttributesAsync(this);
            // to test if your code leaks, put it here. Repeat a lot to magnify the effect
            for (int i = 0; i < 1; i++)
            {
                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
            }
        }
        [TestMethod]
        [MemSpectAttribute(NumIterations = 7, Sensitivity = 1)]
        [ExpectedException(typeof(LeakException))]
        public void StressTestWithAttributeNotAsync()
        {
            try
            {
                ProcessAttributesAsync(this).Wait();
                // to test if your code leaks, put it here. Repeat a lot to magnify the effect
                for (int i = 0; i < 1; i++)
                {
                    _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
                }
            }
            catch (AggregateException ex)
            {
                if (ex.InnerExceptions?.Count == 1)
                {
                    TestContext.WriteLine($"Agg exception with 1 inner {ex.ToString()}");
                    throw ex.InnerExceptions[0];
                }
                TestContext.WriteLine($"Agg exception with !=1 inner {ex.ToString()}");
                throw ex;
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Final exception {ex.ToString()}");
            }
        }
        public async Task ProcessAttributesAsync(object test)
        {
            MemSpectAttribute attr = (MemSpectAttribute)(test.GetType().GetMethod(TestContext.TestName).GetCustomAttribute(typeof(MemSpectAttribute)));

            //            MemSpectAttribute attr = (MemSpectAttribute)_theTestMethod.GetCustomAttribute(typeof(MemSpectAttribute)));
            await StressUtil.DoIterationsAsync(this, NumIterations: attr.NumIterations, attr.Sensitivity, ProcNamesToMonitor: "");

        }
    }
}
