﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    public class LeakAnalysisResult
    {
        public class DataPoint
        {
            public PointF point;
            public bool IsOutlier;
            internal double distance; // distance from line
            public override string ToString()
            {
                return $"{point} IsOutlier={IsOutlier}";
            }
        }
        public PerfCounterData perfCounterData;
        public List<DataPoint> lstData = new List<DataPoint>();
        public double sensitivity;
        public double rmsError;
        public int pctOutliersToIgnore;
        /// <summary>
        /// Slope represents the amount leaked per iteration. 0 means no leak.
        /// </summary>
        public double slope;
        public double yintercept;
        /// <summary>
        /// Normally the entire range of measurements. However, we can try using fewer data points to calculate slope and R² using fewer iterations
        /// </summary>
        public int NumSamplesToUse;
        internal float RSquaredThreashold;

        public int NumOutliers => (int)((NumSamplesToUse) * pctOutliersToIgnore / 100.0);

        public LeakAnalysisResult(List<uint> lst, int numSamplesToUse)
        {
            int ndx = 0;
            foreach (var itm in lst)
            {
                lstData.Add(new DataPoint() { point = new PointF() { X = ++ndx, Y = itm } });
            }
            this.NumSamplesToUse = numSamplesToUse != -1 ? numSamplesToUse : lst.Count;
            if (this.NumSamplesToUse < 0 || this.NumSamplesToUse > lst.Count)
            {
                throw new InvalidOperationException($"@ Samples to use must be >=0 && <= {lst.Count}");
            }
        }

        // http://csharphelper.com/blog/2014/10/find-a-linear-least-squares-fit-for-a-set-of-points-in-c/
        // Find the least squares linear fit.
        // Return the total error.
        public double FindLinearLeastSquaresFit()
        {
            // preliminary slope and intercept with no outliers
            lstData.ForEach(p => p.IsOutlier = false);
            CalcSlopeAndIntercept();
            if (NumOutliers > 0)
            {
                // identify outliers by finding those with largest distance from line
                foreach (var dp in lstData.Take(NumSamplesToUse))
                {
                    var pt = dp.point;
                    dp.distance = Math.Abs(yintercept + slope * pt.X - pt.Y) / Math.Sqrt(1 + slope * slope);
                }
                var sortedLst = lstData.OrderByDescending(p => p.distance).Take(NumOutliers);
                foreach (var item in sortedLst)
                {
                    item.IsOutlier = true;
                }
                CalcSlopeAndIntercept();
            }
            return Math.Sqrt(ErrorSquared());
        }

        public void CalcSlopeAndIntercept()
        {
            double SumX = 0;
            double SumY = 0;
            double SumXX = 0;
            double SumXY = 0;
            int N = 0;
            foreach (var dp in lstData.Take(NumSamplesToUse).Where(p => !p.IsOutlier))
            {
                var pt = dp.point;
                SumX += pt.X;
                SumY += pt.Y;
                SumXX += pt.X * pt.X;
                SumXY += pt.X * pt.Y;
                N++;
            }
            slope = (SumXY * N - SumX * SumY) / (SumXX * N - SumX * SumX);
            yintercept = (SumXY * SumX - SumY * SumXX) / (SumX * SumX - N * SumXX);
        }

        // Return the error squared.
        public double ErrorSquared()
        {
            double total = 0;
            foreach (var dp in lstData.Take(NumSamplesToUse))
            {
                var pt = dp.point;
                double dy = pt.Y - (slope * pt.X + yintercept);
                total += dy * dy;
            }
            return total;
        }

        /// <summary>
        /// When RSquared (range 0-1) is close to 1, indicates how well the trend is linear and matches the line.
        /// The smaller the value, the less likely the trend is linear
        /// </summary>
        public double RSquared()
        {
            var SStot = 0.0;
            var SSerr = 0.0;
            double YMean = 0;
            lstData.Take(NumSamplesToUse).ToList().ForEach(t => YMean += t.point.Y);
            YMean /= (NumSamplesToUse - NumOutliers);
            double xMean = (NumSamplesToUse - NumOutliers - 1) / 2;
            for (int i = 0; i < NumSamplesToUse; i++)
            {
                if (!lstData[i].IsOutlier)
                {
                    var t = lstData[i].point.Y - YMean;
                    SStot += t * t;
                    t = lstData[i].point.Y - (slope * lstData[i].point.X + yintercept);
                    SSerr += t * t;
                }
            }
            var rS = 1.0 - SSerr / SStot;
            return rS;
        }

        public bool IsLeak
        {
            get
            {
                var isLeak = false;
                if (slope >= perfCounterData.thresholdRegression / sensitivity && RSquared() > RSquaredThreashold)
                {
                    // if there are N iterations, the diff between last and first value must be >= N
                    // e.g. if there are 10 iterations and the handle count goes from 4 to 5, it's not a leak
                    if (slope >= .8) // 80% means in 10 iterations, grew by at least 8. E.G. For HandleCount, must leak at least .8 per iteration
                    //if (lstData[lstData.Count - 1].Y - lstData[0].Y >= lstData.Count - 1)
                    {
                        isLeak = true;
                    }
                }
                return isLeak;
            }
        }

        public override string ToString()
        {
            // r²= alt 253
            return $"{perfCounterData.PerfCounterName,-20} R²={RSquared(),8:n2} slope={slope,15:n3} Threshold={perfCounterData.thresholdRegression,11:n1} Sens={sensitivity:n3} N={NumSamplesToUse} IsLeak={IsLeak}";
        }
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
        private string baseDumpFileName;
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
                sBuilderMeasurementResult.Append($"{ctr.PerfCounterName}={pcValue,13:n0}  Δ = {delta,13:n0} ");
                lst.Add(pcValue);
            }
            if (this.sampleType == SampleType.SampleTypeIteration)
            {
                nSamplesTaken++;
            }
            var doCheck = true;
            if (stressUtilOptions.actExecuteAfterEveryIterationAsync != null)
            {
                doCheck = await stressUtilOptions.actExecuteAfterEveryIterationAsync(nSamplesTaken, this);
            }
            if (doCheck)
            {
                await CheckIfDoingSnapshotsAsync();
            }

            return sBuilderMeasurementResult.ToString();
        }

        private async Task CheckIfDoingSnapshotsAsync()
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
                    var lstLeakResults = (await CalculateLeaksAsync(showGraph: stressUtilOptions.ShowUI))
                        .Where(r => r.IsLeak).ToList();
                    if (lstLeakResults.Count > 0 || stressUtilOptions.FailTestAsifLeaksFound)
                    {
                        foreach (var leak in lstLeakResults)
                        {
                            Logger.LogMessage($"Leak Detected!!!!! {leak}");
                        }
                        if (lstLeakResults.Count == 0 && stressUtilOptions.FailTestAsifLeaksFound)
                        {
                            Logger.LogMessage($"Failing test even though no leaks found so test artifacts");
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
                            var fname = Path.Combine(ResultsFolder, $"{TestName} {DiffFileName}_{nSamplesTaken}.txt");
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
                        await CalculateMinimumNumberOfIterationsAsync(lstLeakResults);
                        if (this.testContext != null)
                        {
                            if (lstLeakResults.Count > 0)
                            {
                                throw new LeakException($"Leaks found", lstLeakResults);
                            }
                            if (stressUtilOptions.FailTestAsifLeaksFound)
                            {
                                throw new LeakException($"FailTestAsifLeaksFound", lstLeakResults);
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

        // we know it leaks. Let's give guidance to user about recommended # of versions to get the same R² and slope
        public async Task CalculateMinimumNumberOfIterationsAsync(List<LeakAnalysisResult> lstLeakResults)
        {
            if (stressUtilOptions.NumIterations > 10 && lstLeakResults.Count > 0)
            {
                Logger.LogMessage($"Calculating recommended # of iterations");
                var lstItersWithSameResults = new List<int>();
                for (int iTryNumIterations = 3; iTryNumIterations < stressUtilOptions.NumIterations; iTryNumIterations++)
                {
                    var testLeakResults = (await CalculateLeaksAsync(showGraph: false, NumSamplesToUse: iTryNumIterations)).Where(p => p.IsLeak);
                    if (testLeakResults.Count() == lstLeakResults.Count)
                    {
                        lstItersWithSameResults.Add(iTryNumIterations);
                        Logger.LogMessage($"Got same with {iTryNumIterations} {lstLeakResults[0]}");
                    }

                }
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
        /// <param name="showGraph">show a graph</param>
        /// <param name="NumSamplesToUse">-1 means use all of them. Else use the specified number: This will help figure out the min # of iterations to get the same slope and R²</param>
        /// <returns></returns>
        public async Task<List<LeakAnalysisResult>> CalculateLeaksAsync(bool showGraph, int NumSamplesToUse = -1)
        {
            var lstResults = new List<LeakAnalysisResult>();
            this.stressUtilOptions.SetPerfCounterOverrideSettings();
            foreach (var ctr in LstPerfCounterData.Where(pctr => pctr.IsEnabledForMeasurement))
            {
                var leakAnalysis = new LeakAnalysisResult(measurements[ctr.perfCounterType], NumSamplesToUse)
                {
                    perfCounterData = ctr,
                    sensitivity = stressUtilOptions.Sensitivity,
                    pctOutliersToIgnore = stressUtilOptions.pctOutliersToIgnore,
                    RSquaredThreashold = stressUtilOptions.RSquaredThreshold
                };
                leakAnalysis.FindLinearLeastSquaresFit();
                if (NumSamplesToUse == -1) // only log the real iterations, not when we're calculating the min # of iterations
                {
                    Logger.LogMessage($"{leakAnalysis}");
                }
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
            if (NumSamplesToUse == -1)
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

                        var fname = Path.Combine(ResultsFolder, $"{TestName} Graph {item.perfCounterData.PerfCounterName}.png");
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
            var filename = Path.Combine(ResultsFolder, $"{TestName} Measurements.txt");
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
                    var filename = Path.Combine(ResultsFolder, $"{testContext.TestName} StressTestLog.log");
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
