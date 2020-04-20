//Include: ExecCodeBase.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell;

using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using System.Collections.Generic;

namespace MyCodeToExecute
{
    public class MyClass : BaseExecCodeClass
    {
        IntPtr _heap;
        List<IntPtr> _lstAllocs = new List<IntPtr>();
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args) { ShowUI = false })
            {
                int nIter = 7;
                try
                {
                    await oMyClass.DoTheTest(numIterations: nIter);
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    //oMyClass.logger.LogMessage("Destoy heap");
                    //Heap.HeapDestroy(oMyClass._heap);
                    foreach (var alloc in oMyClass._lstAllocs)
                    {
                        Heap.HeapFree(oMyClass._heap, 0, alloc);
                    }
                    oMyClass.logger.LogMessage("heap frees");
                }
            }
        }
        public MyClass(object[] args) : base(args)
        {
            _heap = Heap.GetProcessHeap();
            logger.LogMessage("GetProcess heap");
        }

        public override async Task DoInitializeAsync()
        {
            await Task.Yield();
        }

        public override async Task DoIterationBodyAsync()
        {
            var x = Heap.HeapAlloc(_heap, 0, 1048576 * 1);
            _lstAllocs.Add(x);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
    public class Heap
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetProcessHeap();
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr HeapCreate(uint flOptions, int dwInitialsize, int dwMaximumSize);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr HeapAlloc(IntPtr hHeap, uint dwFlags, int dwSize);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool HeapFree(IntPtr hHeap, uint dwFlags, IntPtr lpMem);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool HeapDestroy(IntPtr hHeap);
    }
}
