//Desc: Demonstrate a leak System.Diagnostics.Process

//Include: ..\Util\LeakBaseClass.cs

//Ref: %PerfGraphVSIX%
//Pragma: GenerateInMemory = False
//Pragma: UseCSC = true
//Pragma: showwarnings = true
//Pragma: verbose = false

////Ref: c:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll


//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.ComponentModel.Composition.dll


using System;
using System.Linq;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.Test.Stress;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Reflection;
using System.Xml;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using System.IO;

namespace MyCodeToExecute
{
    public class MyProcessLeakClass : LeakBaseClass
    {
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyProcessLeakClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 37, Sensitivity: 2.5, delayBetweenIterationsMsec: 0);
            }
        }
        public MyProcessLeakClass(object[] args) : base(args)
        {
            //ShowUI = false;
            //NumIterationsBeforeTotalToTakeBaselineSnapshot = 0;
            SecsBetweenIterations = 0.1;
        }
        public override async Task DoInitializeAsync()
        {
            await Task.Yield();
        }
        // https://referencesource.microsoft.com/#System/services/monitoring/system/diagnosticts/Process.cs,f8b2e604d6f1fe04
        List<Process> _lstProcesses = new();
        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            await Task.Yield();
//            var process = Process.Start("notepad"); //leaks HandleCount (not UserHandles)
            var process = Process.GetCurrentProcess();
            var m = process.MainModule.FileName;
            process.Refresh();
            _lstProcesses.Add(process);
            
//            process.Dispose();
        }
        public override async Task DoCleanupAsync()
        {
            await Task.Yield();
            foreach (var p in _lstProcesses)
            {
                try
                {
  //                  p.CloseMainWindow();
                }
                catch (Exception)
                {
                }
                p.Dispose();
            }
        }

    }
}
