using DumperViewer;
using LeakTestDatacollector;
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

namespace PerfGraphVSIX
{
    public class StressUtil
    {
        public const string PropNameVSHandler = "VSHandler";
        public const string PropNameiteration = "IterationNumber";
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
                 * probs: the curdir is not empty, so results will be overwritten (might have ClrObjectExplorer open with a result dump)
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

                var measurementHolder = new MeasurementHolder(
                    testContext,
                    PerfCounterData._lstPerfCounterDefinitionsForStressTest,
                    SampleType.SampleTypeIteration,
                    logger: logger,
                    sensitivity: Sensitivity);

                var baseDumpFileName = string.Empty;
                testContext.Properties[PropNameiteration] = 0;

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

                    var res = measurementHolder.TakeMeasurement($"Iter {iteration + 1}/{NumIterations}");
                    logger.LogMessage(res);

                    if (NumIterations > NumIterationsBeforeTotalToTakeBaselineSnapshot &&
                        iteration == NumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot - 1)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5 * DelayMultiplier)).ConfigureAwait(false);
                        logger.LogMessage($"Taking base snapshot dump");
                        baseDumpFileName = await measurementHolder.CreateDumpAsync(
                            PerfCounterData.ProcToMonitor.Id,
                            desc: testContext.TestName + "_" + iteration.ToString(),
                            memoryAnalysisType: MemoryAnalysisType.JustCreateDump);
                    }
                    testContext.Properties[PropNameiteration] = (int)(testContext.Properties[PropNameiteration]) + 1;
                }
                if (NumIterations > 2) // don't want to do leak analysis unless enough iterations
                {
                    var filenameResultsCSV = measurementHolder.DumpOutMeasurementsToCsv();
                    logger.LogMessage($"Measurement Results {filenameResultsCSV}");
                    var lstRegResults = (await measurementHolder.CalculateRegressionAsync(showGraph: true))
                        .Where(r => r.IsRegression).ToList();
                    if (lstRegResults.Count > 0)
                    {
                        foreach (var regres in lstRegResults)
                        {
                            logger.LogMessage($"Regression!!!!! {regres}");
                        }
                        var currentDumpFile = await measurementHolder.CreateDumpAsync(
                            PerfCounterData.ProcToMonitor.Id,
                            desc: testContext.TestName + "_" + NumIterations.ToString(),
                            memoryAnalysisType: MemoryAnalysisType.StartClrObjectExplorer);
                        if (!string.IsNullOrEmpty(baseDumpFileName))
                        {
                            var oDumpAnalyzer = new DumperViewer.DumpAnalyzer(logger);
                            var sb = oDumpAnalyzer.GetDiff(baseDumpFileName,
                                            currentDumpFile,
                                            NumIterations,
                                            NumIterationsBeforeTotalToTakeBaselineSnapshot);
                            var fname = Path.Combine(measurementHolder.ResultsFolder, "DumpDiff Analysis.txt");
                            File.WriteAllText(fname, sb.ToString());
                            Process.Start(fname);
                            logger.LogMessage("DumpDiff Analysis" + fname);
                        }
                        else
                        {
                            logger.LogMessage($"No baseline dump: not enough iterations");
                        }
                        throw new LeakException($"Leaks found\r\n", lstRegResults);
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
        public List<RegressionAnalysis> lstRegResults;

        public LeakException(string message, List<RegressionAnalysis> lstRegResults) : base(message)
        {
            this.lstRegResults = lstRegResults;
        }
    }

    public class Logger : ILogger
    {
        public static List<string> _lstLoggedStrings = new List<string>();
        private readonly TestContextWrapper testContext;
        private string logFilePath;

        public Logger(TestContextWrapper testContext)
        {
            this.testContext = testContext;
        }

        public void LogMessage(string str, params object[] args)
        {
            try
            {
                var dt = string.Format("[{0}],",
                DateTime.Now.ToString("hh:mm:ss:fff")
                );
                str = string.Format(dt + str, args);
                var msgstr = DateTime.Now.ToString("hh:mm:ss:fff") + $" {Thread.CurrentThread.ManagedThreadId,2} {str}";

                testContext.WriteLine(msgstr);
                if (Debugger.IsAttached)
                {
                    Debug.WriteLine(msgstr);
                }
                _lstLoggedStrings.Add(msgstr);

                if (string.IsNullOrEmpty(logFilePath))
                {
                    //   logFilePath = @"c:\Test\StressDataCollector.log";
                    logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "TestStressDataCollector.log"); //can't use the Test deployment folder because it gets cleaned up
                }
                File.AppendAllText(logFilePath, msgstr + Environment.NewLine);
            }
            catch (Exception)
            {
            }

        }
    }
}
