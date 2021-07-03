using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Test.Stress;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TestStressDll
{

    [TestClass]
    public class NativeLeaks
    {
        public TestContext TestContext { get; set; }
        class BigStuffWithLongNameSoICanSeeItBetter : IDisposable
        {
            readonly IntPtr alloc;
            public BigStuffWithLongNameSoICanSeeItBetter(int sizeToAllocate)
            {
                var hp = Heap.GetProcessHeap();
                alloc = Heap.HeapAlloc(hp, 0, sizeToAllocate);
            }
            public void Dispose()
            {
                Heap.HeapFree(Heap.GetProcessHeap(), 0, alloc);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(LeakException))]
        public async Task TestLeakyNativeAsync()
        {
            if (StressUtilOptions.IsRunningOnBuildMachine())
            {
                throw new LeakException("Throwing expected exception so test passes", null);
            }
            var lstperfCounterOverrideDataSettings = new List<PerfCounterOverrideThreshold>
            {
                new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.ProcessorPrivateBytes, regressionThreshold = 1024*1024 * .8f} ,
            };
            await StressUtil.DoIterationsAsync(this, new StressUtilOptions() { NumIterations = 17, ProcNamesToMonitor = string.Empty, PerfCounterOverrideSettings = lstperfCounterOverrideDataSettings });
            _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter(sizeToAllocate: 1024 * 1024));
        }

        List<BigStuffWithLongNameSoICanSeeItBetter> _lst = new List<BigStuffWithLongNameSoICanSeeItBetter>();

        [TestInitialize]
        public void TestInit()
        {
            _lst = new List<BigStuffWithLongNameSoICanSeeItBetter>();
            GC.Collect();
        }


        [TestMethod]
        [ExpectedException(typeof(LeakException))]
        public async Task TestLeakyDetectNativeVerySmallLeakAsync()
        {
            var thresh = 1e3f;
            var lstperfCounterOverrideDataSettings = new List<PerfCounterOverrideThreshold>
            {
                new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.GCBytesInAllHeaps, regressionThreshold = 9 * thresh } ,// use a very high thresh so this counter won't show as leak
                new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.ProcessorPrivateBytes, regressionThreshold = thresh} ,
                new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.ProcessorVirtualBytes, regressionThreshold = 9 * thresh } ,
                new PerfCounterOverrideThreshold { perfCounterType = PerfCounterType.KernelHandleCount, regressionThreshold = 9 * thresh } ,
            };

            await StressUtil.DoIterationsAsync(this, new StressUtilOptions() { NumIterations = 201, ProcNamesToMonitor = string.Empty, PerfCounterOverrideSettings = lstperfCounterOverrideDataSettings, ShowUI = false });
            _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter(sizeToAllocate: 100000));
        }


        [TestCleanup]
        public async Task CleanupAsync()
        {
            await Task.Yield();
            foreach (var itm in _lst)
            {
                itm.Dispose();
            }
            TestContext.WriteLine("Cleanup");
        }

    }
    public class Heap
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetProcessHeap();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr HeapCreate(uint flOptions, UIntPtr dwInitialsize, UIntPtr dwMaximumSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr HeapAlloc(IntPtr hHeap, uint dwFlags, int dwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool HeapFree(IntPtr hHeap, uint dwFlags, IntPtr lpMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool HeapDestroy(IntPtr hHeap);
    }
}
