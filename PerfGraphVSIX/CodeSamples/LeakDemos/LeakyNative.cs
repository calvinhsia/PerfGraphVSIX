//Include: ..\Util\LeakBaseClass.cs
//Desc: Demonstrate leak detection of native heap memory

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
/*
https://devdiv.visualstudio.com/DevDiv/_wiki/wikis/DevDiv.wiki/24637/Diagnose-native-heap-memory-leaks
        bool DoRegImport(string regFileData)
        {
            var tmpRegFileName = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".reg");
            System.IO.File.WriteAllText(tmpRegFileName, regFileData);

            var sb = new StringBuilder();
            logger.LogMessage(System.IO.File.ReadAllText(tmpRegFileName));
            using (var proc = Utility.CreateProcess(@"reg", $"import {tmpRegFileName}", sb))
            {
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
            }
            logger.LogMessage(sb.ToString());
            var result = (sb.ToString().Contains("The operation completed successfully."));
            return result;
        }
        void DoTestRegImports()
        {
            logger.LogMessage(" Temporary: We want to track 48 byte allocation call stacks");
            var regFileData = @"Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\VisualStudio\RemoteSettings\LocalTest]

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\VisualStudio\RemoteSettings\LocalTest\HeapAllocs]
""HeapAllocStacksEnabled""=dword:00000001
""StackFrames""=dword:00000020
""HeapAllocMinValue""=dword:00100000
""HeapAllocSizes""=""48:1000000""
""HeapAllocStackMaxSize""=dword:00800000

";
            DoRegImport(regFileData);

        }
 */
namespace MyCodeToExecute
{

    public class MyClass : LeakBaseClass
    {
        class BigStuffWithLongNameSoICanSeeItBetter : IDisposable
        {
            IntPtr alloc;
            IntPtr alloc2;

            public BigStuffWithLongNameSoICanSeeItBetter(int i)
            {
                var hp = Heap.GetProcessHeap();
                alloc = Heap.HeapAlloc(hp, 0, 1024 * 1024);
                alloc2 = Marshal.AllocHGlobal(1024 * 1024);
            }
            public void Dispose()
            {
                Heap.HeapFree(Heap.GetProcessHeap(), 0, alloc);
                Marshal.FreeHGlobal(alloc2);
            }
        }

        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 17, Sensitivity: 10);
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

        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
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
