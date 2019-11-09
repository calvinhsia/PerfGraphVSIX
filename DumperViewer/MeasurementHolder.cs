using DumperViewer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;

namespace PerfGraphVSIX
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
        /// after creating a dump, the ClrObjectExplorer WPF app is started with the dump loaded for manual analysis
        /// </summary>
        StartClrObjectExplorer = 0x2,
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
    public class RegressionAnalysis
    {
        public PerfCounterData perfCounterData;
        public List<PointF> lstData = new List<PointF>();
        public double rmsError;
        /// <summary>
        /// Slope represents the amount leaked per iteration. 0 means no leak.
        /// </summary>
        public double slope;
        public double yintercept;
        public bool IsRegression;
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
                    SStot += Math.Pow(lstData[i].Y - YMean, 2.0);
                    SSerr += Math.Pow(lstData[i].Y - (slope * lstData[i].X + yintercept), 2.0);
                }
                var rS = 1.0 - SSerr / SStot;
                return rS;
            }
        }
        public override string ToString()
        {
            return $"{perfCounterData.PerfCounterName,-20} RmsErr={rmsError,16:n1} R²={RSquared,8:n2} slope={slope,15:n1} YIntercept={yintercept,15:n1} Thrs={perfCounterData.thresholdRegression,10:n0} Sens={perfCounterData.RatioThresholdSensitivity} isRegression={IsRegression}";
        }
    }

    public class MeasurementHolder
    {
        public string TestName;

        /// <summary>
        /// The list of perfcounters to use
        /// </summary>
        public readonly List<PerfCounterData> lstPerfCounterData;
        readonly ILogger logger;
        readonly SampleType sampleType;
        internal Dictionary<PerfCounterType, List<uint>> measurements = new Dictionary<PerfCounterType, List<uint>>(); // PerfCounterType=> measurements per iteration
        int nSamplesTaken;

        public MeasurementHolder(string TestName, List<PerfCounterData> lstPCData, SampleType sampleType, ILogger logger)
        {
            this.TestName = TestName;
            this.lstPerfCounterData = lstPCData;
            this.sampleType = sampleType;
            this.logger = logger;
            foreach (var entry in lstPCData)
            {
                measurements[entry.perfCounterType] = new List<uint>();
            }
        }

        public string TakeMeasurement(string desc)
        {
            if (string.IsNullOrEmpty(desc))
            {
                desc = TestName;
            }
            if (PerfCounterData.ProcToMonitor.Id == System.Diagnostics.Process.GetCurrentProcess().Id)
            {
                GC.Collect();
            }
            var sBuilder = new StringBuilder(desc + " ");
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
                sBuilder.Append($"{ctr.PerfCounterName}={pcValue,13:n0}  Δ = {delta,13:n0} ");
                lst.Add(pcValue);
            }
            nSamplesTaken++;
            return sBuilder.ToString();
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

        public async Task<List<RegressionAnalysis>> CalculateRegressionAsync(bool showGraph, bool fUseAlgP1 = false)
        {
            var lstResults = new List<RegressionAnalysis>();
            foreach (var ctr in lstPerfCounterData.Where(pctr => pctr.IsEnabledForMeasurement || pctr.IsEnabledForGraph))
            {
                var r = new RegressionAnalysis()
                {
                    perfCounterData = ctr
                };
                int ndx = 0;
                if (fUseAlgP1)
                {
                    var lstx = new List<double>();
                    var lsty = new List<double>();
                    foreach (var itm in measurements[ctr.perfCounterType])
                    {
                        lstx.Add(ndx);
                        lsty.Add(itm);
                        r.lstData.Add(new PointF() { X = ndx++, Y = itm });
                    }
                    LinearRegression(lstx.ToArray(), lsty.ToArray(), 0, lsty.Count - 1, out var rsquared, out var yintercept, out var slope);
                    r.slope = slope;
                    r.yintercept = yintercept;
                    r.rmsError = rsquared;
                }
                else
                {
                    foreach (var itm in measurements[ctr.perfCounterType])
                    {
                        r.lstData.Add(new PointF() { X = ndx++, Y = itm });
                    }
                    r.rmsError = MeasurementHolder.FindLinearLeastSquaresFit(r.lstData, out r.slope, out r.yintercept);
                    if (r.RSquared > 0.5 && r.slope >= ctr.thresholdRegression * ctr.RatioThresholdSensitivity)
                    {
                        r.IsRegression = true;
                    }
                }
                logger.LogMessage($"{r}");
                lstResults.Add(r);
            }
            if (showGraph)
            {
                var tcs = new TaskCompletionSource<int>();
                var timeoutEnabled = Process.GetCurrentProcess().ProcessName.IndexOf("testhost", StringComparison.InvariantCultureIgnoreCase) > 0;
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
            return lstResults;
        }

        public string DumpOutMeasurementsToTempFile(bool StartExcel)
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
            var filename = BrowseList.WriteOutputToTempFile(sb.ToString(), fExt: "csv", fStartIt: StartExcel);
            return filename;
        }

        public async Task CreateDumpAsync(int pid, MemoryAnalysisType memoryAnalysisType, string desc)
        {
            try
            {
                var pathDumpFile = DumperViewer.DumperViewerMain.GetNewDumpFileName(baseName: string.IsNullOrEmpty(desc) ? "devenv" : desc);
                logger.LogMessage($"start clrobjexplorer {pathDumpFile}");
                var arglist = new List<string>()
                    {
                        "-p", pid.ToString(),
                        "-f",  "\"" + pathDumpFile + "\""
                    };
                if (memoryAnalysisType.HasFlag(MemoryAnalysisType.StartClrObjectExplorer))
                {
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
        }

        public override string ToString()
        {
            return $"{TestName} #Samples={nSamplesTaken}";
        }


        /// <summary>
        /// Fits a line to a collection of (x,y) points.
        /// </summary>
        /// <param name="xVals">The x-axis values.</param>
        /// <param name="yVals">The y-axis values.</param>
        /// <param name="inclusiveStart">The inclusive inclusiveStart index.</param>
        /// <param name="exclusiveEnd">The exclusive exclusiveEnd index.</param>
        /// <param name="rsquared">The r^2 value of the line.</param>
        /// <param name="yintercept">The y-intercept value of the line (i.e. y = ax + b, yintercept is b).</param>
        /// <param name="slope">The slop of the line (i.e. y = ax + b, slope is a).</param>
        public static void LinearRegression(double[] xVals, double[] yVals,
                                            int inclusiveStart, int exclusiveEnd,
                                            out double rsquared, out double yintercept,
                                            out double slope)
        {
            double sumOfX = 0;
            double sumOfY = 0;
            double sumOfXSq = 0;
            double sumOfYSq = 0;
            double sumCodeviates = 0;
            double count = exclusiveEnd - inclusiveStart;

            for (int ctr = inclusiveStart; ctr < exclusiveEnd; ctr++)
            {
                double x = xVals[ctr];
                double y = yVals[ctr];
                sumCodeviates += x * y;
                sumOfX += x;
                sumOfY += y;
                sumOfXSq += x * x;
                sumOfYSq += y * y;
            }
            double ssX = sumOfXSq - sumOfX * sumOfX / count;
            //            double ssY = sumOfYSq - sumOfY * sumOfY / count;
            double RNumerator = (count * sumCodeviates) - (sumOfX * sumOfY);
            double RDenom = (count * sumOfXSq - (sumOfX * sumOfX))
             * (count * sumOfYSq - (sumOfY * sumOfY));
            double sCo = sumCodeviates - sumOfX * sumOfY / count;

            double meanX = sumOfX / count;
            double meanY = sumOfY / count;
            double dblR = RNumerator / Math.Sqrt(RDenom);
            rsquared = dblR * dblR;
            yintercept = meanY - ((sCo / ssX) * meanX);
            slope = sCo / ssX;
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
