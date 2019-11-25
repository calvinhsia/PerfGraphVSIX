using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Test.Stress
{
    public class StressUtilOptions
    {
        /// <summary>
        /// Specify the number of iterations to do. Some scenarios are large (open/close solution)
        /// Others are small (scroll up/down a file)
        /// </summary>
        public int NumIterations = 7;
        /// <summary>
        /// Defaults to 1.0 Some perf counter thresholds are large (e.g. 1 megabyte for Private bytes). 
        /// The actual threshold used is the Thresh divided by Sensitivity.
        /// Thus, to find smaller leaks, magnify them by setting this to 1,000,000. Or make the test less sensitive by setting this to .1 (for 10 Meg threshold)
        /// </summary>
        public double Sensitivity = 1.0f;
        /// <summary>
        /// Defaults to 1. All delays (e.g. between iterations, after GC, are multiplied by this factor.
        /// Running the test with instrumented binaries (such as under http://Toolbox/MemSpect  will slow down the target process
        /// </summary>
        public int DelayMultiplier = 1;
        /// <summary>
        /// '|' separated list of processes to monitor VS, use 'devenv' To monitor the current process, use ''. Defaults to 'devenv'
        /// </summary>
        public string ProcNamesToMonitor = "devenv";
        /// <summary>
        /// Show results automatically, like the Graph of Measurements, the Dump in ClrObjExplorer, the Diff Analysis
        /// </summary>
        public bool ShowUI = false;
        public List<PerfCounterDataSetting> lstperfCounterDataSettings = null;
        /// <summary>
        ///  Specifies the iteration # at which to take a baseline. 
        ///    <paramref name="NumIterationsBeforeTotalToTakeBaselineSnapshot"/> is subtracted from <paramref name="NumIterations"/> to get the baseline iteration number
        /// e.g. 100 iterations, with <paramref name="NumIterationsBeforeTotalToTakeBaselineSnapshot"/>=4 means take a baseline at iteartion 100-4==96;
        /// </summary>
        public int NumIterationsBeforeTotalToTakeBaselineSnapshot = 4;
        public TimeSpan timeBetweenIterations = TimeSpan.FromSeconds(0);


        private object theTest;
        internal VSHandler VSHandler;

        private bool? _isApexTest;
        public ILogger logger; // this has to be public for user dynamically compiled ExecCode: we don't know the assembly name and it's not signed.
        internal TestContextWrapper testContext;
        internal MethodInfo _theTestMethod;
        internal List<PerfCounterData> lstPerfCountersToUse = PerfCounterData.GetPerfCountersForStress();

        /// <summary>
        /// if we're being called from a test, pass the test in. Else being called from a dynamic asm
        /// </summary>
        /// <param name="test"></param>
        /// <returns></returns>
        internal async Task<bool> SetTest(object test)
        {
            theTest = test;
            var testType = test.GetType();
            var methGetContext = testType.GetMethod($"get_TestContext");
            if (methGetContext == null)
            {
                throw new InvalidOperationException("can't get TestContext from test. Test must have 'public TestContext TestContext { get; set; }' (perhaps inherited)");
            }
            var val = methGetContext.Invoke(test, null);
            testContext = new TestContextWrapper(val);

            if (testContext.Properties[StressUtil.PropNameRecursionPrevention] != null)
            {
                return false;
            }
            testContext.Properties[StressUtil.PropNameRecursionPrevention] = 1;

            _theTestMethod = testType.GetMethod(testContext.TestName);
            if (logger == null)
            {
                var loggerFld = testType.GetField("logger");
                if (loggerFld != null)
                {
                    logger = loggerFld.GetValue(test) as ILogger;
                }
                if (logger == null)
                {
                    logger = new Logger(testContext);
                }
            }
            logger.LogMessage($@"TestName = {testContext.TestName} 
                                        IsApexTest={IsTestApexTest()}
                                        NumIterations = {NumIterations}
                                        Sensitivity = {Sensitivity}
                                        CurDir = '{Environment.CurrentDirectory}'
                                        TestDeploymentDir = '{testContext.TestDeploymentDir}'
                                        TestRunDirectory = '{testContext.TestRunDirectory}'  
                                        TestResultsDirectory='{testContext.TestResultsDirectory}' 
                                        TestRunResultsDirectory='{testContext.TestRunResultsDirectory}'
                ");
            /*
             * probs: the curdir is not empty, so results will be overwritten (might have ClrObjExplorer or WinDbg open with a result dump)
             *       The Test*dirs are all deleted after the run.
             *       Can use a Runsettings   
             *               <DeleteDeploymentDirectoryAfterTestRunIsComplete>False</DeleteDeploymentDirectoryAfterTestRunIsComplete>
             *              https://docs.microsoft.com/en-us/visualstudio/test/configure-unit-tests-by-using-a-dot-runsettings-file?view=vs-2019
                                    CurDir = 'C:\Users\calvinh\Source\Repos\PerfGraphVSIX\TestStress\bin\Debug'
                                    TestDeploymentDir = 'C:\Users\calvinh\Source\Repos\PerfGraphVSIX\TestResults\Deploy_calvinh 2019-11-15 12_04_59\Out'
                                    TestRunDirectory = 'C:\Users\calvinh\Source\Repos\PerfGraphVSIX\TestResults\Deploy_calvinh 2019-11-15 12_04_59'  
                                    TestResultsDirectory='C:\Users\calvinh\Source\Repos\PerfGraphVSIX\TestResults\Deploy_calvinh 2019-11-15 12_04_59\In' 
                                    TestRunResultsDirectory='C:\Users\calvinh\Source\Repos\PerfGraphVSIX\TestResults\Deploy_calvinh 2019-11-15 12_04_59\In\CALVINH2'
                apex local:
                    CurDir = 'C:\VS\src\Tests\Stress\Project\TestResults\Deploy_calvinh 2019-11-14 18_09_34\Out'
                    TestRunDirectory = 'C:\VS\src\Tests\Stress\Project\TestResults\Deploy_calvinh 2019-11-14 18_09_34'  
                    TestResultsDirectory='C:\VS\src\Tests\Stress\Project\TestResults\Deploy_calvinh 2019-11-14 18_09_34\In' 
                    TestRunResultsDirectory='C:\VS\src\Tests\Stress\Project\TestResults\Deploy_calvinh 2019-11-14 18_09_34\In\calvinhW7'

             * */
            VSHandler vSHandler = null;
            if (string.IsNullOrEmpty(ProcNamesToMonitor))
            {
                PerfCounterData.ProcToMonitor = Process.GetCurrentProcess();
            }
            else
            {
                var vsHandlerFld = testType.GetFields(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.FieldType.Name == "VSHandler").FirstOrDefault();
                if (vsHandlerFld != null)
                {
                    vSHandler = vsHandlerFld.GetValue(test) as VSHandler;
                }
                if (vSHandler == null)
                {
                    vSHandler = testContext.Properties[StressUtil.PropNameVSHandler] as VSHandler;
                    if (vSHandler == null)
                    {
                        vSHandler = new VSHandler(logger, DelayMultiplier);
                        testContext.Properties[StressUtil.PropNameVSHandler] = vSHandler;
                    }
                }
                await vSHandler?.EnsureGotDTE(); // ensure we get the DTE. Even for Apex tests, we need to Tools.ForceGC
                VSHandler = vSHandler;
            }
            if (lstperfCounterDataSettings != null)
            {
                if (lstperfCounterDataSettings != null)
                {
                    foreach (var userSettingItem in lstperfCounterDataSettings) // for each user settings // very small list: linear search
                    {
                        var pCounterToModify = lstPerfCountersToUse.Where(p => p.perfCounterType == userSettingItem.perfCounterType).FirstOrDefault();
                        if (pCounterToModify != null)
                        {
                            pCounterToModify.thresholdRegression = userSettingItem.regressionThreshold;
                        }
                    }
                }
            }
            return true;
        }
        internal bool IsTestApexTest()
        {
            if (!_isApexTest.HasValue)
            {
                _isApexTest = false;
                var typ = theTest.GetType();
                var baseT = string.Empty;
                while (baseT != "Object")
                {
                    baseT = typ.BaseType.Name;
                    typ = typ.BaseType;
                    if (baseT == "ApexTest")
                    {
                        _isApexTest = true;
                        break;
                    }
                }
            }
            return _isApexTest.Value;
        }

    }
}
