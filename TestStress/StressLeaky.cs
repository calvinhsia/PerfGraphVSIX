﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Microsoft.Test.Stress;
using System.IO;

namespace TestStress
{
    [TestClass]
    public class StressLeakyClass
    {
        public TestContext TestContext { get; set; }

        class BigStuffWithLongNameSoICanSeeItBetter
        {
            readonly byte[] arr;
            public BigStuffWithLongNameSoICanSeeItBetter(int initSize = 1024 * 1024 + 1000) // just a little over our 1M threshold
            {
                arr = new byte[initSize];
            }
            public byte[] GetArray => arr;
            public string MyString = ($"leaking string" + DateTime.Now.ToString()).Substring(0, 14); // make a calculated non-unique string so it looks like a leak
        }

        readonly List<BigStuffWithLongNameSoICanSeeItBetter> _lst = new List<BigStuffWithLongNameSoICanSeeItBetter>();

        [TestMethod]
        //[ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        public async Task StressLeaky()
        {
            // Need add only 1 line in test (either at beginning of TestMethod or at end of TestInitialize)
            await StressUtil.DoIterationsAsync(this, NumIterations: 11, ProcNamesToMonitor: "", ShowUI: true);

            _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
        }


        [TestMethod]
        [ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        public async Task StressLeakyCustomThreshold()
        {
            // example to set custom threshold: here we override one counter's threshold. We can also change sensitivity
            var thresh = 1e7f;
            var lstperfCounterDataSettings = new List<PerfCounterDataSetting>
            {
                new PerfCounterDataSetting { perfCounterType = PerfCounterType.GCBytesInAllHeaps, regressionThreshold = thresh } ,
                new PerfCounterDataSetting { perfCounterType = PerfCounterType.ProcessorPrivateBytes, regressionThreshold = 9 * thresh } , // use a very high thresh so this counter won't show as leak
                new PerfCounterDataSetting { perfCounterType = PerfCounterType.ProcessorVirtualBytes, regressionThreshold = 9 * thresh } ,
                new PerfCounterDataSetting { perfCounterType = PerfCounterType.KernelHandleCount, regressionThreshold = 9 * thresh } ,
            };
            try
            {
                await StressUtil.DoIterationsAsync(this, NumIterations: 11, ProcNamesToMonitor: "", ShowUI: false, lstperfCounterDataSettings: lstperfCounterDataSettings);

            }
            catch (LeakException ex)
            {
                // validate only one counter leaked: GCBytesInAllHeaps
                var lkGCB = ex.lstLeakResults.Where(lk => lk.IsLeak && lk.perfCounterData.perfCounterType == PerfCounterType.GCBytesInAllHeaps).FirstOrDefault();
                if ( lkGCB != null &&
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

            _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter(initSize: (int)(thresh + 1000)));
        }



        readonly List<string> myList = new List<string>();

        [TestMethod]
        //[ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        public async Task StressLeakySmall()
        {
            // Need add only 1 line in test (either at beginning of TestMethod or at end of TestInitialize)
            await StressUtil.DoIterationsAsync(this, NumIterations: 511, ProcNamesToMonitor: "", Sensitivity: 1e-6, ShowUI: true, DelayMultiplier: 0);

            myList.Add(($"leaking string" + DateTime.Now.ToString()).Substring(0, 14));
        }




        [TestMethod]
        //[ExpectedException(typeof(LeakException))] // to make the test pass, we need a LeakException. However, Pass deletes all the test results <sigh>
        public async Task StressLeakyLotsIter()
        {
            // Need add only 1 line in test (either at beginning of TestMethod or at end of TestInitialize)
            await StressUtil.DoIterationsAsync(this, NumIterations: 511, ProcNamesToMonitor: "", ShowUI: true);

            _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
        }


        [TestMethod]
        [ExpectedException(typeof(LeakException))]
        public async Task StressLeakyVerifyDiff()
        {
            int numiter = 11;
            try
            {
                // Need add only 1 line in test (either at beginning of TestMethod or at end of TestInitialize)
                await StressUtil.DoIterationsAsync(this, NumIterations: numiter, ProcNamesToMonitor: "", ShowUI: false);

                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
            }
            catch (LeakException)
            {
                var lstFileResults = (List<FileResultsData>)TestContext.Properties[StressUtil.PropNameListFileResults];
                var diffFile = lstFileResults.Where(r => Path.GetFileName(r.filename).Contains(MeasurementHolder.DiffFileName)).First();
                var diffs = File.ReadAllText(diffFile.filename);
                TestContext.WriteLine("Verifying diff file");
                Assert.IsTrue(diffs.Contains("7    11 TestStress.StressLeakyClass+BigStuffWithLongNameSoICanSeeItBetter"), "doesn't have leaking type");

                Assert.IsTrue(diffs.Contains("    8    12 leaking string"), "doesn't have leaking string"); // there's one more "leaking string" because it's a class static internally (in System.Object[] array of Pinned handle statics)

                Assert.IsTrue(lstFileResults.Where(r => Path.GetExtension(r.filename) == ".dmp").Count() == 2, "expected 2 dumps");
                throw;
            }
        }



        [TestMethod]
        [MemSpectAttribute(NumIterations = 7)]
        [ExpectedException(typeof(LeakException))]
        public async Task StressTestWithAttribute()
        {
            await ProcessAttributesAsync(this);
            _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
        }
        [TestMethod]
        [MemSpectAttribute(NumIterations = 7, Sensitivity = 1)]
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
            await StressUtil.DoIterationsAsync(this, NumIterations: attr.NumIterations, attr.Sensitivity, ProcNamesToMonitor: "");

        }
    }
}
