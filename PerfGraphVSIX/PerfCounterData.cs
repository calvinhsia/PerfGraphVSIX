using System;
using System.Diagnostics;
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

        public float LastValue;
        public float ReadNextValue()
        {
            float retVal;
            switch (perfCounterType)
            {
                case PerfCounterType.UserHandleCount:
                    retVal = GetGuiResourcesGDICount();
                    break;
                case PerfCounterType.GDIHandleCount:
                    retVal = GetGuiResourcesUserCount();
                    break;
                default:
                    retVal = lazyPerformanceCounter.Value.NextValue();
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
            this.lazyPerformanceCounter = new Lazy<PerformanceCounter>(() =>
            {
                PerformanceCounter pc = null;
                var vsPid = Process.GetCurrentProcess().Id;
                var category = new PerformanceCounterCategory(PerfCounterCategory);

                foreach (var instanceName in category.GetInstanceNames()) // exception if you're not admin or "Performance Monitor Users" group (must re-login)
                {
                    using (var cntr = new PerformanceCounter(category.CategoryName, PerfCounterInstanceName, instanceName, readOnly: true))
                    {
                        try
                        {
                            var val = (int)cntr.NextValue();
                            if (val == vsPid)
                            {
                                pc = new PerformanceCounter(PerfCounterCategory, PerfCounterName, instanceName);
                                break;
                            }
                        }
                        catch (Exception)
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
            return GetGuiResources(Process.GetCurrentProcess().Handle, 0);
        }

        public static int GetGuiResourcesUserCount()
        {
            return GetGuiResources(Process.GetCurrentProcess().Handle, 1);
        }
    }
}
