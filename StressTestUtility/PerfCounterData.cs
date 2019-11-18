using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace StressTestUtility
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
        /// <summary>
        /// These are the counters used for stress test measurements
        /// </summary>
        public static readonly List<PerfCounterData> _lstPerfCounterDefinitionsForStressTest = new List<PerfCounterData>()
        {
            {new PerfCounterData(PerfCounterType.GCBytesInAllHeaps, ".NET CLR Memory","# Bytes in all Heaps","Process ID" ) { IsEnabledForGraph=true,thresholdRegression=1e6f} },
            {new PerfCounterData(PerfCounterType.ProcessorPrivateBytes, "Process","Private Bytes","ID Process") { thresholdRegression=1e6f} },
            {new PerfCounterData(PerfCounterType.ProcessorVirtualBytes, "Process","Virtual Bytes","ID Process") { thresholdRegression=1e6f}},
            //{new PerfCounterData(PerfCounterType.ProcessorWorkingSet, "Process","Working Set","ID Process") { thresholdRegression=1e6f}},
            {new PerfCounterData(PerfCounterType.ThreadCount, "Process","Thread Count","ID Process") { thresholdRegression=1}},
            {new PerfCounterData(PerfCounterType.KernelHandleCount, "Process","Handle Count","ID Process") { thresholdRegression=.5f}},
            {new PerfCounterData(PerfCounterType.GDIHandleCount, "GetGuiResources","GDIHandles",string.Empty) { thresholdRegression=.5f } },
            {new PerfCounterData(PerfCounterType.UserHandleCount, "GetGuiResources","UserHandles",string.Empty) { thresholdRegression=.5f}},
        };
        /// <summary>
        /// these are used to provider interactive user counters from which to choose in VSIX to drive graph
        /// typically, user will select only 1 or 2 else the graph is too busy (and different scales)
        /// </summary>
        public static readonly List<PerfCounterData> _lstPerfCounterDefinitionsForVSIX = new List<PerfCounterData>()
        {
            {new PerfCounterData(PerfCounterType.GCBytesInAllHeaps, ".NET CLR Memory","# Bytes in all Heaps","Process ID" ) { IsEnabledForMeasurement=false} },
            {new PerfCounterData(PerfCounterType.ProcessorPrivateBytes, "Process","Private Bytes","ID Process")  { IsEnabledForMeasurement=false} },
            {new PerfCounterData(PerfCounterType.ProcessorVirtualBytes, "Process","Virtual Bytes","ID Process")  { IsEnabledForMeasurement=false} },
            {new PerfCounterData(PerfCounterType.ProcessorPctTime, "Process","% Processor Time","ID Process" ) { IsEnabledForMeasurement=false} } ,
            {new PerfCounterData(PerfCounterType.ProcessorWorkingSet, "Process","Working Set","ID Process")  { IsEnabledForMeasurement=false} },
            {new PerfCounterData(PerfCounterType.GCPctTime, ".NET CLR Memory","% Time in GC","Process ID")  { IsEnabledForMeasurement=false} },
            {new PerfCounterData(PerfCounterType.GCAllocatedBytesPerSec, ".NET CLR Memory","Allocated Bytes/sec","Process ID")  { IsEnabledForMeasurement=false} },
            {new PerfCounterData(PerfCounterType.PageFaultsPerSec, "Process","Page Faults/sec","ID Process")  { IsEnabledForMeasurement=false} },
            {new PerfCounterData(PerfCounterType.ThreadCount, "Process","Thread Count","ID Process")  { IsEnabledForMeasurement=false} },
            {new PerfCounterData(PerfCounterType.KernelHandleCount, "Process","Handle Count","ID Process")  { IsEnabledForMeasurement=false} },
            {new PerfCounterData(PerfCounterType.GDIHandleCount, "GetGuiResources","GDIHandles",string.Empty)  { IsEnabledForMeasurement=false} },
            {new PerfCounterData(PerfCounterType.UserHandleCount, "GetGuiResources","UserHandles",string.Empty)  { IsEnabledForMeasurement=false} },
        };

        public PerfCounterType perfCounterType;
        public string PerfCounterCategory;
        public string PerfCounterName;
        public string PerfCounterInstanceName;
        /// <summary>
        /// counter can be enabled for graphing but not for measurements (graph can look too busy and scaling can hide trends)
        /// </summary>
        public bool IsEnabledForGraph = false;
        /// <summary>
        /// true means collect (for iterations) but not necessarily for graph
        /// </summary>
        public bool IsEnabledForMeasurement = true;
        public Lazy<PerformanceCounter> lazyPerformanceCounter;
        public static Process ProcToMonitor;

        public float LastValue;
        /// <summary>
        /// We calculate the linear regression slope, which is the growth of the counter per iteration. If this growth changes > this threshold, then fail the test
        /// </summary>
        public float thresholdRegression;

        ///// <summary>
        ///// This is a scale factor (sensitivity) multiplied by the thrsholdRegression. Should be > 0, centered at 1
        ///// If the iteration is small and fast, and doesn't do much allocation, then our static thresholds might be too big, so set this to be e.g. .5 for half the threshold
        ///// Likewise, if the iteration is huge and leaky, set this >1 to increase the default theshold
        ///// Default to 1 means no effect: use the threshold defaults.
        ///// </summary>
        //public float RatioThresholdSensitivity = 1;
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
            return $"{perfCounterType} {PerfCounterCategory} {PerfCounterName} {PerfCounterInstanceName} EnabledForMeasure = {IsEnabledForMeasurement} IsEnabledForGraph={IsEnabledForGraph}";
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
    }
}
