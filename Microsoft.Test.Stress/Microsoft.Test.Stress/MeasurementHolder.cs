using Microsoft.VisualStudio.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI;
using System.Windows;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using System.Xaml;
using System.Xml.Linq;

namespace Microsoft.Test.Stress
{
    public enum SampleType
    {
        /// <summary>
        /// sample is taken, no accumulation. If accumulation was in progress, terminate the accumulation (so doesn't show as leak)
        /// </summary>
        SampleTypeNormal,
        /// <summary>
        /// Sample of an iterated test. The iterated samples are accumulated so we can calculate statistics/regression analysis
        /// this will show as small leak (sizeof measurement * numiterations)
        /// The number of these samples is the iteration count so far.
        /// </summary>
        SampleTypeIteration,
    }

    /// <summary>
    /// When a stress test needs to create a dump, these flags indicate what do do
    /// </summary>
    [Flags]
    public enum MemoryAnalysisType
    {
        /// <summary>
        /// Just create the dump. No analysis
        /// </summary>
        JustCreateDump = 0x1,
        /// <summary>
        /// after creating a dump, the ClrObjExplorer WPF app is started with the dump loaded for manual analysis
        /// </summary>
        StartClrObjExplorer = 0x2,
        /// <summary>
        /// the dump is analyzed and type counts are stored in a file
        /// </summary>
        OutputTypeCounts = 0x4,
        /// <summary>
        /// the dump is analyzed and type counts are compared to prior stored results
        /// </summary>
        CompareTypeCounts = 0x8,
        /// <summary>
        /// the PerfCounter measurements are output to a CSV file easily digested by Excel for graphing
        /// </summary>
        OutputMeasurements = 0x10,
    }

    public class FileResultsData
    {
        public string filename;
        public string description;
    }
    public class MeasurementHolder : IDisposable
    {
        public const string InteractiveUser = "InteractiveUser";
        public const string DiffFileName = "String and Type Count differences";
        public string TestName;
        public readonly StressUtilOptions stressUtilOptions;

        /// <summary>
        /// The list of perfcounters to use
        /// </summary>
        public List<PerfCounterData> LstPerfCounterData => stressUtilOptions.lstPerfCountersToUse;
        public ILogger Logger => stressUtilOptions.logger;
        readonly SampleType sampleType;
        public Dictionary<PerfCounterType, List<uint>> measurements = new Dictionary<PerfCounterType, List<uint>>(); // PerfCounterType=> measurements per iteration

        public Dictionary<string, object> dictTelemetryProperties = new Dictionary<string, object>();


        public int nSamplesTaken;

