using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerfGraphVSIX
{
    public class MeasurementHolder
    {
        public string TestName;

        /// <summary>
        /// The list of perfcounters to use
        /// </summary>
        public readonly List<PerfCounterData> lstPerfCounterData;
        readonly ILogger logger;

        internal Dictionary<string, List<uint>> measurements = new Dictionary<string, List<uint>>(); // ctrname=> measurements per iteration
        int nSamplesTaken;


        public MeasurementHolder(string TestName, List<PerfCounterData> lstPCData, ILogger logger)
        {
            this.TestName = TestName;
            this.lstPerfCounterData = lstPCData;
            this.logger = logger;
            foreach (var entry in lstPCData)
            {
                measurements[entry.PerfCounterName] = new List<uint>();
            }
        }

        public string TakeMeasurement(string desc, SampleType sampleType)
        {
            if (string.IsNullOrEmpty(desc))
            {
                desc = TestName;
            }
            var sBuilder = new StringBuilder(desc + " ");
            foreach (var ctr in lstPerfCounterData.Where(pctr => pctr.IsEnabledForMeasurement || pctr.IsEnabledForGraph))
            {
                if (!measurements.TryGetValue(ctr.PerfCounterName, out var lst))
                {
                    lst = new List<uint>();
                    measurements[ctr.PerfCounterName] = lst;
                }
                var pcValueAsFloat = ctr.ReadNextValue();
                uint priorValue = 0;
                if (lst.Count > 0)
                {
                    priorValue = lst[0];
                    if (sampleType != SampleType.SampleTypeIteration)
                    {
                        lst.RemoveAt(0); // we're not iterating, don't accumulate more than 1 (1 for previous)
                    }
                    else
                    {
                    }
                }
                uint pcValue = (uint)pcValueAsFloat;
                int delta = (int)pcValue - (int)priorValue;
                sBuilder.Append($"{ctr.PerfCounterName}={pcValue:n0}  Δ = {delta:n0} ");
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
                var entry = measurements[ctr.PerfCounterName];
                res.Add(entry[entry.Count - 1]);
            }
            return res;
        }
        public bool CalculateRegression()
        {
            var AnyCounterRegresssed = false;
            foreach (var ctr in lstPerfCounterData.Where(pctr => pctr.IsEnabledForMeasurement || pctr.IsEnabledForGraph))
            {
                var lstData = new List<PointF>();
                int ndx = 0;
                foreach (var itm in measurements[ctr.PerfCounterName])
                {
                    lstData.Add(new PointF() { X = ndx++, Y = itm });
                }
                var rmsError = MeasurementHolder.FindLinearLeastSquaresFit(lstData, out var m, out var b);
                var isRegression = false;
                if (m > ctr.thresholdRegression * ctr.RatioThresholdSensitivity)
                {
                    isRegression = true;
                    AnyCounterRegresssed = true;
                }
                logger.LogMessage($"{ctr.PerfCounterName,-25} RmsErr={rmsError,16:n3} m={m,18:n3} b={b,18:n3} Thrs={ctr.thresholdRegression,8:n0} Sens={ctr.RatioThresholdSensitivity} isRegression={isRegression}");
            }
            return AnyCounterRegresssed;
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
                    if (i < measurements[ctr.PerfCounterName].Count)
                    {
                        lst.Add($"{measurements[ctr.PerfCounterName][i]}");
                    }
                    else
                    {
                        logger.LogMessage($"Index out of range {ctr.PerfCounterName}  {i}  {measurements[ctr.PerfCounterName].Count}");
                    }
                }
                sb.AppendLine(string.Join(",", lst.ToArray()));
            }

            return BrowseList.WriteOutputToTempFile(sb.ToString(), fExt: "csv", fStartIt: StartExcel);
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
    }
}
