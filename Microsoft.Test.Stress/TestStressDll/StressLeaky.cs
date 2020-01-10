using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Microsoft.Test.Stress;
using System.IO;

namespace TestStressDll
{
    [TestClass]
    public class StressLeakyClass
    {
        public TestContext TestContext { get; set; }

        class BigStuffWithLongNameSoICanSeeItBetter
        {
            readonly byte[] arr;
            public BigStuffWithLongNameSoICanSeeItBetter(int initSize = 1024 * 1024 * 2) // over our 1M threshold
            {
                arr = new byte[initSize];
            }
            public byte[] GetArray => arr;
            public string MyString = ($"leaking string" + DateTime.Now.ToString()).Substring(0, 14); // make a calculated non-unique string so it looks like a leak
        }

        List<BigStuffWithLongNameSoICanSeeItBetter> _lst;

        [TestInitialize]
        public void TestInit()
        {
            _lst = new List<BigStuffWithLongNameSoICanSeeItBetter>();
            GC.Collect();
        }

        [TestMethod]
        [ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        public async Task StressLeaky()
        {
            string didGetLeakException = "didGetLeakException";
            TestContext.WriteLine($"Username=" + Environment.GetEnvironmentVariable("Username"));
            TestContext.WriteLine($"Computername=" + Environment.GetEnvironmentVariable("Computername"));
            TestContext.WriteLine($"UserDomain=" + Environment.GetEnvironmentVariable("userdomain"));
            int numIter = 11;
            try
            {
                await StressUtil.DoIterationsAsync(
                    this,
                    new StressUtilOptions() { NumIterations = numIter, ProcNamesToMonitor = string.Empty, ShowUI = false }
                    );

                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
            }
            catch (LeakException)
            {
                TestContext.Properties[didGetLeakException] = 1;
                throw;
            }
        }


        [TestMethod]
        [ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        public async Task StressLeakyCustomThreshold()
        {
            // example to set custom threshold: here we override one counter's threshold. We can also change sensitivity
            var thresh = 1e7f;
            try
            {
                var opts = new StressUtilOptions()
                {
                    PerfCounterOverrideSettings = new List<PerfCounterOverrideThreshold>
                    {
                        new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.GCBytesInAllHeaps, regressionThreshold = thresh } ,
                        new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.ProcessorPrivateBytes, regressionThreshold = 9 * thresh } , // use a very high thresh so this counter won't show as leak
                        new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.ProcessorVirtualBytes, regressionThreshold = 9 * thresh } ,
                        new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.KernelHandleCount, regressionThreshold = 9 * thresh } ,
                    },
                    NumIterations = 11,
                    ProcNamesToMonitor = string.Empty,
                    ShowUI = false

                };
                await StressUtil.DoIterationsAsync(this, opts);

            }
            catch (LeakException ex)
            {
                // validate only one counter leaked: GCBytesInAllHeaps
                var lkGCB = ex.lstLeakResults.Where(lk => lk.IsLeak && lk.perfCounterData.perfCounterType == PerfCounterType.GCBytesInAllHeaps).FirstOrDefault();
                if (lkGCB != null &&
                //if (ex.lstLeakResults.Where(lk => lk.IsLeak && lk.perfCounterData.perfCounterType == PerfCounterType.GCBytesInAllHeaps).FirstOrDefault() != null &&
                    ex.lstLeakResults.Where(lk => lk.IsLeak).Count() == 1
                    )
                {
                    if (lkGCB.perfCounterData.thresholdRegression == thresh) // verify we're using the provided thresh
                    {
                        throw; // it's a valid leak.. throw because test expects a LeakException, and test passes
                    }
                }
                Assert.Fail("Didn't get expected leak type");
            }

