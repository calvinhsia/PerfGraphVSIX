using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerfGraphVSIX
{
    public interface IStressUtil
    {
        Task DoSampleAsync(MeasurementHolder measurementHolder, SampleType sample, string descriptionOverride = "");

        Task CreateDumpAsync(int pid, MemoryAnalysisType memoryAnalysisType, string desc);
    }

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

}
