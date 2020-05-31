//Desc: Repeatedly Open/close a folder. Modify the code to point to a folder

//Include: ..\Util\LeakBaseClass.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.VisualStudio.Shell;

using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace MyCodeToExecute
{
    public class MyClass : LeakBaseClass
    {
        public TaskCompletionSource<int> _tcs = new TaskCompletionSource<int>();

        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 3);
            }
        }
        public MyClass(object[] args) : base(args) { }

        public override async Task DoInitializeAsync()
        {
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterOpenFolder += SolutionEvents_OnAfterOpenFolder;
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution += SolutionEvents_OnAfterCloseSolution;
        }

        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            await OpenAFolderAsync();

            await CloseTheFolderAsync();
            await Task.Delay(5000, _CancellationTokenExecuteCode);
        }
        public override async Task DoCleanupAsync()
        {
            await CloseTheSolutionAsync();
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterOpenFolder -= SolutionEvents_OnAfterOpenFolder;
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution -= SolutionEvents_OnAfterCloseSolution;
        }

        async Task OpenAFolderAsync()
        {
            _tcs = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            g_dte.ExecuteCommand("File.OpenFolder", @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln");
            await _tcs.Task;
            if (!_CancellationTokenExecuteCode.IsCancellationRequested)
            {
                await Task.Delay(5000 * DelayMultiplier, _CancellationTokenExecuteCode);
            }
        }

        async Task CloseTheFolderAsync()
        {
            _tcs = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            g_dte.Solution.Close();

            if (!_CancellationTokenExecuteCode.IsCancellationRequested)
            {
                await Task.Delay(5000 * DelayMultiplier, _CancellationTokenExecuteCode);
            }
        }

        private void SolutionEvents_OnAfterOpenFolder(object sender, EventArgs e)
        {
//           _logger.LogMessage("SolutionEvents_OnAfterOpenFolder");
            _tcs.TrySetResult(0);
        }

        //private void SolutionEvents_OnAfterCloseFolder(object sender, EventArgs e)
        //{
        //   _logger.LogMessage("SolutionEvents_OnAfterCloseFolder");
        //    _tcs.TrySetResult(0);
        //}

        private void SolutionEvents_OnAfterCloseSolution(object sender, EventArgs e)
        {
//           _logger.LogMessage("SolutionEvents_OnAfterCloseSolution");
            _tcs.TrySetResult(0);
        }
    }
}
