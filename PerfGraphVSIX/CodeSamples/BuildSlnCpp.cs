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
                await oMyClass.DoTheTest(numIterations: 17, Sensitivity:1);
            }
        }
        public MyClass(object[] args) : base(args) { }

        public override async Task DoInitializeAsync()
        {
            SolutionToLoad = @"C:\Users\calvinh\Source\repos\ReflectCPP\ReflectCpp\ReflectCpp.sln";
            await OpenASolutionAsync();
        }

        public override async Task DoIterationBodyAsync()
        {
            _tcsProject = new TaskCompletionSource<int>();
            g_dte.ExecuteCommand("Build.CleanSolution", @"");
            await _tcsProject.Task;

            //                    logger.LogMessage("Build.BuildSolution");
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
