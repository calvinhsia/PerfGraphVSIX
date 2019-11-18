﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StressTestUtility
{
    public class StressUtil
    {
        public const string PropNameVSHandler = "VSHandler";
        public const string PropNameIteration = "IterationNumber";
        /// <summary>
        /// Do it all: tests need only add a single call to turn the test into a stress test
        /// The call can be made from the TestInitialize or the beginning of the TestMethod
        /// </summary>
        /// <param name="test">pass the test itself</param>
        /// <param name="NumIterations">Specify the number of iterations to do. Some scenarios are large (open/close solution)
        /// Others are small (scroll up/down a file)</param>
        /// <param name="Sensitivity">Defaults to 1 (multiplier). Some perf counter thresholds are large (e.g. 1 megabyte for Private bytes). To find
        /// smaller leaks, set this to .1. If too sensitive, set this to 10 (for 10 Meg threshold) </param>
        /// <param name="DelayMultiplier">Defaults to 1. All delays (e.g. between iterations, after GC, are multiplied by this factor.
        /// Running the test with instrumented binaries (such as under http://Toolbox/MemSpect  will slow down the target process</param>
        /// <param name="ProcNamesToMonitor">'|' separated list of processes to monitor VS, use 'devenv' To monitor the current process, use ''. </param>
        /// <param name="ShowUI">Show results automatically, like the Graph of Measurements, the Dump in ClrObjExplorer, the Diff Analysis</param>
        /// <param name="NumIterationsBeforeTotalToTakeBaselineSnapshot"> Specifies the iteration # at which to take a baseline. 
        ///    <paramref name="NumIterationsBeforeTotalToTakeBaselineSnapshot"/> is subtracted from <paramref name="NumIterations"/> to get the baseline iteration number
        /// e.g. 100 iterations, with <paramref name="NumIterationsBeforeTotalToTakeBaselineSnapshot"/>=4 means take a baseline at iteartion 100-4==96;
        /// </param>
        public static async Task DoIterationsAsync(
            object test,
            int NumIterations = 7,
            double Sensitivity = 1.0f,
            int DelayMultiplier = 1,
            string ProcNamesToMonitor = "devenv",
            bool ShowUI = false,
            int NumIterationsBeforeTotalToTakeBaselineSnapshot = 4)
        {
            const string PropNameRecursionPrevention = "RecursionPrevention";
            ILogger logger = test as ILogger;
            try
            {
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
                logger.LogMessage($@"{
                    nameof(DoIterationsAsync)} TestName = {testContext.TestName} 
                                        NumIterations = {NumIterations}
                                        Sensitivity = {Sensitivity}
                                        CurDir = '{Environment.CurrentDirectory}'
                                        TestDeploymentDir = '{testContext.TestDeploymentDir}'
                                        TestRunDirectory = '{testContext.TestRunDirectory}'  
                                        TestResultsDirectory='{testContext.TestResultsDirectory}' 
                                        TestRunResultsDirectory='{testContext.TestRunResultsDirectory}'
                ");
                /*
                 * probs: the curdir is not empty, so results will be overwritten (might have ClrObjExplorer open with a result dump)
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
                        vSHandler = testContext.Properties[PropNameVSHandler] as VSHandler;
                        if (vSHandler == null)
                        {
                            vSHandler = new VSHandler(logger, DelayMultiplier);
                            testContext.Properties[PropNameVSHandler] = vSHandler;
                        }
                    }
                    await vSHandler?.EnsureGotDTE(); // ensure we get the DTE. Even for Apex tests, we need to Tools.ForceGC
                }

                using (var measurementHolder = new MeasurementHolder(
                    testContext,
                    PerfCounterData._lstPerfCounterDefinitionsForStressTest,
                    SampleType.SampleTypeIteration,
                    logger: logger,
                    sensitivity: Sensitivity))
                {
                    var baseDumpFileName = string.Empty;
                    testContext.Properties[PropNameIteration] = 0;

                    for (int iteration = 0; iteration < NumIterations; iteration++)
                    {
                        var result = _theTestMethod.Invoke(test, parameters: null);
                        if (_theTestMethod.ReturnType.Name == "Task")
                        {
                            var resultTask = (Task)result;
                            await resultTask;
                        }
                        if (PerfCounterData.ProcToMonitor.Id == Process.GetCurrentProcess().Id)
                        {
                            GC.Collect();
                        }
                        else
                        {
                            // we just finished executing the user code. The IDE might be busy executing the last request.
                            // we need to delay some or else System.Runtime.InteropServices.COMException (0x8001010A): The message filter indicated that the application is busy. (Exception from HRESULT: 0x8001010A (RPC_E_SERVERCALL_RETRYLATER))
                            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                            vSHandler?.DteExecuteCommand("Tools.ForceGC");
                            await Task.Delay(TimeSpan.FromSeconds(1 * DelayMultiplier)).ConfigureAwait(false);
                        }

                        var res = measurementHolder.TakeMeasurement($"Iter {iteration + 1,3}/{NumIterations}");
                        logger.LogMessage(res);
                        // if we have enough iterations, lets take a snapshot before they're all done so we can compare
                        if (iteration == NumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot - 1)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(5 * DelayMultiplier)).ConfigureAwait(false);
                            logger.LogMessage($"Taking base snapshot dump");
                            baseDumpFileName = await measurementHolder.CreateDumpAsync(
                                PerfCounterData.ProcToMonitor.Id,
                                desc: testContext.TestName + "_" + iteration.ToString(),
                                memoryAnalysisType: MemoryAnalysisType.JustCreateDump);
                            testContext?.AddResultFile(baseDumpFileName);
                        }
                        testContext.Properties[PropNameIteration] = (int)(testContext.Properties[PropNameIteration]) + 1;
                    }
                    if (NumIterations > 2) // don't want to do leak analysis unless enough iterations
                    {
                        var filenameResultsCSV = measurementHolder.DumpOutMeasurementsToCsv();
                        logger.LogMessage($"Measurement Results {filenameResultsCSV}");
                        var lstLeakResults = (await measurementHolder.CalculateLeaksAsync(showGraph: ShowUI))
                            .Where(r => r.IsLeak).ToList();
                        if (lstLeakResults.Count > 0)
                        {
                            foreach (var leak in lstLeakResults)
                            {
                                logger.LogMessage($"Leak Detected!!!!! {leak}");
                            }
                            var currentDumpFile = await measurementHolder.CreateDumpAsync(
                                PerfCounterData.ProcToMonitor.Id,
                                desc: testContext.TestName + "_" + NumIterations.ToString(),
                                memoryAnalysisType: ShowUI? MemoryAnalysisType.StartClrObjExplorer: MemoryAnalysisType.JustCreateDump);
                            testContext?.AddResultFile(currentDumpFile);
                            if (!string.IsNullOrEmpty(baseDumpFileName))
                            {
                                var oDumpAnalyzer = new DumpAnalyzer(logger);
                                var sb = oDumpAnalyzer.GetDiff(baseDumpFileName,
                                                currentDumpFile,
                                                NumIterations,
                                                NumIterationsBeforeTotalToTakeBaselineSnapshot);
                                var fname = Path.Combine(measurementHolder.ResultsFolder, "DumpDiff Analysis.txt");
                                File.WriteAllText(fname, sb.ToString());
                                if (ShowUI)
                                {
                                    Process.Start(fname);
                                }
                                testContext?.AddResultFile(fname);
                                logger.LogMessage("DumpDiff Analysis" + fname);
                            }
                            else
                            {
                                logger.LogMessage($"No baseline dump: not enough iterations");
                            }
                            throw new LeakException($"Leaks found\r\n", lstLeakResults);
                        }
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