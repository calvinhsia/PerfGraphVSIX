using Microsoft.VisualStudio.TestTools.UnitTesting;
using StressTestUtility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TestStress
{

    [TestClass]
    public class MyClass
    {
        public TestContext TestContext { get; set; }
        class BigStuffWithLongNameSoICanSeeItBetter : IDisposable
        {
            readonly IntPtr alloc;
            public BigStuffWithLongNameSoICanSeeItBetter()
            {
                var hp = Heap.GetProcessHeap();
                alloc = Heap.HeapAlloc(hp, 0, 1024 * 1024);
            }
            public void Dispose()
            {
                Heap.HeapFree(Heap.GetProcessHeap(), 0, alloc);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(LeakException))]
        public async Task TestLeakyNative()
        {
            await StressUtil.DoIterationsAsync(this, NumIterations: 17, ProcNamesToMonitor: "", ShowUI: false);
            _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter());
        }

        readonly List<BigStuffWithLongNameSoICanSeeItBetter> _lst = new List<BigStuffWithLongNameSoICanSeeItBetter>();

        [TestCleanup]
        public async Task DoCleanupAsync()
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
