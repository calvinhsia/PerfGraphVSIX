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
        public const string PropNameIteration = "IterationNumber"; // range from 0 - #Iter -  1
        public const string PropNameListFileResults = "DictListFileResults";
        internal const string PropNameRecursionPrevention = "RecursionPrevention";
        internal const string PropNameVSHandler = "VSHandler";
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
            try
            {
                if (stressUtilOptions == null)
                {
                    stressUtilOptions = new StressUtilOptions()
                    {
                        NumIterations = NumIterations
                    };
                }
                if (!await stressUtilOptions.SetTest(test)) // are we recurring?
                {
                    return;
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
                    stressUtilOptions.testContext,
                    lstPerfCountersToUse,
                    stressUtilOptions,
                    SampleType.SampleTypeIteration))
                {
                    var baseDumpFileName = string.Empty;
                    stressUtilOptions.testContext.Properties[PropNameIteration] = 0;

                    for (int iteration = 0; iteration < stressUtilOptions.NumIterations; iteration++)
                    {
                        var result = stressUtilOptions._theTestMethod.Invoke(test, parameters: null);
                        if (stressUtilOptions._theTestMethod.ReturnType.Name == "Task")
                        {
                            var resultTask = (Task)result;
                            await resultTask;
                        }

                        var res = await measurementHolder.TakeMeasurementAsync($"Iter {iteration + 1,3}/{stressUtilOptions.NumIterations}");
                        stressUtilOptions.logger.LogMessage(res);
                        stressUtilOptions.testContext.Properties[PropNameIteration] = (int)(stressUtilOptions.testContext.Properties[PropNameIteration]) + 1;
                    }
                }
            }
            catch (Exception ex)
            {
                stressUtilOptions.logger.LogMessage(ex.ToString());

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
