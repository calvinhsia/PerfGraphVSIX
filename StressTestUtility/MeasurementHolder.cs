﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;

namespace StressTestUtility
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
    /// <summary>
    /// Todo: discard outliers
    /// </summary>
    public class LeakAnalysisResult
    {
        public PerfCounterData perfCounterData;
        public List<PointF> lstData = new List<PointF>();
        public double sensitivity;
        public double rmsError;
        /// <summary>
        /// Slope represents the amount leaked per iteration. 0 means no leak.
        /// </summary>
        public double slope;
        public double yintercept;
        public bool IsLeak
        {
            get
            {
                var isLeak = false;
                if (slope >= perfCounterData.thresholdRegression * sensitivity && RSquared > 0.5)
                {
                    // if there are N iterations, the diff between last and first value must be >= N
                    // e.g. if there are 10 iterations and the handle count goes from 4 to 5, it's not a leak
                    if (slope >= 1)
                    //if (lstData[lstData.Count - 1].Y - lstData[0].Y >= lstData.Count - 1)
                    {
                        isLeak = true;
                    }
                }
                return isLeak;
            }
        }
        /// <summary>
        /// When RSquared (range 0-1) is close to 1, indicates how well the trend is linear and matches the line.
        /// The smaller the value, the less likely the trend is linear
        /// </summary>
        public double RSquared
        {
            get
            {
                var SStot = 0.0;
                var SSerr = 0.0;
                double YMean = 0;
                lstData.ForEach(t => YMean += t.Y);
                YMean /= lstData.Count;
                double xMean = (lstData.Count - 1) / 2;
                for (int i = 0; i < lstData.Count; i++)
                {
                    var t = lstData[i].Y - YMean;
                    SStot += t * t;
                    t = lstData[i].Y - (slope * lstData[i].X + yintercept);
                    SSerr += t * t;
                }
                var rS = 1.0 - SSerr / SStot;
                return rS;
            }
        }
        public override string ToString()
        {
            // r²= alt 253
            return $"{perfCounterData.PerfCounterName,-20} R²={RSquared,8:n2} slope={slope,15:n3} Threshold={perfCounterData.thresholdRegression,10:n0} Sens={sensitivity:n2} IsLeak={IsLeak}";
        }
    }

    public class FileResultsData
    {
        public string filename;
        public string description;
    }
    public class MeasurementHolder : IDisposable
    {
        public const string DiffFileName = "String and Type Count differences";
        public string TestName;

        /// <summary>
        /// The list of perfcounters to use
        /// </summary>
        public readonly List<PerfCounterData> lstPerfCounterData;
        readonly ILogger logger;
        readonly SampleType sampleType;
        readonly double sensitivity;
        private readonly int NumTotalIterations;
        private readonly int NumIterationsBeforeTotalToTakeBaselineSnapshot;
        private readonly bool ShowUI;
        internal Dictionary<PerfCounterType, List<uint>> measurements = new Dictionary<PerfCounterType, List<uint>>(); // PerfCounterType=> measurements per iteration
        int nSamplesTaken;
        /// <summary>
        /// unique folder per test.
        /// </summary>
        public string ResultsFolder;
        private string baseDumpFileName;
        readonly TestContextWrapper testContext;
        readonly List<FileResultsData> lstFileResults = new List<FileResultsData>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="TestNameOrTestContext">When running from MSTest, the TestContext can be used to get TestName as well as various other properties. When run from VSix, the compiled code test name
        ///         When run from command line there will be no TestContext</param>
        /// <param name="lstPCData">The list of PerfCounters to use.</param>
        /// <param name="sampleType"></param>
        /// <param name="NumTotalIterations">-1 means don't take base or final dump. </param>
        /// <param name="logger"></param>
        /// <param name="sensitivity"></param>
        public MeasurementHolder(object TestNameOrTestContext,
                    List<PerfCounterData> lstPCData,
                    SampleType sampleType,
                    ILogger logger,
                    int NumTotalIterations,
                    int NumIterationsBeforeTotalToTakeBaselineSnapshot = 4,
                    bool ShowUI = false,
                    double sensitivity = 1.0f)
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
            this.lstPerfCounterData = lstPCData;
            this.sampleType = sampleType;
            this.logger = logger;
            this.sensitivity = sensitivity;
            this.NumTotalIterations = NumTotalIterations;
            this.NumIterationsBeforeTotalToTakeBaselineSnapshot = NumIterationsBeforeTotalToTakeBaselineSnapshot;
            this.ShowUI = ShowUI;

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
                ResultsFolder = pathResultsFolder;
            }
            else
            {
                ResultsFolder = Path.Combine(this.testContext.TestDeploymentDir, this.TestName);
            }
            if (!Directory.Exists(ResultsFolder))
            {
                Directory.CreateDirectory(ResultsFolder);
            }
            foreach (var entry in lstPCData)
            {
                measurements[entry.perfCounterType] = new List<uint>();
            }
        }

        public async Task<string> TakeMeasurementAsync(string desc)
        {
            if (string.IsNullOrEmpty(desc))
            {
                desc = TestName;
            }
            if (PerfCounterData.ProcToMonitor.Id == System.Diagnostics.Process.GetCurrentProcess().Id)
            {
                GC.Collect(); // ok to collect twice
            }
            var sBuilderMeasurementResult = new StringBuilder(desc + $" {PerfCounterData.ProcToMonitor.ProcessName} {PerfCounterData.ProcToMonitor.Id} ");
            foreach (var ctr in lstPerfCounterData.Where(pctr => pctr.IsEnabledForMeasurement || pctr.IsEnabledForGraph))
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
            if (NumTotalIterations >= NumIterationsBeforeTotalToTakeBaselineSnapshot)
            {
                // if we have enough iterations, lets take a snapshot before they're all done so we can compare: take a baseline snapshot 
                if (nSamplesTaken == NumTotalIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    logger.LogMessage("Taking base snapshot dump");
                    baseDumpFileName = await CreateDumpAsync(
                        System.Diagnostics.Process.GetCurrentProcess().Id,
                        desc: TestName + "_" + nSamplesTaken.ToString(),
                        memoryAnalysisType: MemoryAnalysisType.JustCreateDump);
                    lstFileResults.Add(new FileResultsData() { filename = baseDumpFileName, description = $"BaselineDumpFile taken after iteration # {nSamplesTaken}" });
                }
                else if (nSamplesTaken == NumTotalIterations) // final snapshot?
                {
                    var filenameResultsCSV = DumpOutMeasurementsToCsv();
                    logger.LogMessage($"Measurement Results {filenameResultsCSV}");
                    var lstLeakResults = (await CalculateLeaksAsync(showGraph: ShowUI))
                        .Where(r => r.IsLeak).ToList();
                    if (lstLeakResults.Count > 0)
                    {
                        foreach (var leak in lstLeakResults)
                        {
                            logger.LogMessage($"Leak Detected!!!!! {leak}");
                        }
                        var currentDumpFile = await CreateDumpAsync(
                            PerfCounterData.ProcToMonitor.Id,
                            desc: TestName + "_" + NumTotalIterations.ToString(),
                            memoryAnalysisType: ShowUI ? MemoryAnalysisType.StartClrObjExplorer : MemoryAnalysisType.JustCreateDump);
                        lstFileResults.Add(new FileResultsData() { filename = currentDumpFile, description = "CurrentDumpFile" });
                        if (!string.IsNullOrEmpty(baseDumpFileName))
                        {
                            var oDumpAnalyzer = new DumpAnalyzer(logger);
                            var sb = oDumpAnalyzer.GetDiff(baseDumpFileName,
                                            currentDumpFile,
                                            NumTotalIterations,
                                            NumIterationsBeforeTotalToTakeBaselineSnapshot);
                            var fname = Path.Combine(ResultsFolder, $"{TestName} {DiffFileName}.txt");
                            File.WriteAllText(fname, sb.ToString());
                            if (ShowUI)
                            {
                                Process.Start(fname);
                            }
                            lstFileResults.Add(new FileResultsData() { filename = fname, description = "Differences for Type and String counts" });
                            logger.LogMessage("DumpDiff Analysis " + fname);
                        }
                        else
                        {
                            logger.LogMessage($"No baseline dump: not enough iterations");
                        }
                        if (this.testContext != null)
                        {
                            throw new LeakException($"Leaks found", lstLeakResults);
                        }
                    }
                }
            }
            return sBuilderMeasurementResult.ToString();
        }

        /// <summary>
        /// get the counter for graphing
        /// </summary>
        /// <returns></returns>
        public List<uint> GetLastMeasurements()
        {
            var res = new List<uint>();
            foreach (var ctr in lstPerfCounterData.Where(pctr => pctr.IsEnabledForGraph))
            {
                var entry = measurements[ctr.perfCounterType];
                res.Add(entry[entry.Count - 1]);
            }
            return res;
        }

        public async Task<List<LeakAnalysisResult>> CalculateLeaksAsync(bool showGraph)
        {
            var lstResults = new List<LeakAnalysisResult>();
            foreach (var ctr in lstPerfCounterData.Where(pctr => pctr.IsEnabledForMeasurement || pctr.IsEnabledForGraph))
            {
                var leakAnalysis = new LeakAnalysisResult()
                {
                    perfCounterData = ctr,
                    sensitivity = this.sensitivity
                };
                int ndx = 1;
                foreach (var itm in measurements[ctr.perfCounterType])
                {
                    leakAnalysis.lstData.Add(new PointF() { X = ndx++, Y = itm });
                }
                leakAnalysis.rmsError = FindLinearLeastSquaresFit(leakAnalysis.lstData, out leakAnalysis.slope, out leakAnalysis.yintercept);
                logger.LogMessage($"{leakAnalysis}");
                lstResults.Add(leakAnalysis);
            }
            if (showGraph)
            {
                var tcs = new TaskCompletionSource<int>();
                // if we're running in testhost process, then we want a timeout
                var timeoutEnabled = this.testContext != null;
                logger.LogMessage($"Showing graph  timeoutenabled ={timeoutEnabled}");
                var thr = new Thread((o) =>
                {
                    try
                    {
                        var graphWin = new GraphWin(this);
                        graphWin.AddGraph(lstResults);
                        if (!timeoutEnabled)
                        {
                            //                            graphWin.WindowState = WindowState.Maximized;
                        }
                        graphWin.ShowDialog();
                        logger.LogMessage($"finished showing graph");
                    }
                    catch (Exception ex)
                    {
                        logger.LogMessage($"graph {ex.ToString()}");
                    }
                    tcs.SetResult(0);
                });
                thr.SetApartmentState(ApartmentState.STA);
                thr.Start();
                if (await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(120))) != tcs.Task)
                {
                    logger.LogMessage($"Timedout showing graph");
                }
            }
            // do this after showgraph else hang
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
                    for (int i = 0; i < item.lstData.Count; i++)
                    {
                        var dp = new DataPoint(i + 1, item.lstData[i].Y); // measurements taken at end of iteration
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
                    var dp1 = new DataPoint(item.lstData.Count, item.lstData.Count * item.slope + item.yintercept);
                    seriesTrendLine.Points.Add(dp1);

                    chart.Legends.Add(new Legend());
                    //var legend = new Legend();
                    //legend.Title = item.perfCounterData.PerfCounterName;
                    //var it = new LegendItem() { na}
                    //legend.it
                    //chart.Legends.Add(legend);
                    //var legend2 = new Legend();
                    //legend2.Title = "Trend Line";
                    //chart.Legends.Add(legend2);


                    var fname = Path.Combine(ResultsFolder, $"{TestName} Graph {item.perfCounterData.PerfCounterName}.png");
                    chart.SaveImage(fname, ChartImageFormat.Png);
                    lstFileResults.Add(new FileResultsData() { filename = fname, description = $"Graph {item.perfCounterData}" });
                }
            }

            return lstResults;
        }

        public string DumpOutMeasurementsToCsv()
        {
            var sb = new StringBuilder();
            var lst = new List<string>();
            foreach (var ctr in lstPerfCounterData.Where(pctr => pctr.IsEnabledForMeasurement || pctr.IsEnabledForGraph))
            {
                lst.Add(ctr.PerfCounterName);
            }
            sb.AppendLine(string.Join(",", lst.ToArray()));

            for (int i = 0; i < nSamplesTaken; i++)
            {
                lst.Clear();
                foreach (var ctr in lstPerfCounterData.Where(pctr => pctr.IsEnabledForMeasurement || pctr.IsEnabledForGraph))
                {
                    if (i < measurements[ctr.perfCounterType].Count)
                    {
                        lst.Add($"{measurements[ctr.perfCounterType][i]}");
                    }
                    else
                    {
                        logger.LogMessage($"Index out of range {ctr.PerfCounterName}  {i}  {measurements[ctr.perfCounterType].Count}");
                    }
                }
                sb.AppendLine(string.Join(",", lst.ToArray()));
            }
            var filename = Path.Combine(ResultsFolder, $" {TestName} Measurements.csv");
            File.WriteAllText(filename, sb.ToString());
            lstFileResults.Add(new FileResultsData() { filename = filename, description = "Raw Measuremensts as CSV File to open in Excel" });
            return filename;
        }

        public async Task<string> CreateDumpAsync(int pid, MemoryAnalysisType memoryAnalysisType, string desc)
        {
            var pathDumpFile = Path.ChangeExtension(Path.Combine(ResultsFolder, desc), ".dmp");
            try
            {
                var arglist = new List<string>()
                    {
                        "-p", pid.ToString(),
                        "-f",  "\"" + pathDumpFile + "\""
                    };
                if (memoryAnalysisType.HasFlag(MemoryAnalysisType.StartClrObjExplorer))
                {
                    logger.LogMessage($"start clrobjexplorer {pathDumpFile}");
                    arglist.Add("-c");
                }
                var odumper = new DumperViewerMain(arglist.ToArray())
                {
                    _logger = logger
                };
                await odumper.DoitAsync();
            }
            catch (Exception ex)
            {
                logger.LogMessage(ex.ToString());
            }
            return pathDumpFile;
        }

        public override string ToString()
        {
            return $"{TestName} #Samples={nSamplesTaken}";
        }


        // http://csharphelper.com/blog/2014/10/find-a-linear-least-squares-fit-for-a-set-of-points-in-c/
        // Find the least squares linear fit.
        // Return the total error.
        public static double FindLinearLeastSquaresFit(
            List<PointF> points, out double m, out double b)
        {
            double N = points.Count;
            double SumX = 0;
            double SumY = 0;
            double SumXX = 0;
            double SumXY = 0;
            foreach (PointF pt in points)
            {
                SumX += pt.X;
                SumY += pt.Y;
                SumXX += pt.X * pt.X;
                SumXY += pt.X * pt.Y;
            }
            m = (SumXY * N - SumX * SumY) / (SumXX * N - SumX * SumX);
            b = (SumXY * SumX - SumY * SumXX) / (SumX * SumX - N * SumXX);
            return Math.Sqrt(ErrorSquared(points, m, b));
        }
        // Return the error squared.
        public static double ErrorSquared(List<PointF> points,
            double m, double b)
        {
            double total = 0;
            foreach (PointF pt in points)
            {
                double dy = pt.Y - (m * pt.X + b);
                total += dy * dy;
            }
            return total;
        }

        public void Dispose()
        {
            if (this.testContext != null)
            {
                if (logger is Logger myLogger)
                {
                    var sb = new StringBuilder();
                    foreach (var str in myLogger._lstLoggedStrings)
                    {
                        sb.AppendLine(str);
                    }
                    var filename = Path.Combine(ResultsFolder, "StressTestLog.log");
                    File.WriteAllText(filename, sb.ToString());
                    lstFileResults.Add(new FileResultsData() { filename = filename, description = "Stress Test Log" });
                }
                if (this.testContext != null)
                {
                    var sbHtml = new StringBuilder("");
                    foreach (var fileresult in lstFileResults)
                    {
                        this.testContext.AddResultFile(fileresult.filename);
                        switch (Path.GetExtension(fileresult.filename))
                        {
                            case ".dmp":
                                //            var strHtml = @"
                                //<a href=""file://C:/Users/calvinh/Source/repos/PerfGraphVSIX/TestResults/Deploy_calvinh 2019-11-19 11_00_13/Out/TestMeasureRegressionVerifyGraph/Graph Handle Count.png"">gr </a>
                                //            ";
                                //            var fileHtml = Path.Combine(resultsFolder, "Index.html");
                                //            File.WriteAllText(fileHtml, strHtml);
                                //            TestContext.AddResultFile(fileHtml);
                                sbHtml.AppendLine($@"<p><a href=""file://{DumpAnalyzer.GetClrObjExplorerPath()} -m {fileresult.filename}"">Start ClrObjExplorer with dump {Path.GetFileName(fileresult.filename)} </a>");
                                break;
                            default:
                                sbHtml.AppendLine($@"<p><a href=""file://{fileresult.filename}"">{Path.GetFileName(fileresult.filename)}</a>");
                                break;
                        }

                    }
                    var filenameHtml = Path.Combine(ResultsFolder, "Index.html");
                    File.WriteAllText(filenameHtml, sbHtml.ToString());
                    this.testContext.AddResultFile(filenameHtml);
                }
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
