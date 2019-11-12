﻿//Include: ExecCodeBase.cs
// this will demonstate leak detection

using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell;
using EnvDTE;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using HANDLE = System.IntPtr;

// https://blogs.msdn.microsoft.com/calvin_hsia/2010/05/30/managed-code-using-unmanaged-memory-heapcreate-peek-and-poke/

namespace MyCodeToExecute
{

    public class MyClass : BaseExecCodeClass
    {
        class BigStuffWithLongNameSoICanSeeItBetter : IDisposable
        {
            IntPtr alloc;
            public BigStuffWithLongNameSoICanSeeItBetter(int i)
            {
                var hp = Heap.GetProcessHeap();
                alloc = Heap.HeapAlloc(hp, 0, 1024 * 200);
            }
            public void Dispose()
            {
                Heap.HeapFree(Heap.GetProcessHeap(), 0, alloc);
            }
        }

        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 19, Sensitivity: .1);
            }
        }
        string somestring = "somestring1";
        string somestring2 = "somestring2";
        string somestring3 = "somestring3";
        List<BigStuffWithLongNameSoICanSeeItBetter> _lst = new List<BigStuffWithLongNameSoICanSeeItBetter>();
        public MyClass(object[] args) : base(args)
        {
        }

        public override async Task DoInitializeAsync()
        {
            await base.DoInitializeAsync();
        }

        public override async Task DoIterationBodyAsync()
        {
            // to test if your code leaks, put it here. Repeat a lot to magnify the effect
            for (int i = 0; i < 10; i++)
            {
                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter(i));
            }
        }
        public override async Task DoCleanupAsync()
        {
            foreach (var itm in _lst)
            {
                itm.Dispose();
            }
            await base.DoCleanupAsync();
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