            _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter(initSize: (int)(thresh + 100000)));
        }



        readonly List<string> myList = new List<string>();

        [TestMethod]
        [ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        [Description("Sensitivity Leak a very small string of 14 chars")]
        [Ignore] // same test below with XML settings.
        public async Task StressLeakyDetectVerySmallLeak()
        {
            await StressUtil.DoIterationsAsync(this, new StressUtilOptions() { NumIterations = 711, ProcNamesToMonitor = "", Sensitivity = 1e6, DelayMultiplier = 0 });

            myList.Add($"leaking string" + "asdfafsdfasdfasd".Substring(0, 14));// needs to be done at runtime to create a diff string each iter. Time dominated by GC
        }


        [TestMethod]
        [ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        [DeploymentItem("StressLeakyWithCustomXMLSettings.settings.xml", "Assets")]
        [Ignore]
        public async Task StressLeakyWithCustomXMLSettings()
        {

            string didValidateSettingsRead = "didValidateSettingsRead";
            await StressUtil.DoIterationsAsync(this);
            var curIter = (int)TestContext.Properties[StressUtil.PropNameCurrentIteration];
            if (TestContext.Properties.Contains(StressUtil.PropNameLogger))
            {
                if (TestContext.Properties[StressUtil.PropNameLogger] is Logger logger)
                {
                    if (curIter == 0)
                    {
                        Assert.IsNotNull(logger._lstLoggedStrings.Where(s => s.Contains("Reading settings from")).First(), "should have read settings from xml file"); // this will assert each iteration
                        TestContext.Properties[didValidateSettingsRead] = 1;
                    }
                }
            }
            if (curIter == 0)
            {
                Assert.IsTrue((int)TestContext.Properties[didValidateSettingsRead] > 0, "didn't validate settings read");
            }
            myList.Add($"leaking string" + "asdfafsdfasdfasd".Substring(0, 14));// needs to be done at runtime to create a diff string each iter. Time dominated by GC

        }


        [TestMethod]
        [ExpectedException(typeof(LeakException))]
        public async Task StressLeakyVerifyDiffFileResult()
        {
            int numiter = 11;
            try
            {
                await StressUtil.DoIterationsAsync(this, new StressUtilOptions() { NumIterations = numiter, ProcNamesToMonitor = "" });

                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
            }
            catch (LeakException)
            {
                var lstFileResults = (List<FileResultsData>)TestContext.Properties[StressUtil.PropNameListFileResults];
                var diffFile = lstFileResults.Where(r => Path.GetFileName(r.filename).Contains(MeasurementHolder.DiffFileName)).First();
                var diffs = File.ReadAllText(diffFile.filename);
                TestContext.WriteLine("Verifying diff file");
                Assert.IsTrue(diffs.Contains("TestStressDll.StressLeakyClass+BigStuffWithLongNameSoICanSeeItBetter"), "doesn't have leaking type");

                Assert.IsTrue(diffs.Contains("leaking string"), "doesn't have leaking string"); // there's one more "leaking string" because it's a class static internally (in System.Object[] array of Pinned handle statics)

                Assert.IsTrue(lstFileResults.Where(r => Path.GetExtension(r.filename) == ".dmp").Count() == 2, "expected 2 dumps");
                throw;
            }
        }



        [TestMethod]
        [MemSpectAttribute(NumIterations = 17)]
        [ExpectedException(typeof(LeakException))]
        public async Task StressTestWithAttribute()
        {
            await ProcessAttributesAsync(this);
            _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
        }
        [TestMethod]
        [MemSpectAttribute(NumIterations = 17, Sensitivity = 1)]
        [ExpectedException(typeof(LeakException))]
        public void StressTestWithAttributeNotAsync()
        {
            try
            {
                ProcessAttributesAsync(this).Wait();
                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
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
            await StressUtil.DoIterationsAsync(
                this,
                new StressUtilOptions()
                {
                    NumIterations = attr.NumIterations,
                    Sensitivity = attr.Sensitivity,
                    ProcNamesToMonitor = ""
                });

        }
    }
}
