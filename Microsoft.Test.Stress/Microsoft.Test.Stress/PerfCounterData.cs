using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Test.Stress
{
    [Flags] // user can select multiple items. (beware scaling: pct => 0-100, Bytes => 0-4G)
    public enum PerfCounterType
    {
        None,
        /// <summary>
        /// % Processor Time is the percentage of elapsed time that all of process threads used the processor to execution instructions. 
        /// An instruction is the basic unit of execution in a computer, a thread is the object that executes instructions, and a process is the object created when a program is run. 
        /// Code executed to handle some hardware interrupts and trap conditions are included in this count.
        /// </summary>
        ProcessorPctTime = 0x1,
        /// <summary>
        /// Private Bytes is the current size, in bytes, of memory that this process has allocated that cannot be shared with other processes.
        /// </summary>
        ProcessorPrivateBytes = 0x2,
        /// <summary>
        /// Virtual Bytes is the current size, in bytes, of the virtual address space the process is using. Use of virtual address space does not necessarily imply corresponding use of either disk or main memory pages. 
        /// Virtual space is finite, and the process can limit its ability to load libraries.
        /// </summary>
        ProcessorVirtualBytes = 0x4,
        /// <summary>
        /// Working Set is the current size, in bytes, of the Working Set of this process. The Working Set is the set of memory pages touched recently by the threads in the process. 
        /// If free memory in the computer is above a threshold, pages are left in the Working Set of a process even if they are not in use.  
        /// When free memory falls below a threshold, pages are trimmed from Working Sets. If they are needed they will then be soft-faulted back into the Working Set before leaving main memory.
        /// </summary>
        ProcessorWorkingSet = 0x8,
        /// <summary>
        /// % Time in GC is the percentage of elapsed time that was spent in performing a garbage collection (GC) since the last GC cycle. 
        /// This counter is usually an indicator of the work done by the Garbage Collector on behalf of the application to collect and compact memory. 
        /// This counter is updated only at the end of every GC and the counter value reflects the last observed value; its not an average.
        /// </summary>
        GCPctTime = 0x10,
        /// <summary>
        /// This counter is the sum of four other counters; Gen 0 Heap Size; Gen 1 Heap Size; Gen 2 Heap Size and the Large Object Heap Size. This counter indicates the current memory allocated in bytes on the GC Heaps.
        /// </summary>
        GCBytesInAllHeaps = 0x20,
        /// <summary>
        /// This counter displays the maximum bytes that can be allocated in generation 0 (Gen 0); its does not indicate the current number of bytes allocated in Gen 0. 
        /// A Gen 0 GC is triggered when the allocations since the last GC exceed this size. The Gen 0 size is tuned by the Garbage Collector and can change during the execution of the application. 
        /// At the end of a Gen 0 collection the size of the Gen 0 heap is infact 0 bytes; this counter displays the size (in bytes) of allocations that would trigger the next Gen 0 GC. 
        /// This counter is updated at the end of a GC; its not updated on every allocation.
        /// </summary>
        GCGen0HeapSize = 0x40,
        /// <summary>
        /// This counter displays the current number of bytes in generation 1 (Gen 1); this counter does not display the maximum size of Gen 1. 
        /// Objects are not directly allocated in this generation; they are promoted from previous Gen 0 GCs. This counter is updated at the end of a GC; its not updated on every allocation.
        /// </summary>
        GCGen1HeapSize = 0x80,
        /// <summary>
        /// This counter displays the current number of bytes in generation 2 (Gen 2). Objects are not directly allocated in this generation; they are promoted from Gen 1 during previous Gen 1 GCs. 
        /// This counter is updated at the end of a GC; its not updated on every allocation.
        /// </summary>
        GCGen2HeapSize = 0x100,
        /// <summary>
        /// This counter displays the current size of the Large Object Heap in bytes. Objects greater than a threshold are treated as large objects by the Garbage Collector and are directly allocated in a special heap; they are not promoted through the generations. 
        /// In CLR v1.1 and above this threshold is equal to 85000 bytes. This counter is updated at the end of a GC; it’s not updated on every allocation.
        /// </summary>
        GCLargeObjectHeap = 0x200,
        /// <summary>
        /// This counter displays the rate of bytes per second allocated on the GC Heap. 
        /// This counter is updated at the end of every GC; not at each allocation. 
        /// This counter is not an average over time; it displays the difference between the values observed in the last two samples divided by the duration of the sample interval.
        /// </summary>
        GCAllocatedBytesPerSec = 0x400,
        /// <summary>
        /// This counter displays the amount of virtual memory (in bytes) currently committed by the Garbage Collector. (Committed memory is the physical memory for which space has been reserved on the disk paging file).
        /// </summary>
        GCTotalCommitted = 0x800,
        /// <summary>
        /// Page Faults/sec is the rate at which page faults by the threads executing in this process are occurring.  
        /// A page fault occurs when a thread refers to a virtual memory page that is not in its working set in main memory. 
        /// This may not cause the page to be fetched from disk if it is on the standby list and hence already in main memory, or if it is in use by another process with whom the page is shared.
        /// </summary>
        PageFaultsPerSec = 0x1000,
        /// <summary>
        /// The total number of handles currently open by this process. This number is equal to the sum of the handles currently open by each thread in this process.
        /// same as Win32api GetProcessHandleCount
        /// </summary>
        KernelHandleCount = 0x2000,
        /// <summary>
        /// Not a perfcounter: API call GetGuiResources
        /// </summary>
        GDIHandleCount = 0x4000,
        /// <summary>
        /// Not a perfcounter: API call GetGuiResources
        /// </summary>
        UserHandleCount = 0x8000,
        /// <summary>
        /// The number of threads currently active in this process. An instruction is the basic unit of execution in a processor, and a thread is the object that executes instructions. Every running process has at least one thread.
        /// </summary>
        ThreadCount = 0x10000,
    }

    /// <summary>
    /// Specify a user override to the current settings. Does not add new counters: just change threshold
    /// </summary>
    [Serializable]
    public class PerfCounterOverrideThreshold
    {
        public PerfCounterType perfCounterType;
        public float regressionThreshold;
    }
    public class PerfCounterData
    {
        /// <summary>
        /// these are used to provider interactive user counters from which to choose in VSIX to drive graph
        /// typically, user will select only 1 or 2 else the graph is too busy (and different scales)
        /// </summary>
        private static readonly List<PerfCounterData> _lstPerfCounterDefinitions = new List<PerfCounterData>()
        {
            {new PerfCounterData(PerfCounterType.GCBytesInAllHeaps, ".NET CLR Memory","# Bytes in all Heaps","Process ID" ) { IsEnabledForGraph=true, IsEnabledForMeasurement =true, thresholdRegression=1024*1024}},
            {new PerfCounterData(PerfCounterType.ProcessorPrivateBytes, "Process","Private Bytes","ID Process") { IsEnabledForMeasurement=true, thresholdRegression=1024*1024}},
            {new PerfCounterData(PerfCounterType.ProcessorVirtualBytes, "Process","Virtual Bytes","ID Process") { IsEnabledForMeasurement=true, thresholdRegression=1024*1024}},
            {new PerfCounterData(PerfCounterType.ProcessorPctTime, "Process","% Processor Time","ID Process")},
            {new PerfCounterData(PerfCounterType.ProcessorWorkingSet, "Process","Working Set","ID Process")},
            {new PerfCounterData(PerfCounterType.GCPctTime, ".NET CLR Memory","% Time in GC","Process ID")},
            {new PerfCounterData(PerfCounterType.GCAllocatedBytesPerSec, ".NET CLR Memory","Allocated Bytes/sec","Process ID")},
            {new PerfCounterData(PerfCounterType.GCGen0HeapSize, ".NET CLR Memory","Gen 0 heap size","Process ID")},
            {new PerfCounterData(PerfCounterType.GCGen1HeapSize, ".NET CLR Memory","Gen 1 heap size","Process ID")},
            {new PerfCounterData(PerfCounterType.GCGen2HeapSize, ".NET CLR Memory","Gen 2 heap size","Process ID")},
            {new PerfCounterData(PerfCounterType.GCLargeObjectHeap, ".NET CLR Memory","Large Object Heap size","Process ID")},
            {new PerfCounterData(PerfCounterType.PageFaultsPerSec, "Process","Page Faults/sec","ID Process")  },
            {new PerfCounterData(PerfCounterType.ThreadCount, "Process","Thread Count","ID Process")  { IsEnabledForMeasurement=true, thresholdRegression=.5f} },
            {new PerfCounterData(PerfCounterType.KernelHandleCount, "Process","Handle Count","ID Process")  { IsEnabledForMeasurement=true, thresholdRegression=.5f} },
            {new PerfCounterData(PerfCounterType.GDIHandleCount, "GetGuiResources","GDIHandles",string.Empty)  { IsEnabledForMeasurement=true, thresholdRegression=.5f} },
            {new PerfCounterData(PerfCounterType.UserHandleCount, "GetGuiResources","UserHandles",string.Empty)  { IsEnabledForMeasurement=true, thresholdRegression=.5f} },
        };

        public PerfCounterType perfCounterType;
        public string PerfCounterCategory;
        public string PerfCounterName;
        public string PerfCounterInstanceName;
        /// <summary>
        /// true means collect (for stress iterations)
        /// </summary>
        public bool IsEnabledForMeasurement = false;
        /// <summary>
        /// true means collect (for stress iterations)
        /// </summary>
        public bool IsEnabledForGraph = false;
        public Lazy<PerformanceCounter> lazyPerformanceCounter;
        public Process ProcToMonitor;

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
            if (ProcToMonitor.HasExited)
            {
                throw new InvalidOperationException($"Process has exited {ProcToMonitor.ProcessName} PID = {ProcToMonitor.Id}");
            }
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
        /// <summary>
        /// get list of perf counters to measure
        /// </summary>
        /// <param name="processToMonitor"></param>
        /// <param name="IsForStress">For VSIX, some counters aren't leaky, like % CPU</param>
        /// <returns></returns>
        public static List<PerfCounterData> GetPerfCountersToUse(Process processToMonitor, bool IsForStress)
        {
            var lst = new List<PerfCounterData>();
            foreach (var pc in _lstPerfCounterDefinitions.Where(p => !IsForStress || p.IsEnabledForMeasurement))
            {
                var newdata = (PerfCounterData)pc.MemberwiseClone();
                newdata.ProcToMonitor = processToMonitor;
                newdata.ResetCounter();
                lst.Add(newdata);
            }
            return lst;
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
            return $"{perfCounterType} {PerfCounterCategory} {PerfCounterName} {PerfCounterInstanceName} {thresholdRegression:n1} EnabledForMeasure = {IsEnabledForMeasurement}";
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

        public int GetGuiResourcesGDICount()
        {
            return GetGuiResources(ProcToMonitor.Handle, uiFlags: 0);
        }

        public int GetGuiResourcesUserCount()
        {
            return GetGuiResources(ProcToMonitor.Handle, uiFlags: 1);
        }
    }
}
