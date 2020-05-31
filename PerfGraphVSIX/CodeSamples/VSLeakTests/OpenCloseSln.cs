//Desc: Repeatedly open/close a solution to find leaks. Modify the code to point to a solution

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
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 7);
            }
        }
        public MyClass(object[] args) : base(args) { }

        public override async Task DoInitializeAsync()
        {
            await Task.Yield();
        }

        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            await OpenASolutionAsync();

            await CloseTheSolutionAsync();
        }
    }
}
