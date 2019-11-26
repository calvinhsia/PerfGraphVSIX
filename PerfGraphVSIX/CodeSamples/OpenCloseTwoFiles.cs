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
    public class MyClass : BaseExecCodeClass
    {
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 13);
            }
        }
        public MyClass(object[] args) : base(args) { }

        string file1 = @"C:\Users\calvinh\Source\repos\hWndHost\Reflect\Reflect.xaml.cs"; // 1642 lines
        string file2 = @"C:\Users\calvinh\Source\repos\hWndHost\Fish\FishWindow.xaml.cs"; // 1047 lines

        public override async Task DoInitializeAsync()
        {
            //await OpenASolutionAsync();
            //g_dte.ExecuteCommand("File.OpenFile", file1);
            //await Task.Delay(TimeSpan.FromSeconds(5 * DelayMultiplier), _CancellationTokenExecuteCode); // wait to allow UI thread to catch  up
            //g_dte.ExecuteCommand("File.OpenFile", file2);
            //await Task.Delay(TimeSpan.FromSeconds(5 * DelayMultiplier), _CancellationTokenExecuteCode); // wait to allow UI thread to catch  up
        }

        public override async Task DoIterationBodyAsync()
        {
            g_dte.ExecuteCommand("File.OpenFile", file1);
            await Task.Delay(2000, _CancellationTokenExecuteCode); // wait one second to allow UI thread to catch  up
            g_dte.ExecuteCommand("File.Close", file1);
            await Task.Delay(2000, _CancellationTokenExecuteCode); // wait one second to allow UI thread to catch  up


            g_dte.ExecuteCommand("File.OpenFile", file2);
            await Task.Delay(2000, _CancellationTokenExecuteCode); // wait one second to allow UI thread to catch  up
            g_dte.ExecuteCommand("File.Close", file2);
            await Task.Delay(2000, _CancellationTokenExecuteCode); // wait one second to allow UI thread to catch  up
        }
        public override async Task DoCleanupAsync()
        {
            //            await CloseTheSolutionAsync();
        }
    }
}

