using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.Test.Stress
{
    /// <summary>
    /// MemSpect tracks native and managed allocations, as well as files, heapcreates, etc. Each allocation has ThreadId, call stack, size, additional info (like filename)
    /// When an object is freed (or Garbage collected) this info is discarded)
    /// Things are much slower with MemSpect, but can be much easier to diagnose. Tracking only native or only managed makes things faster.
    /// To use MemSpect here, environment variables need to be set before the target process starts the CLR.
    /// </summary>
    [Flags]
    public enum MemSpectModeFlags
    {
        MemSpectModeNone = 0,
        /// <summary>
        /// Track Native Allocations.
        /// </summary>
        MemSpectModeNative = 0x1,
        /// <summary>
        /// Tracks managed memory allocations
        /// </summary>
        MemSpectModeManaged = 0x2,
        MemSpectModeFull = MemSpectModeManaged | MemSpectModeNative,
    }

    public class StressUtil
    {
        public const string PropNameCurrentIteration = "IterationNumber"; // range from 0 - #Iter -  1
        public const string PropNameListFileResults = "DictListFileResults";
        internal const string PropNameRecursionPrevention = "RecursionPrevention";
        internal const string PropNameVSHandler = "VSHandler";
        internal const string PropNameLogger = "Logger";

        private static TelemetrySession telemetrySession = null;
        private static bool firstIteration = true;

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
            try
            {
                if (firstIteration)
                {
                    PostInitialTelemetry();
                    firstIteration = false;
                }

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

                using (var measurementHolder = new MeasurementHolder(
                    stressUtilOptions.testContext,
                    stressUtilOptions,
                    SampleType.SampleTypeIteration))
                {
                    var baseDumpFileName = string.Empty;
                    stressUtilOptions.testContext.Properties[PropNameCurrentIteration] = 0;

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
                        stressUtilOptions.testContext.Properties[PropNameCurrentIteration] = (int)(stressUtilOptions.testContext.Properties[PropNameCurrentIteration]) + 1;
                    }
                    // increment one last time, so test methods can check for final execution after measurements taken
                    stressUtilOptions.testContext.Properties[PropNameCurrentIteration] = (int)(stressUtilOptions.testContext.Properties[PropNameCurrentIteration]) + 1;
                    DisposeTelemetrySession();
                }
            }
            catch (Exception ex)
            {
                stressUtilOptions.logger.LogMessage(ex.ToString());
                DisposeTelemetrySession();
                throw;
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

        public static void SetEnvironmentForMemSpect(IDictionary<string, string> environment, MemSpectModeFlags memSpectModeFlags, string MemSpectDllPath)
        {
            /*
Set COR_ENABLE_PROFILING=1
Set COR_PROFILER={01673DDC-46F5-454F-84BC-F2F34564C2AD}
Set COR_PROFILER_PATH=c:\MemSpect\MemSpectDll.dll
*/
            if (string.IsNullOrEmpty(MemSpectDllPath))
            {
                MemSpectDllPath = @"c:\MemSpect\MemSpectDll.dll"; // @"C:\VS\src\ExternalAPIs\MemSpect\MemSpectDll.dll"
            }
            if (!File.Exists(MemSpectDllPath))
            {
                throw new FileNotFoundException($@"Couldn't find MemSpectDll.Dll at {MemSpectDllPath}. See http://Toolbox/MemSpect and/or VS\src\ExternalAPIs\MemSpect\MemSpectDll.dll");
            }
            environment["COR_ENABLE_PROFILING"] = "1";
            environment["COR_PROFILER"] = "{01673DDC-46F5-454F-84BC-F2F34564C2AD}";
            environment["COR_PROFILER_PATH"] = MemSpectDllPath;
            if (memSpectModeFlags != MemSpectModeFlags.MemSpectModeFull)
            { //todo
                //var MemSpectInitFile = Path.Combine(Path.GetDirectoryName(pathMemSpectDll), "MemSpect.ini");
                // need to WritePrivateProfileString  "TrackClrObjects"  "fTrackHeap" "EnableAsserts"
            }

        }

        private static void PostInitialTelemetry()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);

            Dictionary<string, string> telemetryProperties = new Dictionary<string, string>()
            {
                { "DevDivStress.StressUtil.Initialization.FileVersion",  fileVersionInfo.FileVersion},
            };

            PostTelemetryEvent("DevDivStress/StressUtil/Initialization", telemetryProperties);
        }

        private static void PostTelemetryEvent(string path, Dictionary<string, string> properties)
        {
            if (telemetrySession == null)
            {
                telemetrySession = TelemetryService.DefaultSession;
                telemetrySession.IsOptedIn = true;
                telemetrySession.Start();
            }

            TelemetryEvent telemetryEvent = new TelemetryEvent(path);

            foreach (KeyValuePair<string, string> property in properties)
            {
                telemetryEvent.Properties[property.Key] = property.Value;
            }

            telemetrySession.PostEvent(telemetryEvent);
        }

        private static void DisposeTelemetrySession()
        {
            if (telemetrySession != null)
            {
                telemetrySession.Dispose();
                telemetrySession = null;
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
