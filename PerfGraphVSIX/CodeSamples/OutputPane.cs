//Include: ExecCodeBase.cs
// this will demonstate leak detection
// 
//Ref: MapFileDict.dll

using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using System.Collections;
using System.Collections.Generic;

using Microsoft.VisualStudio.Shell;
using EnvDTE;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Shell.Interop;

namespace MyCodeToExecute
{

    public class MyClass : BaseExecCodeClass
    {
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 5);
            }
        }
        IVsOutputWindowPane m_pane;
        public MyClass(object[] args) : base(args)
        {
            ShowUI = false;
            NumIterationsBeforeTotalToTakeBaselineSnapshot = 0;
        }
        Guid guidPane = new Guid("{CEEAB38D-8BC4-4675-9DFD-993BBE9996A5}");
        Guid guidDebugOutputPane = new Guid("FC076020-078A-11D1-A7DF-00A0C9110051");

        public override async Task DoInitializeAsync()
        {
            // this is a sample showing how to get VS Services (see base class for samples)
            // you can add ref to a DLL if needed, and add Using's if needed
            // if you're outputing to the OutputWindow, be aware that the OutputPanes are editor instances, which will
            // look like a leak as they accumulate data.
            IVsOutputWindow outputWindow = await asyncServiceProvider.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
            var crPane = outputWindow.CreatePane(
                ref guidPane,
                "PerfGraphVSIX",
                fInitVisible: 1,
                fClearWithSolution: 0);
            outputWindow.GetPane(ref guidPane, out m_pane);
            m_pane.Clear();
            logger.LogMessage(string.Format("got output Window CreatePane={0} OutputWindow = {1}  Pane {2}", crPane, outputWindow, m_pane));
        }
        public override async Task DoIterationBodyAsync(int iteration, CancellationToken token)
        {
            await Task.Yield();
            var numPerIter = 3;
            for (int i = 0; i < numPerIter; i++)
            {
                m_pane.OutputString(string.Format(" test {0}  {1}\n", i, DateTime.Now));
                //                window.
            }
        }
        public override async Task DoCleanupAsync()
        {
            await Task.Yield();
        }
    }
}
