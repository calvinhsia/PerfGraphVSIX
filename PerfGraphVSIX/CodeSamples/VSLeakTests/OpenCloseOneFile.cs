//Desc: Repeatedly open/close one file to find leaks. Modify the code to point to a file

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
                await oMyClass.DoTheTest(numIterations: 3);
            }
        }
        public MyClass(object[] args) : base(args) { }

        public override async Task DoInitializeAsync()
        {
            await OpenASolutionAsync();
        }

        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            _dte.ExecuteCommand("File.OpenFile", @"C:\Users\calvinh\Source\repos\hWndHost\Reflect\Reflect.xaml.cs");

            await Task.Delay(3000, _CancellationTokenExecuteCode);

            _dte.ExecuteCommand("File.Close", @"");
        }
        public virtual async Task DoCleanupAsync()
        {
            await CloseTheSolutionAsync();
        }
    }
}

