//Desc: Repeatedly Build a solution to find leaks. Modify the code to point to a solution
//Include: ..\Util\LeakBaseClass.cs


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
    public class MyClass : LeakBaseClass
    {
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 73);
            }
        }
        public MyClass(object[] args) : base(args) { }

        public override async Task DoInitializeAsync()
        {
            await OpenASolutionAsync();
        }

        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            _tcsProject = new TaskCompletionSource<int>();
            g_dte.ExecuteCommand("Build.CleanSolution", @"");
            await _tcsProject.Task;

            //                   _logger.LogMessage("Build.BuildSolution");
            _tcsProject = new TaskCompletionSource<int>();
            g_dte.ExecuteCommand("Build.BuildSolution", @"");
            await _tcsProject.Task;
        }
        public override async Task DoCleanupAsync()
        {
            await CloseTheSolutionAsync();
        }
    }
}
