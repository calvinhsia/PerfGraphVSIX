﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;


namespace Microsoft.Test.Stress
{
    public class StressUtil
    {
        public const string PropNameNumIterations = "NumIterations";
        public const string PropNameCurrentIteration = "IterationNumber"; // range from 0 - #Iter -  1
        public const string PropNameMinimumIteration = "MinimumIteration";
        public const string PropNameListFileResults = "DictListFileResults";
        public const string PropNameMeasurementHolder = "MeasurementHolder";
        internal const string PropNameRecursionPrevention = "RecursionPrevention";
        public const string PropNameVSHandler = "VSHandler";
        public const string PropNameLogger = "Logger";


        /// <summary>
        /// Iterate the test method the desired number of times
        /// The call can be made from the TestInitialize or the beginning of the TestMethod
        /// </summary>
        /// <param name="test">pass the test itself</param>
        /// <param name="stressUtilOptions">If passed, includes all options: all other optional parameters are ignored</param>
        /// <param name="NumIterations">The number of iterations (defaults to 71)</param>
        public static async Task DoIterationsAsync(
            object test,
            StressUtilOptions stressUtilOptions = null,
            int NumIterations = 71)
        {
            MeasurementHolder measurementHolder = null;
            try
            {
                if (stressUtilOptions == null)
                {
                    stressUtilOptions = new StressUtilOptions()
                    {
                        NumIterations = NumIterations
                    };
                }
                if (!await stressUtilOptions.SetTestAsync(test)) // are we recurring?
                {
                    return;
                }

                measurementHolder = new MeasurementHolder(
                    stressUtilOptions.testContext,
                    stressUtilOptions,
                    SampleType.SampleTypeIteration);

                var baseDumpFileName = string.Empty;
                stressUtilOptions.testContext.Properties[PropNameCurrentIteration] = 0;
                stressUtilOptions.testContext.Properties[PropNameMeasurementHolder] = measurementHolder;
                var utilFileName = typeof(StressUtil).Assembly.Location;
                var verInfo = FileVersionInfo.GetVersionInfo(utilFileName);
                /*
InternalName:     Microsoft.Test.Stress.dll
OriginalFilename: Microsoft.Test.Stress.dll
FileVersion:      1.1.29.55167
FileDescription:  Microsoft.Test.Stress
Product:          Microsoft.Test.Stress
ProductVersion:   1.1.29+g7fd76485e3
Debug:            False
Patched:          False
PreRelease:       False
PrivateBuild:     False
SpecialBuild:     False
Language:         Language Neutral
                 */
                stressUtilOptions.logger.LogMessage($"{utilFileName} {verInfo.OriginalFilename}  FileVersion:{verInfo.FileVersion}  ProductVesion:{verInfo.ProductVersion}");
                measurementHolder.dictTelemetryProperties["StressLibVersion"] = verInfo.FileVersion;

                for (int iteration = 0; iteration < stressUtilOptions.NumIterations; iteration++)
                {
                    if (stressUtilOptions.actExecuteBeforeEveryIterationAsync != null)
                    {
                        await stressUtilOptions.actExecuteBeforeEveryIterationAsync(iteration + 1, measurementHolder);
                    }
                    var result = stressUtilOptions._theTestMethod.Invoke(test, parameters: null);
                    if (stressUtilOptions._theTestMethod.ReturnType.Name == "Task")
                    {
                        var resultTask = (Task)result;
                        await resultTask;
                    }

                    var res = await measurementHolder.TakeMeasurementAsync($"Iter {iteration + 1,3}/{stressUtilOptions.NumIterations}", DoForceGC: true);
                    stressUtilOptions.logger.LogMessage(res);
                    stressUtilOptions.testContext.Properties[PropNameCurrentIteration] = (int)(stressUtilOptions.testContext.Properties[PropNameCurrentIteration]) + 1;
                }
                // note: if a leak is found an exception will be throw and this will not get called
                // increment one last time, so test methods can check for final execution after measurements taken
                stressUtilOptions.testContext.Properties[PropNameCurrentIteration] = (int)(stressUtilOptions.testContext.Properties[PropNameCurrentIteration]) + 1;
                DoIterationsFinished(measurementHolder, exception: null);

            }
            catch (Exception ex)
            {
                DoIterationsFinished(measurementHolder, ex);
                throw;
            }
        }

        private static void DoIterationsFinished(MeasurementHolder measurementHolder, Exception exception)
        {
            if (measurementHolder != null)
            {
                if (measurementHolder.testContext != null)
                {
                    measurementHolder.testContext.Properties[PropNameMeasurementHolder] = null;
                }
                if (exception != null)
                {
                    measurementHolder.Logger.LogMessage(exception.ToString());
                    if (exception is LeakException)
                    {
                        measurementHolder.dictTelemetryProperties["LeakException"] = exception.Message;
                    }
                    else
                    {
                        measurementHolder.dictTelemetryProperties["TestException"] = exception.ToString();
                    }
                }
                measurementHolder.dictTelemetryProperties["TestPassed"] = exception == null;
                measurementHolder.Dispose(); // write test results
            }
        }


        /// <summary>
        /// Iterate the test method the desired number of times and with the specified options.
        /// The call must be made after preparing the target process(es) for iteration.
        /// </summary>
        /// <param name="test">pass the test itself</param>
        /// <param name="stressUtilOptions">If passed, includes all options: all other optional parameters are ignored</param>
        /// <param name="numIterations">The number of iterations (defaults to 71)</param>
        public static void DoIterations(object test, StressUtilOptions stressUtilOptions = null, int numIterations = 71)
        {
            AsyncPump.Run(async () =>
            {
                await DoIterationsAsync(test, stressUtilOptions, numIterations);
            });
        }

        /// <summary>
        /// Need a VS Handler that's built against the right VSSdk for ENVDTE interop (Dev17, Dev16)
        /// </summary>
        public static IVSHandler CreateVSHandler(ILogger logger, int delayMultiplier = 1)
        {
            var thisasmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var seg = IntPtr.Size == 8 ? "64" : "32";
            var pathHandler = Path.Combine( // in subfolder called VSHandler32\VSHandler32.dll so dependent assemblies are separate for 32 and 64 bit
                thisasmDir,
                $"VSHandler{seg}",
                $"VSHandler{seg}.dll");
            if (!File.Exists(pathHandler))
            {
                throw new FileNotFoundException(pathHandler);
            }
            Assembly asm = Assembly.LoadFrom(pathHandler);
            var typ = asm.GetType("Microsoft.Test.Stress.VSHandler");
            var vsHandler = (IVSHandler)Activator.CreateInstance(typ);
            vsHandler.Initialize(logger, delayMultiplier);
            return vsHandler;
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