        public string _ResultsFolder;
        /// <summary>
        /// unique folder per test.
        /// </summary>
        public string ResultsFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_ResultsFolder))
                {
                    if (this.testContext == null)
                    { // running from ui: get a clean empty folder
                        var dirMyTemp = DumperViewerMain.EnsureResultsFolderExists();
                        int nIter = 0;
                        string pathResultsFolder;
                        while (true) // we want to let the user have multiple dumps open for comparison
                        {
                            var appendstr = nIter++ == 0 ? string.Empty : nIter.ToString();
                            pathResultsFolder = Path.Combine(
                                dirMyTemp,
                                $"{this.TestName}{appendstr}");
                            if (!Directory.Exists(pathResultsFolder))
                            {
                                Directory.CreateDirectory(pathResultsFolder);
                                break;
                            }
                        }
                        _ResultsFolder = pathResultsFolder;
                    }
                    else
                    {
                        _ResultsFolder = Path.Combine(this.testContext.TestDeploymentDir, this.TestName);
                    }
                    if (!Directory.Exists(ResultsFolder))
                    {
                        Directory.CreateDirectory(ResultsFolder);
                    }
                }
                return _ResultsFolder;
            }
        }
        /// <summary>
        /// The dump taken at NumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot
        /// </summary>
        public string baseDumpFileName;
        /// <summary>
        /// A leak can be detected before all iterations are complete. When >0, this indicates the 1st iteration at which a leak was detected
        /// </summary>
        public int _ReportedMinimumNumberOfIterations = -1;
        private TelemetrySession telemetrySession;

        /// <summary>
        /// can be null when running user compiled code
        /// </summary>
        internal readonly TestContextWrapper testContext;
        public readonly List<FileResultsData> lstFileResults = new List<FileResultsData>();
        public readonly bool IsMeasuringCurrentProcess;

        // The TelemetryService.DefaultSession cannot be used after being disposed. It's static per process. 
        // The telemetry session's lifetime is meant to match the process lifetime, not the test lifetime.
        // The test process may run multiple tests, and a given test doesn't know if it's the last test to run, so it doesn't know to dispse the session
        // The dispose must be called to send the telemetry.
        // So we dispose/recreate the session from serialized settings
        static string SerializedTelemetrySession = string.Empty;
        private int _GoneQuietSamplesTaken = 0;
        private int _IterationsGoneQuiet = 0;
        private List<LeakAnalysisResult> _lstAllLeakResults;
        private DumpAnalyzer _oDumpAnalyzer;
        private readonly DateTime _startTime = DateTime.Now;

        /// <summary>
        /// Same as RPS so uploading results works
        /// </summary>
        public const string _xmlResultFileName = "ConsumptionTempResults.xml";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="TestNameOrTestContext">When running from MSTest, the TestContext can be used to get TestName as well as various other properties. When run from VSix, the compiled code test name
        ///         When run from command line there will be no TestContext</param>
        ///<param name="stressUtilOptions">Set of options to use</param>
        /// <param name="sampleType"></param>
        public MeasurementHolder(object TestNameOrTestContext,
                    StressUtilOptions stressUtilOptions,
                    SampleType sampleType)
        {
            if (TestNameOrTestContext is TestContextWrapper)
            {
                this.testContext = TestNameOrTestContext as TestContextWrapper;
                this.TestName = testContext.TestName;
                this.testContext.Properties[StressUtil.PropNameListFileResults] = lstFileResults;
            }
            else
            {
                this.TestName = TestNameOrTestContext as string;
            }
            this.stressUtilOptions = stressUtilOptions;
            this.sampleType = sampleType;

            foreach (var entry in stressUtilOptions.lstPerfCountersToUse)
            {
                measurements[entry.perfCounterType] = new List<uint>();
            }
            IsMeasuringCurrentProcess = LstPerfCounterData[0].ProcToMonitor.Id == Process.GetCurrentProcess().Id;
        }

        public async Task<string> TakeMeasurementAsync(string desc, bool DoForceGC, bool IsForInteractiveGraph = false)
        {
            if (string.IsNullOrEmpty(desc))
            {
                desc = TestName;
            }
            await Task.Delay(TimeSpan.FromSeconds(stressUtilOptions.SecsBetweenIterations));
            if (DoForceGC)
            {
                await DoForceGCAsync();
            }

            var sBuilderMeasurementResult = new StringBuilder(desc + $" {LstPerfCounterData[0].ProcToMonitor.ProcessName} {LstPerfCounterData[0].ProcToMonitor.Id} ");

            TakeRawMeasurement(sBuilderMeasurementResult, IsForInteractiveGraph);

            if (stressUtilOptions.WaitTilVSQuiet)
            {
                await WaitTilVSQuietAsync();
            }

            if (_ReportedMinimumNumberOfIterations == -1 && nSamplesTaken > 10) // if we haven't reported min yet
            {
                var lstLeaksSoFar = (await CalculateLeaksAsync(showGraph: false, GraphsAsFilePrefix: null));

                if (lstLeaksSoFar.Where(lk => lk.IsLeak).Any())
                {
                    Logger.LogMessage($"Earliest Iteration at which leak detected: {nSamplesTaken}");
                    foreach (var leak in lstLeaksSoFar)
                    {
                        Logger.LogMessage($"    {leak}");
                    }
                    if (testContext != null)
                    {
                        testContext.Properties[StressUtil.PropNameMinimumIteration] = nSamplesTaken; // so unit test can verify
                    }
                    _ReportedMinimumNumberOfIterations = nSamplesTaken;
                    dictTelemetryProperties["MinIterationLeakDetected"] = _ReportedMinimumNumberOfIterations;
                }
            }
            var doCheck = true;
            if (stressUtilOptions.actExecuteAfterEveryIterationAsync != null)
            {
                doCheck = await stressUtilOptions.actExecuteAfterEveryIterationAsync(nSamplesTaken, this);
            }
            if (doCheck)
            {
                await CheckIfNeedToTakeSnapshotsAsync();
            }
            if (stressUtilOptions.FailTestAsifLeaksFound && nSamplesTaken == stressUtilOptions.NumIterations)
            {
                throw new LeakException($"FailTestAsifLeaksFound", null);
            }

            return sBuilderMeasurementResult.ToString();
        }

        public void TakeRawMeasurement(StringBuilder sBuilderMeasurementResult = null, bool IsForInteractiveGraph = false)
        {
            foreach (var ctr in LstPerfCounterData.Where(pctr => IsForInteractiveGraph ? pctr.IsEnabledForGraph : pctr.IsEnabledForMeasurement))
            {
                if (!measurements.TryGetValue(ctr.perfCounterType, out var lst))
                {
                    lst = new List<uint>();
                    measurements[ctr.perfCounterType] = lst;
                }
                var pcValueAsFloat = 0f;
                try
                {
                    pcValueAsFloat = ctr.ReadNextValue();
                }
                catch (InvalidOperationException ex) //From Eventlog:The Open procedure for service ".NETFramework" in DLL "C:\Windows\system32\mscoree.dll" failed with error code 5. Performance data for this service will not be available.
                {
                    string clrFilePath = string.Empty;
                    string clrFileVersion = string.Empty;
                    foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
                    {
                        if (module.ModuleName.Equals("clr.dll", StringComparison.OrdinalIgnoreCase))
                        {
                            clrFilePath = module.FileName;
                            clrFileVersion = module.FileVersionInfo.FileVersion;
                            break;
                        }
                    }
                    Logger.LogMessage($"***EXCEPTION PerfCounter '{ex.Message}' {ctr} ProcId{ctr.ProcToMonitor.Id} StartTime= {ctr.ProcToMonitor.StartTime} HasExited={ctr.ProcToMonitor.HasExited} ClrPath={clrFilePath} ClrVersion={clrFileVersion}");

                }
                uint priorValue = 0;
                if (lst.Count > 0)
                {
                    priorValue = lst[lst.Count - 1];
                    if (this.sampleType != SampleType.SampleTypeIteration)
                    {
                        lst.RemoveAt(0); // we're not iterating, don't accumulate more than 1 (1 for previous)
                    }
                }
                uint pcValue = (uint)pcValueAsFloat;
                int delta = (int)pcValue - (int)priorValue;
                sBuilderMeasurementResult?.Append($"{ctr.PerfCounterName}={pcValue,13:n0}  Δ = {delta,13:n0} ");
                lst.Add(pcValue);
            }
            if (this.sampleType == SampleType.SampleTypeIteration)
            {
                nSamplesTaken++;
            }
        }

        public async Task CheckIfNeedToTakeSnapshotsAsync()
        {
            if (stressUtilOptions.NumIterationsBeforeTotalToTakeBaselineSnapshot > 0 &&
                stressUtilOptions.NumIterations >= stressUtilOptions.NumIterationsBeforeTotalToTakeBaselineSnapshot)
            {
                // if we have enough iterations, lets take a snapshot before they're all done so we can compare: take a baseline snapshot 
                if (nSamplesTaken == stressUtilOptions.NumIterations - stressUtilOptions.NumIterationsBeforeTotalToTakeBaselineSnapshot)
                {
                    baseDumpFileName = await DoCreateDumpAsync($"Taking base snapshot dump at iteration {nSamplesTaken}");
                }
                else if (nSamplesTaken == stressUtilOptions.NumIterations) // final iteration? take snapshot
                {
                    var filenameMeasurementResults = DumpOutMeasurementsToTxtFile();
                    Logger.LogMessage($"Measurement Results {filenameMeasurementResults}");
                    _lstAllLeakResults = (await CalculateLeaksAsync(showGraph: stressUtilOptions.ShowUI, GraphsAsFilePrefix: "Graph"));
                    foreach (var itm in _lstAllLeakResults)
                    {
                        Logger.LogMessage(itm.ToString());
                        dictTelemetryProperties[$"Ctr{itm.perfCounterData.perfCounterType}rsquared"] = itm.RSquared(); // can't use perfcounter name: invalid property name. so use enum name
                        dictTelemetryProperties[$"Ctr{itm.perfCounterData.perfCounterType}slope"] = itm.slope;
                        dictTelemetryProperties[$"Ctr{itm.perfCounterData.perfCounterType}IsLeak"] = itm.IsLeak;
                        dictTelemetryProperties[$"Ctr{itm.perfCounterData.perfCounterType}Threshold"] = itm.perfCounterData.thresholdRegression;
                    }
                    var lstLeakResults = _lstAllLeakResults.Where(r => r.IsLeak).ToList();

                    if (lstLeakResults.Count >= 0 || stressUtilOptions.FailTestAsifLeaksFound)
                    {
                        foreach (var leak in lstLeakResults.Where(p => p.IsLeak))
                        {
                            Logger.LogMessage($"Leak Detected!!!!! {leak}");
                        }
                        if (lstLeakResults.Count == 0 && stressUtilOptions.FailTestAsifLeaksFound)
                        {
                            Logger.LogMessage($"Failing test even though no leaks found so test artifacts can be examined");
                        }
                        var currentDumpFile = await DoCreateDumpAsync($"Taking final snapshot dump at iteration {nSamplesTaken}");
                        if (!string.IsNullOrEmpty(baseDumpFileName))
                        {
                            try
                            {
                                _oDumpAnalyzer = new DumpAnalyzer(Logger);
                                var sb = new StringBuilder();
                                sb.AppendLine($"'{TestName}' Leaks Found");
                                foreach (var leak in lstLeakResults)
                                {
                                    sb.AppendLine($"Leak Detected: {leak}");
                                }
                                sb.AppendLine();
                                _oDumpAnalyzer.GetDiff(sb,
                                                baseDumpFileName,
                                                currentDumpFile,
                                                stressUtilOptions.NumIterations,
                                                stressUtilOptions.NumIterationsBeforeTotalToTakeBaselineSnapshot,
                                                stressUtilOptions.TypesToReportStatisticsOn,
                                                out DumpAnalyzer.TypeStatistics baselineTypeStatistics,
                                                out DumpAnalyzer.TypeStatistics currentTypeStatistics);

                                if (baselineTypeStatistics != null)
                                {
                                    dictTelemetryProperties["TypeStatsExclusiveRetainedBytes_Base"] = baselineTypeStatistics.ExclusiveRetainedBytes;
                                    dictTelemetryProperties["TypeStatsInclusiveRetainedBytes_Base"] = baselineTypeStatistics.InclusiveRetainedBytes;
                                }
                                if (currentTypeStatistics != null)
                                {
                                    dictTelemetryProperties["TypeStatsExclusiveRetainedBytes_Final"] = currentTypeStatistics.ExclusiveRetainedBytes;
                                    dictTelemetryProperties["TypeStatsInclusiveRetainedBytes_Final"] = currentTypeStatistics.InclusiveRetainedBytes;
                                }

                                var fname = Path.Combine(ResultsFolder, $"{DiffFileName}_{nSamplesTaken}.txt");
                                File.WriteAllText(fname, sb.ToString());
                                if (stressUtilOptions.ShowUI)
                                {
                                    Process.Start(fname);
                                }
                                lstFileResults.Add(new FileResultsData() { filename = fname, description = $"Differences for Type and String counts at iter {nSamplesTaken}" });
                                Logger.LogMessage("DumpDiff Analysis " + fname);
                            }
                            catch (FileNotFoundException ex)
                            {
                                Logger.LogMessage($"{ex}");
                                void DumpDir(string dir)
                                {
                                    Logger.LogMessage($"   Dumping folder contents {dir}");
                                    foreach (var file in Directory.EnumerateFiles(dir, "Microsoft.Diagnostics.Runtime.*", SearchOption.AllDirectories))
                                    {
                                        var finfo = new FileInfo(file);
                                        var verinfo = FileVersionInfo.GetVersionInfo(file);
                                        Logger.LogMessage($"  {finfo.Length,20:n0} {verinfo.FileVersion,30}    {file} ");
                                    }
                                }
                                DumpDir(Environment.CurrentDirectory);
                                throw;
                            }
                        }
                        else
                        {
                            Logger.LogMessage($"No baseline dump: not enough iterations");
                        }
                        if (this.testContext != null)
                        {
                            if (lstLeakResults.Count > 0)
                            {
                                throw new LeakException($"Leaks found: " + string.Join(",", lstLeakResults.Select(t => t.perfCounterData.perfCounterType).ToList()), lstLeakResults); //Leaks found: GCBytesInAllHeaps,ProcessorPrivateBytes,ProcessorVirtualBytes,KernelHandleCount
                            }
                        }
                    }
                }
            }
        }

        public async Task WaitTilVSQuietAsync(int circBufferSize = 5, int numTimesToGetQuiet = 50)
        {
            var measurementHolder = this;
            // we want to take measures in a circular buffer and wait til those are quiet
            var quietMeasure = new MeasurementHolder(
                "Quiet",
                new StressUtilOptions()
                {
                    SendTelemetry = false, // we don't want the inner MeasurementHolder to send telemetry
                    NumIterations = 1, // we'll do 1 iteration 
                    pctOutliersToIgnore = 0,
                    logger = measurementHolder.Logger,
                    VSHandler = measurementHolder.stressUtilOptions.VSHandler,
                    lstPerfCountersToUse = measurementHolder.stressUtilOptions.lstPerfCountersToUse,
                }, SampleType.SampleTypeIteration
            );
            // We just took a measurement, so copy those values to init our buffer
            foreach (var pctrMeasure in measurementHolder.measurements.Keys)
            {
                var lastVal = measurementHolder.measurements[pctrMeasure][measurementHolder.nSamplesTaken - 1];
                quietMeasure.measurements[pctrMeasure].Add(lastVal);
            }
            quietMeasure.nSamplesTaken++;

            var isQuiet = false;
            int nMeasurementsForQuiet = 0;
            while (!isQuiet && nMeasurementsForQuiet < numTimesToGetQuiet)
            {
                await quietMeasure.DoForceGCAsync();
                await Task.Delay(TimeSpan.FromSeconds(1 * measurementHolder.stressUtilOptions.DelayMultiplier)); // after GC, wait 1 before taking measurements
                var sb = new StringBuilder($"Measure for Quiet iter = {measurementHolder.nSamplesTaken} QuietSamp#= {nMeasurementsForQuiet}");
                quietMeasure.TakeRawMeasurement(sb);
                //measurementHolder.Logger.LogMessage(sb.ToString());//xxxremove
                if (quietMeasure.nSamplesTaken == circBufferSize)
                {
                    var lk = await quietMeasure.CalculateLeaksAsync(
                        showGraph: false,
                        GraphsAsFilePrefix:
#if DEBUG
                                    "Graph"
#else
                                    null
#endif
                                    );
                    isQuiet = true;
                    foreach (var k in lk.Where(p => !p.IsQuiet()))
                    {
                        //measurementHolder.Logger.LogMessage($"  !quiet {k}"); //xxxremove
                        isQuiet = false;
                    }
                    //                                    isQuiet = !lk.Where(k => !k.IsQuiet()).Any();

                    foreach (var pctrMeasure in quietMeasure.measurements.Keys) // circular buffer: remove 1st item
                    {
                        quietMeasure.measurements[pctrMeasure].RemoveAt(0);
                    }
                    quietMeasure.nSamplesTaken--;
                }
                nMeasurementsForQuiet++;
            }
            this._GoneQuietSamplesTaken += nMeasurementsForQuiet; // for avg calc
            this._IterationsGoneQuiet++; // for total # gone quiet
            if (isQuiet) // the counters have stabilized. We'll use the stabilized numbers as the sample value for the iteration
            {
                measurementHolder.Logger.LogMessage($"Gone quiet in {nMeasurementsForQuiet} measures");
            }
            else
            {
                measurementHolder.Logger.LogMessage($"Didn't go quiet in {numTimesToGetQuiet}");
            }
            // Whether or not it's quiet, we'll take the most recent measure as the iteration sample
            foreach (var pctrMeasure in measurementHolder.measurements.Keys)
            {
                var lastVal = quietMeasure.measurements[pctrMeasure][quietMeasure.nSamplesTaken - 1];
                measurementHolder.measurements[pctrMeasure][measurementHolder.nSamplesTaken - 1] = lastVal;
            }
        }

        public async Task<string> DoCreateDumpAsync(string desc, string filenamepart = "")
        {
            if (stressUtilOptions.SecsDelayBeforeTakingDump > 0)
            {
                Logger.LogMessage($"Delay {stressUtilOptions.SecsDelayBeforeTakingDump} before taking dump at iteration {nSamplesTaken}");
                await Task.Delay(TimeSpan.FromSeconds(stressUtilOptions.SecsDelayBeforeTakingDump));
            }

            Logger.LogMessage(desc);

            var memAnalysisType = MemoryAnalysisType.JustCreateDump;
            if (nSamplesTaken == stressUtilOptions.NumIterations && stressUtilOptions.ShowUI)
            {
                memAnalysisType = MemoryAnalysisType.StartClrObjExplorer;
            }
            var DumpFileName = await CreateDumpAsync(
                LstPerfCounterData[0].ProcToMonitor.Id,
                desc: TestName + filenamepart + "_" + nSamplesTaken.ToString(),
                memoryAnalysisType: memAnalysisType);
            lstFileResults.Add(new FileResultsData() { filename = DumpFileName, description = $"DumpFile taken after iteration # {nSamplesTaken}" });
            return DumpFileName;
        }

        public async Task DoForceGCAsync()
        {
            if (IsMeasuringCurrentProcess)
            {
                const int MAX_CLEANUP_CYCLES = 10;

                // Each time a GC occurs more COM objects can become available for cleanup. 
                // So we keep calling until no more objects are available for cleanup OR 
                // we reach a hard coded limit.
                for (int iLoopCount = 0; iLoopCount < MAX_CLEANUP_CYCLES; ++iLoopCount)
                {
                    GC.Collect(GC.MaxGeneration);
                    GC.WaitForPendingFinalizers();

                    if (!Marshal.AreComObjectsAvailableForCleanup())
                    {
                        break;
                    }
                    Marshal.CleanupUnusedObjectsInCurrentContext();
                }
            }
            else
            {
                // we just finished executing the user code. The IDE might be busy executing the last request.
                // we need to delay some or else System.Runtime.InteropServices.COMException (0x8001010A): The message filter indicated that the application is busy. (Exception from HRESULT: 0x8001010A (RPC_E_SERVERCALL_RETRYLATER))
                //await Task.Delay(TimeSpan.FromSeconds(1 * stressUtilOptions.DelayMultiplier)).ConfigureAwait(false);
                // cmdidShellForceGC GarbageCollectCLRIterative https://devdiv.visualstudio.com/DevDiv/_git/VS?path=%2Fsrc%2Fappid%2FAppDomainManager%2FVsRcwCleanup.cs&version=GBmaster&_a=contents
                await stressUtilOptions.VSHandler.DteExecuteCommandAsync("Tools.ForceGC", TimeoutSecs: 60);
                //await Task.Delay(TimeSpan.FromSeconds(1 * stressUtilOptions.DelayMultiplier)).ConfigureAwait(false);
            }
        }


        //private async Task IfApexTestDelayAsync()
        //{
        //    if (stressUtilOptions.IsTestApexTest())
        //    {
        //        var ApexLeaseDelay = System.Runtime.Remoting.Lifetime.LifetimeServices.LeaseTime + TimeSpan.FromSeconds(10 * stressUtilOptions.DelayMultiplier); // a little more than the lease lifetime
        //        logger.LogMessage($"Apex test: waiting {ApexLeaseDelay.TotalSeconds:n0} seconds for leaselifetime to expire");
        //        await Task.Delay(ApexLeaseDelay);
        //    }
        //    else
        //    {
        //        if (PerfCounterData.ProcToMonitor.Id != Process.GetCurrentProcess().Id)
        //        {
        //            await Task.Delay(TimeSpan.FromSeconds(2 * stressUtilOptions.DelayMultiplier));
        //        }
        //    }
        //}

        /// <summary>
        /// get the counter for graphing
        /// </summary>
        /// <returns></returns>
        public Dictionary<PerfCounterType, uint> GetLastMeasurements()
        {
            var res = new Dictionary<PerfCounterType, uint>();
            foreach (var ctr in LstPerfCounterData.Where(pctr => pctr.IsEnabledForGraph))
            {
                var entry = measurements[ctr.perfCounterType];
                res[ctr.perfCounterType] = entry[entry.Count - 1];
            }
            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="showGraph">show a graph interactively</param>
        /// <param name="GraphsAsFilePrefix">Create graphs as files and test attachments. Null means none</param>
        /// <returns></returns>
        public async Task<List<LeakAnalysisResult>> CalculateLeaksAsync(bool showGraph, string GraphsAsFilePrefix)
        {
            var lstResults = new List<LeakAnalysisResult>();
            this.stressUtilOptions.SetPerfCounterOverrideSettings();
            foreach (var ctr in LstPerfCounterData.Where(pctr => pctr.IsEnabledForMeasurement))
            {
                var leakAnalysis = new LeakAnalysisResult(measurements[ctr.perfCounterType])
                {
                    perfCounterData = ctr,
                    sensitivity = stressUtilOptions.Sensitivity,
                    pctOutliersToIgnore = stressUtilOptions.pctOutliersToIgnore,
                    RSquaredThreshold = stressUtilOptions.RSquaredThreshold
                };
                leakAnalysis.FindLinearLeastSquaresFit();

                lstResults.Add(leakAnalysis);
            }
            if (showGraph)
            {
                var tcs = new TaskCompletionSource<int>();
                // if we're running in testhost process, then we want a timeout
                var timeoutEnabled = this.testContext != null;
                var timeout = TimeSpan.FromSeconds(60);
                Logger.LogMessage($"Showing graph  timeoutenabled ={timeoutEnabled} {timeout.TotalSeconds:n0} secs");
                var thr = new Thread((oparam) =>
                {
                    try
                    {
                        var graphWin = new GraphWin(this);
                        if (timeoutEnabled)
                        {
                            var timer = new DispatcherTimer()
                            {
                                Interval = timeout
                            };
                            timer.Tick += (o, e) =>
                            {
                                Logger.LogMessage($"Timedout showing graph");
                                timer.Stop();
                                graphWin.Close();
                            };
                            timer.Start();
                        }
                        graphWin.AddGraph(lstResults);
                        if (timeoutEnabled)
                        {
                            graphWin.Title += $" timeout {timeout.TotalSeconds:n0}";
                        }
                        graphWin.ShowDialog();
                        Logger.LogMessage($"finished showing graph");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogMessage($"graph {ex}");
                    }
                    tcs.SetResult(0);
                });
                thr.SetApartmentState(ApartmentState.STA);
                thr.Start();
                await tcs.Task;
                //if (await Task.WhenAny(tcs.Task, Task.Delay(timeout)) != tcs.Task)
                //{
                //    Logger.LogMessage($"Timedout showing graph");
                //}
            }
            // Create graphs as files. do this after showgraph else hang
            if (!string.IsNullOrEmpty(GraphsAsFilePrefix))
            {
                foreach (var item in lstResults)
                {
                    using (var chart = new Chart())
                    {
                        chart.Titles.Add($"{TestName} {item}");
                        chart.Size = new System.Drawing.Size(1200, 800);
                        chart.Series.Clear();
                        chart.ChartAreas.Clear();
                        ChartArea chartArea = new ChartArea("ChartArea");
                        chartArea.AxisY.LabelStyle.Format = "{0:n0}";
                        chartArea.AxisY.Title = item.perfCounterData.PerfCounterName;
                        chartArea.AxisX.Title = "Iteration";
                        chartArea.AxisY.LabelStyle.Font = new System.Drawing.Font("Consolas", 12);
                        chart.ChartAreas.Add(chartArea);
                        chartArea.AxisY.IsStartedFromZero = false;
                        var series = new Series
                        {
                            ChartType = SeriesChartType.Line,
                            Name = item.perfCounterData.PerfCounterName
                        };
                        series.MarkerSize = 10;
                        series.MarkerStyle = MarkerStyle.Circle;
                        chart.Series.Add(series);
                        for (int i = 0; i < item.NumSamplesToUse; i++)
                        {
                            var dp = new DataPoint(i + 1, item.lstData[i].point.Y); // measurements taken at end of iteration
                            if (item.lstData[i].IsOutlier)
                            {
                                dp.MarkerColor = System.Drawing.Color.Red;
                                dp.MarkerStyle = MarkerStyle.Cross;
                            }
                            series.Points.Add(dp);
                        }
                        // now show trend line
                        var seriesTrendLine = new Series()
                        {
                            ChartType = SeriesChartType.Line,
                            Name = "Trend Line"
                        };
                        chart.Series.Add(seriesTrendLine);
                        var dp0 = new DataPoint(1, item.yintercept + item.slope);
                        seriesTrendLine.Points.Add(dp0);
                        var dp1 = new DataPoint(item.NumSamplesToUse, item.NumSamplesToUse * item.slope + item.yintercept);
                        seriesTrendLine.Points.Add(dp1);

                        chart.Legends.Add(new Legend());
                        chart.Legends[0].CustomItems.Add(new LegendItem()
                        {
                            Name = "IsOutlier",
                            ImageStyle = LegendImageStyle.Marker,
                            MarkerColor = System.Drawing.Color.Red,
                            MarkerStyle = MarkerStyle.Cross,
                            MarkerBorderWidth = 0,
                            MarkerSize = 10
                        });

                        var fname = Path.Combine(ResultsFolder, $"{GraphsAsFilePrefix} {item.perfCounterData.PerfCounterName}.png");
                        chart.SaveImage(fname, ChartImageFormat.Png);
                        lstFileResults.Add(new FileResultsData() { filename = fname, description = $"{GraphsAsFilePrefix} {item.perfCounterData}" });
                    }
                }
            }
            return lstResults;
        }

        public string DumpOutMeasurementsToTxtFile()
        {
            var sb = new StringBuilder();
            var lst = new List<string>();
            foreach (var ctr in LstPerfCounterData.Where(pctr => pctr.IsEnabledForMeasurement))
            {
                lst.Add(ctr.PerfCounterName);
            }
            sb.AppendLine(string.Join(",", lst.ToArray()));

            for (int i = 0; i < nSamplesTaken; i++)
            {
                lst.Clear();
                foreach (var ctr in LstPerfCounterData.Where(pctr => pctr.IsEnabledForMeasurement))
                {
                    if (i < measurements[ctr.perfCounterType].Count)
                    {
                        lst.Add($"{measurements[ctr.perfCounterType][i]}");
                    }
                    else
                    {
                        Logger.LogMessage($"Index out of range {ctr.PerfCounterName}  {i}  {measurements[ctr.perfCounterType].Count}");
                    }
                }
                sb.AppendLine(string.Join(",", lst.ToArray()));
            }
            var filename = Path.Combine(ResultsFolder, $"Measurements.txt");
            File.WriteAllText(filename, sb.ToString());
            lstFileResults.Add(new FileResultsData() { filename = filename, description = "Raw Measuremensts as Txt File to open/graph in Excel" });
            return filename;
        }

        public async Task<string> CreateDumpAsync(int pid, MemoryAnalysisType memoryAnalysisType, string desc)
        {
            var pathDumpFile = DumperViewerMain.CreateNewFileName(ResultsFolder, desc);
            try
            {
                await DoForceGCAsync();
                var arglist = new List<string>()
                    {
                        "-p", pid.ToString(),
                        "-f",  "\"" + pathDumpFile + "\""
                    };
                if (memoryAnalysisType.HasFlag(MemoryAnalysisType.StartClrObjExplorer))
                {
                    arglist.Add("-c");
                }
                var odumper = new DumperViewerMain(arglist.ToArray())
                {
                    _logger = Logger
                };
                await odumper.DoitAsync();
            }
            catch (Exception ex)
            {
                Logger.LogMessage(ex.ToString());
            }
            return pathDumpFile;
        }

        public override string ToString()
        {
            return $"{TestName} #Samples={nSamplesTaken}";
        }

        public void Dispose()
        {
#if false


From: Brad White <brad.white@microsoft.com> 
Sent: Wednesday, February 5, 2020 6:08 PM
To: Calvin Hsia <calvinh@microsoft.com>
Subject: RE: Get Run ID from test

	From within an Apex/DTE test how do I get something like “Release-800” from the screenshot below?
I would advise against adding dependencies on Azure DevOps to test code. 

We push our telemetry in one of two ways:
1.	Service hook that calls an Azure Function
2.	Script in the release that runs after test execution

For you, I’d recommend #2. Add a script that runs after the tests complete. To get the run ID, you can use $(testrunid), which gets set by the Visual Studio Test task after the run has completed.

#endif

            var duration = (DateTime.Now - _startTime);
            var secsPerIteration = duration.TotalSeconds / stressUtilOptions.NumIterations;
            Logger.LogMessage($"Number of Seconds/Iteration = {secsPerIteration:n1}");
            dictTelemetryProperties["IterationsPerSecond"] = secsPerIteration;
            dictTelemetryProperties["Duration"] = duration.TotalSeconds;

            dictTelemetryProperties["GoneQuietAvg"] = (double)this._GoneQuietSamplesTaken / stressUtilOptions.NumIterations;
            dictTelemetryProperties["IterationsGoneQuiet"] = this._IterationsGoneQuiet;
            dictTelemetryProperties["NumIterations"] = stressUtilOptions.NumIterations;
            dictTelemetryProperties["TestName"] = this.TestName;
            dictTelemetryProperties["MachineName"] = Environment.GetEnvironmentVariable("COMPUTERNAME");
            dictTelemetryProperties["TargetProcessName"] = Path.GetFileNameWithoutExtension(LstPerfCounterData[0].ProcToMonitor.MainModule.FileName);
            var fileVersion = LstPerfCounterData[0].ProcToMonitor.MainModule.FileVersionInfo.FileVersion;
            var lastSpace = fileVersion.LastIndexOf(" ");
            var branchName = string.Empty;
            if (lastSpace > 0)
            {
                branchName = fileVersion.Substring(lastSpace + 1);
            }
            dictTelemetryProperties["TargetProcessVersion"] = fileVersion;
            dictTelemetryProperties["BranchName"] = branchName;
            if (stressUtilOptions.TypesToReportStatisticsOn != null)
            {
                dictTelemetryProperties["TypesToReportStatisticsOn"] = stressUtilOptions.TypesToReportStatisticsOn;
            }

            //            WriteResultsToXML(Path.Combine(ResultsFolder, _xmlResultFileName));
            if (this.testContext != null)
            {
                if (Logger is Logger myLogger)
                {
                    var sb = new StringBuilder();
                    foreach (var str in myLogger._lstLoggedStrings)
                    {
                        sb.AppendLine(str);
                    }
                    var filename = Path.Combine(ResultsFolder, $"StressTestLog.log");
                    File.WriteAllText(filename, sb.ToString());
                    lstFileResults.Add(new FileResultsData() { filename = filename, description = "Stress Test Log" });
                }
                foreach (var fileresult in lstFileResults)
                {
                    this.testContext.AddResultFile(fileresult.filename);
                }
            }
            if (stressUtilOptions.SendTelemetry)
            {
                try
                {
                    PostTelemetryEvent("devdivstress/stresslib/leakresult", dictTelemetryProperties);
                    if (Process.GetCurrentProcess().ProcessName != "devenv") // if we're not running as a VSIX
                    {
                        if (telemetrySession != null)
                        {
                            telemetrySession.Dispose(); // System.IO.FileNotFoundException: Could not load file or assembly 'Newtonsoft.Json, Version=13.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed' or one of its dependencies. The system cannot find the file specified.
                            telemetrySession = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogMessage($"Exception sending telemetry\n{ex}");
                }
            }
        }
        public void WriteResultsToXML(string outputXMLFile)
        {
            try
            {
                XElement xmlDom = new XElement("StressResults");
                xmlDom.Add(new XAttribute("TestName", this.TestName));
                xmlDom.Add(new XAttribute("TargetProcessName", (string)(dictTelemetryProperties["TargetProcessName"])));
                xmlDom.Add(new XAttribute("TargetProcessVersion", (string)(dictTelemetryProperties["TargetProcessVersion"])));
                xmlDom.Add(new XAttribute("BranchName", (string)(dictTelemetryProperties["BranchName"])));
                xmlDom.Add(new XAttribute("NumIterations", stressUtilOptions.NumIterations));
                xmlDom.Add(new XAttribute("Duration", ((double)(dictTelemetryProperties["Duration"])).ToString("0.00")));
                xmlDom.Add(new XAttribute("GoneQuietAvg", ((double)(dictTelemetryProperties["GoneQuietAvg"])).ToString("0.00")));
                xmlDom.Add(new XAttribute("IterationsGoneQuiet", ((int)dictTelemetryProperties["IterationsGoneQuiet"]).ToString("0")));

                foreach (var result in _lstAllLeakResults)
                {
                    var xmlResult = new XElement("LeakResult");
                    xmlResult.Add(new XAttribute("Name", result.perfCounterData.perfCounterType.ToString()));// use enum (with no embedded spaces) rather than name
                    var fmt = (result.perfCounterData.PerfCounterName.IndexOf("ytes") > 0) ? "0" : "0.000";
                    xmlResult.Add(new XAttribute("Slope", result.slope.ToString(fmt)));
                    xmlResult.Add(new XAttribute("RSquared", result.RSquared().ToString("0.000")));
                    xmlResult.Add(new XAttribute("IsLeak", result.IsLeak));
                    xmlResult.Add(new XAttribute("Threshold", result.perfCounterData.thresholdRegression.ToString(fmt)));
                    xmlDom.Add(xmlResult);
                }

                var measResult = new XElement("MeasurementResult");
                xmlDom.Add(measResult);
                foreach (var kvp in measurements)
                {
                    var measnode = new XElement(kvp.Key.ToString());
                    measResult.Add(measnode);
                    foreach (var val in kvp.Value)
                    {
                        measnode.Add(new XElement("Value", val));
                    }
                }
                if (_oDumpAnalyzer != null)
                {
                    void AddDiffNode(string kind, Dictionary<string, Tuple<int, int>> dict)
                    {
                        var node = new XElement(kind + "s");
                        xmlDom.Add(node);
                        foreach (var kvp in dict)
                        {
                            var child = new XElement(kind, kvp.Key);// + @"</\\test>"
                            child.Add(new XAttribute("BaseCnt", kvp.Value.Item1));
                            child.Add(new XAttribute("CurrCnt", kvp.Value.Item2));
                            node.Add(child);
                        }
                    }
                    AddDiffNode("Type", _oDumpAnalyzer._dictTypeDiffs);
                    AddDiffNode("String", _oDumpAnalyzer._dictStringDiffs);
                }

                xmlDom.Save(outputXMLFile);
                if (stressUtilOptions.ShowUI)
                {
                    Process.Start(outputXMLFile);
                }
                lstFileResults.Add(new FileResultsData() { filename = outputXMLFile, description = "XML Results" });
            }
            catch (Exception ex)
            {
                Logger.LogMessage($"Exception writing XML file results {outputXMLFile} " + ex.ToString());
            }
        }

        public void PostTelemetryEvent(string telemetryEventName, Dictionary<string, object> telemetryProperties)
        {
            if (telemetrySession == null)
            {
                if (Process.GetCurrentProcess().ProcessName == "devenv")
                {
                    telemetrySession = TelemetryService.DefaultSession;
                }
                else
                {
                    if (string.IsNullOrEmpty(SerializedTelemetrySession))
                    {
                        telemetrySession = TelemetryService.DefaultSession;
                        telemetrySession.IsOptedIn = true;
                        telemetrySession.Start();
                        SerializedTelemetrySession = telemetrySession.SerializeSettings();
                    }
                    else
                    {
                        telemetrySession = new TelemetrySession(SerializedTelemetrySession);
                        telemetrySession.Start();
                    }
                }
            }
            var prefix = telemetryEventName.Replace("/", ".") + ".";

            TelemetryEvent telemetryEvent = new TelemetryEvent(telemetryEventName);

            foreach (var property in telemetryProperties)
            {
                telemetryEvent.Properties[prefix + property.Key] = property.Value;
            }

            telemetrySession.PostEvent(telemetryEvent);
        }


    }
    public struct PointF
    {
        public double X;
        public double Y;
        public override string ToString()
        {
            return $"({X:n1},{Y:n1})";
        }
    }
}
