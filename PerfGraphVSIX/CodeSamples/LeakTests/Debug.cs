//Desc: Repeatedly debug . Modify the code to point to a solution
//Include: ExecCodeBase.cs


using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;

using Microsoft.VisualStudio.Shell;
using EnvDTE;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace MyCodeToExecute
{
    public class MyClass : ExecCodeBase
    {
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 17);
            }
        }
        public MyClass(object[] args) : base(args) { }

        public override async Task DoInitializeAsync()
        {
            await OpenASolutionAsync();
            await Task.Delay(TimeSpan.FromSeconds(5 * DelayMultiplier), _CancellationTokenExecuteCode);
        }

        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            _tcsDebug = new TaskCompletionSource<int>();
            g_dte.ExecuteCommand("Debug.Start", @"");
            //await Task.Delay(10000 * DelayMultiplier);
            await _tcsDebug.Task;

            await Task.Delay(TimeSpan.FromSeconds(15 * DelayMultiplier));

            _tcsDebug = new TaskCompletionSource<int>();
            g_dte.ExecuteCommand("Debug.StopDebugging", @"");
            //                    await Task.Delay(10000 * DelayMultiplier);
            await _tcsDebug.Task;
        }
        public override async Task DoCleanupAsync()
        {
            await CloseTheSolutionAsync();
        }
    }
}
