using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Test.Stress
{
    public class StressUtil
    {
        public const string PropNameVSHandler = "VSHandler";
        public const string PropNameIteration = "IterationNumber"; // range from 0 - #Iter -  1
        public const string PropNameListFileResults = "DictListFileResults";
        /// <summary>
        /// Iterate the test method the desired number of times
        /// The call can be made from the TestInitialize or the beginning of the TestMethod
        /// </summary>
        /// <param name="test">pass the test itself</param>
        /// <param name="stressUtilOptions">If passed, includes all options: all other optional parameters are ignored</param>
        /// <param name="NumIterations">The number of iterations (defaults to 7)</param>
        public static async Task DoIterationsAsync(
            object test,
            StressUtilOptions stressUtilOptions = null,
            int NumIterations = 7)
        {
            const string PropNameRecursionPrevention = "RecursionPrevention";
            ILogger logger = test as ILogger;
            try
            {
                if (stressUtilOptions == null)
                {
                    stressUtilOptions = new StressUtilOptions()
                    {
                        NumIterations = NumIterations,
                    };
                }
                stressUtilOptions.SetTest(test);
                var testType = test.GetType();
                var methGetContext = testType.GetMethod($"get_TestContext");
                if (methGetContext == null)
                {
                    throw new InvalidOperationException("can't get TestContext from test. Test must have 'public TestContext TestContext { get; set; }' (perhaps inherited)");
                }
                var val = methGetContext.Invoke(test, null);
                TestContextWrapper testContext = new TestContextWrapper(val);

                if (testContext.Properties[PropNameRecursionPrevention] != null)
                {
                    return;
                }
                testContext.Properties[PropNameRecursionPrevention] = 1;

                var _theTestMethod = testType.GetMethod(testContext.TestName);
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
                var isApexTest = stressUtilOptions.IsTestApexTest();
                logger.LogMessage($@"{
                    nameof(DoIterationsAsync)} TestName = {testContext.TestName} 
                                        IsApexTest={isApexTest}
                                        NumIterations = {stressUtilOptions.NumIterations}
                                        Sensitivity = {stressUtilOptions.Sensitivity}
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
                if (string.IsNullOrEmpty(stressUtilOptions.ProcNamesToMonitor))
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
                        vSHandler = testContext.Properties[PropNameVSHandler] as VSHandler;
                        if (vSHandler == null)
                        {
                            vSHandler = new VSHandler(logger, stressUtilOptions.DelayMultiplier);
                            testContext.Properties[PropNameVSHandler] = vSHandler;
                        }
                    }
                    await vSHandler?.EnsureGotDTE(); // ensure we get the DTE. Even for Apex tests, we need to Tools.ForceGC
                    stressUtilOptions.VSHandler = vSHandler;
                }
                var lstPerfCountersToUse = PerfCounterData.GetPerfCountersForStress(); // very small list: linear search
                if (stressUtilOptions.lstperfCounterDataSettings != null)
                {
                    foreach (var userSettingItem in stressUtilOptions.lstperfCounterDataSettings) // for each user settings
                    {
                        var pCounterToModify = lstPerfCountersToUse.Where(p => p.perfCounterType == userSettingItem.perfCounterType).FirstOrDefault();
                        if (pCounterToModify != null)
                        {
                            pCounterToModify.thresholdRegression = userSettingItem.regressionThreshold;
                        }
                    }
                }

                using (var measurementHolder = new MeasurementHolder(
                    testContext,
                    lstPerfCountersToUse,
                    stressUtilOptions,
                    SampleType.SampleTypeIteration,
                    logger: logger))
                {
                    var baseDumpFileName = string.Empty;
                    testContext.Properties[PropNameIteration] = 0;

                    for (int iteration = 0; iteration < stressUtilOptions.NumIterations; iteration++)
                    {
                        var result = _theTestMethod.Invoke(test, parameters: null);
                        if (_theTestMethod.ReturnType.Name == "Task")
                        {
                            var resultTask = (Task)result;
                            await resultTask;
                        }

                        var res = await measurementHolder.TakeMeasurementAsync($"Iter {iteration + 1,3}/{stressUtilOptions.NumIterations}");
                        logger.LogMessage(res);
                        testContext.Properties[PropNameIteration] = (int)(testContext.Properties[PropNameIteration]) + 1;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage(ex.ToString());

                throw;
            }
        }
    }

    public class LeakException : Exception
    {
        public List<LeakAnalysisResult> lstLeakResults;

        public LeakException(string message, List<LeakAnalysisResult> lstRegResults) : base(message)
        {
            this.lstLeakResults = lstRegResults;
        }
    }

}
