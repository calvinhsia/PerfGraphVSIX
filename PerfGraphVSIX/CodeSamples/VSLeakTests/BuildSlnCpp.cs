//Desc: Repeatedly Build a CPP solution to find leaks. Modify the code to point to a CPP solution
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
                await oMyClass.DoTheTest(numIterations: 17, Sensitivity:1);
            }
        }
        public MyClass(object[] args) : base(args) { }

        public override async Task DoInitializeAsync()
        {
            /// Note: replace this with an existing file on your machine!
            await OpenASolutionAsync(@"C:\Users\calvinh\Source\repos\ReflectCPP\ReflectCpp\ReflectCpp.sln");
        }

        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            _tcsProject = new TaskCompletionSource<int>();
            _dte.ExecuteCommand("Build.CleanSolution", @"");
            await _tcsProject.Task;

            //                   _logger.LogMessage("Build.BuildSolution");
            _tcsProject = new TaskCompletionSource<int>();
            _dte.ExecuteCommand("Build.BuildSolution", @"");
            await _tcsProject.Task;
        }
        public override async Task DoCleanupAsync()
        {
            await CloseTheSolutionAsync();
        }
    }
}
