//Include: ExecCodeBase.cs


using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;

using Microsoft.VisualStudio.Shell;

using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace MyCodeToExecute
{
    public class MyClass : BaseExecCodeClass
    {
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
            SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
            await OpenASolutionAsync();
            g_dte.ExecuteCommand("File.OpenFile", @"C:\Users\calvinh\Source\repos\hWndHost\Reflect\Reflect.xaml.cs");
            await Task.Delay(TimeSpan.FromSeconds(1 * DelayMultiplier));
        }

        public override async Task DoIterationBodyAsync()
        {
            int nScroll = 50;
            g_dte.ExecuteCommand("Edit.DocumentStart", @"");
            for (int r = 0; r < nScroll && !_CancellationTokenExecuteCode.IsCancellationRequested; r++)
            {
                //                        g_dte.ExecuteCommand("Edit.CharRight", @"");
                g_dte.ExecuteCommand("Edit.ScrollPageDown", @"");

                await Task.Delay(TimeSpan.FromMilliseconds(1000), _CancellationTokenExecuteCode); // wait to allow UI thread to catch  up
            }

            for (int r = 0; r < nScroll && !_CancellationTokenExecuteCode.IsCancellationRequested; r++)
            {
                //                        g_dte.ExecuteCommand("Edit.CharRight", @"");
                g_dte.ExecuteCommand("Edit.ScrollPageUp", @"");

                await Task.Delay(TimeSpan.FromMilliseconds(1000), _CancellationTokenExecuteCode); // wait to allow UI thread to catch  up
            }
        }

        public override async Task DoCleanupAsync()
        {
            await CloseTheSolutionAsync();
        }
    }
}
