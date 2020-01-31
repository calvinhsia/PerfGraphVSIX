using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Test.Stress
{
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
        public int NumSamplesToUse =>lstData.Count;
        internal float RSquaredThreashold;

        public int NumOutliers => (int)((NumSamplesToUse) * pctOutliersToIgnore / 100.0);

        public LeakAnalysisResult(List<uint> lst)
        {
            int ndx = 0;
            foreach (var itm in lst)
            {
                lstData.Add(new DataPoint() { point = new PointF() { X = ++ndx, Y = itm } });
            }
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
}