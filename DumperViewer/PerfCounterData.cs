using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace PerfGraphVSIX
{
    [Flags] // user can select multiple items. (beware scaling: pct => 0-100, Bytes => 0-4G)
    public enum PerfCounterType
    {
        None,
        ProcessorPctTime = 0x1,
        ProcessorPrivateBytes = 0x2,
        ProcessorVirtualBytes = 0x4,
        ProcessorWorkingSet = 0x8,
        GCPctTime = 0x10,
        GCBytesInAllHeaps = 0x20,
        GCAllocatedBytesPerSec = 0x40,
        PageFaultsPerSec = 0x80,
        KernelHandleCount = 0x100, // same as Win32api GetProcessHandleCount
        GDIHandleCount = 0x200, //GetGuiResources
        UserHandleCount = 0x400, //GetGuiResources
        ThreadCount = 0x800,
    }

    public class PerfCounterData
    {

        public PerfCounterType perfCounterType;
        public string PerfCounterCategory;
        public string PerfCounterName;
        public string PerfCounterInstanceName;
        public bool IsEnabled = false;
        public Lazy<PerformanceCounter> lazyPerformanceCounter;
        public static Process ProcToMonitor;

        public float LastValue;
        public float ReadNextValue()
        {
            float retVal = 0;
            switch (perfCounterType)
            {
                case PerfCounterType.UserHandleCount:
                    retVal = GetGuiResourcesGDICount();
                    break;
                case PerfCounterType.GDIHandleCount:
                    retVal = GetGuiResourcesUserCount();
                    break;
                default:
                    if (lazyPerformanceCounter.Value != null)
                    {
                        retVal = lazyPerformanceCounter.Value.NextValue();
                    }
                    break;
            }
            LastValue = retVal;
            return retVal;
        }
        public PerfCounterData(PerfCounterType perfCounterType, string perfCounterCategory, string perfCounterName, string perfCounterInstanceName)
        {
            this.perfCounterType = perfCounterType;
            this.PerfCounterCategory = perfCounterCategory;
            this.PerfCounterName = perfCounterName;
            this.PerfCounterInstanceName = perfCounterInstanceName;
            this.ResetCounter();
            ProcToMonitor = Process.GetCurrentProcess(); // this will be changed by stress tests
        }

        public void ResetCounter()
        {
            this.lazyPerformanceCounter = new Lazy<PerformanceCounter>(() =>
            {
                PerformanceCounter pc = null;
                var category = new PerformanceCounterCategory(PerfCounterCategory);
                foreach (var instanceName in category.GetInstanceNames().Where(p => p.StartsWith(ProcToMonitor.ProcessName))) //'devenv'
                {
                    using (var cntr = new PerformanceCounter(category.CategoryName, PerfCounterInstanceName, instanceName, readOnly: true))
                    {
                        try
                        {
                            var val = (int)cntr.NextValue();
                            if (val == ProcToMonitor.Id)
                            {
                                pc = new PerformanceCounter(PerfCounterCategory, PerfCounterName, instanceName);
                                break;
                            }
                        }
                        catch (Exception) //. Could get exception if you're not admin or "Performance Monitor Users" group (must re-login)
                        {
                            // System.InvalidOperationException: Instance 'IntelliTrace' does not exist in the specified Category.
                        }
                    }
                }
                return pc;
            });
        }

        public override string ToString()
        {
            return $"{perfCounterType} {PerfCounterCategory} {PerfCounterName} {PerfCounterInstanceName} Enabled = {IsEnabled}";
        }
        /// uiFlags: 0 - Count of GDI objects
        /// uiFlags: 1 - Count of USER objects
        /// - Win32 GDI objects (pens, brushes, fonts, palettes, regions, device contexts, bitmap headers)
        /// - Win32 USER objects:
        ///      - WIN32 resources (accelerator tables, bitmap resources, dialog box templates, font resources, menu resources, raw data resources, string table entries, message table entries, cursors/icons)
        /// - Other USER objects (windows, menus)
        ///
        [DllImport("User32")]
        extern public static int GetGuiResources(IntPtr hProcess, int uiFlags);

        public static int GetGuiResourcesGDICount()
        {
            return GetGuiResources(ProcToMonitor.Handle, uiFlags: 0);
        }

        public static int GetGuiResourcesUserCount()
        {
            return GetGuiResources(ProcToMonitor.Handle, uiFlags: 1);
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
            Debug.Assert(xVals.Length == yVals.Length);
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
        public struct PointF
        {
            public double X;
            public double Y;
        }
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
}
