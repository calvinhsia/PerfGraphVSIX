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
            await Task.Yield();
            SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
        }

        public override async Task DoIterationBodyAsync()
        {
            await OpenASolutionAsync();

            await CloseTheSolutionAsync();
        }
    }
}
