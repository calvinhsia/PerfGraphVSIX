using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI;
using System.Windows;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using System.Windows.Threading;

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

        /// <summary>
        /// can be null when running user compiled code
        /// </summary>
        readonly TestContextWrapper testContext;
        public readonly List<FileResultsData> lstFileResults = new List<FileResultsData>();

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
        }

        public async Task<string> TakeMeasurementAsync(string desc, bool IsForInteractiveGraph = false)
        {
            if (string.IsNullOrEmpty(desc))
            {
                desc = TestName;
            }
            await Task.Delay(TimeSpan.FromSeconds(stressUtilOptions.SecsBetweenIterations));

            if (LstPerfCounterData[0].ProcToMonitor.Id == Process.GetCurrentProcess().Id)
            {
                GC.Collect();
            }
            else
            {
                // we just finished executing the user code. The IDE might be busy executing the last request.
                // we need to delay some or else System.Runtime.InteropServices.COMException (0x8001010A): The message filter indicated that the application is busy. (Exception from HRESULT: 0x8001010A (RPC_E_SERVERCALL_RETRYLATER))
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                await DoForceGCAsync();
            }

            var sBuilderMeasurementResult = new StringBuilder(desc + $" {LstPerfCounterData[0].ProcToMonitor.ProcessName} {LstPerfCounterData[0].ProcToMonitor.Id} ");

            TakeRawMeasurement(sBuilderMeasurementResult, IsForInteractiveGraph);


            if (_ReportedMinimumNumberOfIterations == -1 && nSamplesTaken > 10) // if we haven't reported min yet
            {
                var lstLeaksSoFar = (await CalculateLeaksAsync(showGraph: false, CreateGraphsAsFiles: false)).Where(r => r.IsLeak);
                if (lstLeaksSoFar.Any())
                {
                    Logger.LogMessage($"Earliest Iteration at which leak detected: {nSamplesTaken}");
                    foreach (var leak in lstLeaksSoFar)
                    {
                        Logger.LogMessage($"    {leak}");
                    }
                    testContext.Properties[StressUtil.PropNameMinimumIteration] = nSamplesTaken; // so unit test can verify
                    _ReportedMinimumNumberOfIterations = nSamplesTaken;
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
                var pcValueAsFloat = ctr.ReadNextValue();
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
            if (stressUtilOptions.NumIterations >= stressUtilOptions.NumIterationsBeforeTotalToTakeBaselineSnapshot)
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
                    var lstLeakResults = (await CalculateLeaksAsync(showGraph: stressUtilOptions.ShowUI, CreateGraphsAsFiles: true))
                        .Where(r => r.IsLeak).ToList();
                    if (lstLeakResults.Count > 0 || stressUtilOptions.FailTestAsifLeaksFound)
                    {
                        foreach (var leak in lstLeakResults)
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
                            var oDumpAnalyzer = new DumpAnalyzer(Logger);
                            var sb = new StringBuilder();
                            sb.AppendLine($"'{TestName}' Leaks Found");
                            foreach (var leak in lstLeakResults)
                            {
                                sb.AppendLine($"Leak Detected: {leak}");
                            }
                            sb.AppendLine();
                            oDumpAnalyzer.GetDiff(sb,
                                            baseDumpFileName,
                                            currentDumpFile,
                                            stressUtilOptions.NumIterations,
                                            stressUtilOptions.NumIterationsBeforeTotalToTakeBaselineSnapshot);
                            var fname = Path.Combine(ResultsFolder, $"{DiffFileName}_{nSamplesTaken}.txt");
                            File.WriteAllText(fname, sb.ToString());
                            if (stressUtilOptions.ShowUI)
                            {
                                Process.Start(fname);
                            }
                            lstFileResults.Add(new FileResultsData() { filename = fname, description = $"Differences for Type and String counts at iter {nSamplesTaken}" });
                            Logger.LogMessage("DumpDiff Analysis " + fname);
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

        public async Task<string> DoCreateDumpAsync(string desc, string filenamepart = "")
        {
            if (stressUtilOptions.SecsDelayBeforeTakingDump > 0)
            {
                Logger.LogMessage($"Delay {stressUtilOptions.SecsDelayBeforeTakingDump} before taking dump at iteration {nSamplesTaken}");
                await Task.Delay(TimeSpan.FromSeconds(stressUtilOptions.SecsDelayBeforeTakingDump));
                await DoForceGCAsync();
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
            // cmdidShellForceGC GarbageCollectCLRIterative https://devdiv.visualstudio.com/DevDiv/_git/VS?path=%2Fsrc%2Fappid%2FAppDomainManager%2FVsRcwCleanup.cs&version=GBmaster&_a=contents
            stressUtilOptions.VSHandler?.DteExecuteCommand("Tools.ForceGC");
            await Task.Delay(TimeSpan.FromSeconds(1 * stressUtilOptions.DelayMultiplier)).ConfigureAwait(false);
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
        /// <param name="CreateGraphsAsFiles">Create graphs as files and test attachments</param>
        /// <returns></returns>
        public async Task<List<LeakAnalysisResult>> CalculateLeaksAsync(bool showGraph, bool CreateGraphsAsFiles)
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
                    RSquaredThreashold = stressUtilOptions.RSquaredThreshold
                };
                leakAnalysis.FindLinearLeastSquaresFit();

                Logger.LogMessage($"{leakAnalysis}");
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
            if (CreateGraphsAsFiles)
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

                        var fname = Path.Combine(ResultsFolder, $"Graph {item.perfCounterData.PerfCounterName}.png");
                        chart.SaveImage(fname, ChartImageFormat.Png);
                        lstFileResults.Add(new FileResultsData() { filename = fname, description = $"Graph {item.perfCounterData}" });
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
                //var sbHtml = new StringBuilder("");
                foreach (var fileresult in lstFileResults)
                {
                    this.testContext.AddResultFile(fileresult.filename);
                    //switch (Path.GetExtension(fileresult.filename))
                    //{
                    //    case ".dmp":
                    //        //            var strHtml = @"
                    //        //<a href=""file://C:/Users/calvinh/Source/repos/PerfGraphVSIX/TestResults/Deploy_calvinh 2019-11-19 11_00_13/Out/TestMeasureRegressionVerifyGraph/Graph Handle Count.png"">gr </a>
                    //        //            ";
                    //        //            var fileHtml = Path.Combine(resultsFolder, "Index.html");
                    //        //            File.WriteAllText(fileHtml, strHtml);
                    //        //            TestContext.AddResultFile(fileHtml);
                    //        sbHtml.AppendLine($@"<p><a href=""file://{DumpAnalyzer.GetClrObjExplorerPath()} -m {fileresult.filename}"">Start ClrObjExplorer with dump {Path.GetFileName(fileresult.filename)} </a>");
                    //        break;
                    //    default:
                    //        sbHtml.AppendLine($@"<p><a href=""file://{fileresult.filename}"">{Path.GetFileName(fileresult.filename)}</a>");
                    //        break;
                    //}

                }
                //var filenameHtml = Path.Combine(ResultsFolder, "Index.html");
                //File.WriteAllText(filenameHtml, sbHtml.ToString());
                //this.testContext.AddResultFile(filenameHtml);
            }
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
