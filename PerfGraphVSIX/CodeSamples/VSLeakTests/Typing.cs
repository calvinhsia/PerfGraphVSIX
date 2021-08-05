//Desc: Repeatedly navigate in a file to find leaks. Modify the code to point to a file
//Include: ..\Util\LeakBaseClass.cs


using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;

using Microsoft.VisualStudio.Shell;

using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.Test.Stress;

namespace MyCodeToExecute
{
    public class MyClass : LeakBaseClass
    {
        KeyboardAutomationService kb = new KeyboardAutomationService();
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
            await OpenASolutionAsync(@"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln");
            /// Note: replace this with an existing file on your machine!
            _dte.ExecuteCommand("File.OpenFile", @"C:\Users\calvinh\Source\repos\hWndHost\Reflect\Reflect.xaml.cs");
            await Task.Delay(TimeSpan.FromSeconds(1 * DelayMultiplier));
        }

        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            // note: sending keystrokes works by sending to current active Window: if you click on Outlook, the keystrokes will be sent there.
            //kb.TypeKey(KeyboardModifier.Alt, new[] { 't', 'o' }); // ToolsOptions
            //await Task.Delay(TimeSpan.FromMilliseconds(1500));
            //kb.TypeKey(KeyboardKey.Escape);
            //await Task.Delay(TimeSpan.FromMilliseconds(1500));
            //            _dte.ExecuteCommand("Edit.DocumentStart", @"");
            kb.TypeKey(KeyboardModifier.Control, KeyboardKey.Home);
            for (int i = 0; i < 33; i++)
            {
                kb.TypeKey(KeyboardKey.Down);
            }
            kb.TypeKey(KeyboardKey.Enter);
            kb.TypeKey(KeyboardKey.Up);
            for (int i = 0; i < 10; i++)
            {
                kb.TypeText($"\"{iteration}/{i}\"."); //  "1".ToString();
                await Task.Delay(TimeSpan.FromMilliseconds(500)); // for intellisense to 
                kb.TypeKey('t');
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                kb.TypeKey('o');
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                kb.TypeKey('s');
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                kb.TypeKey(KeyboardKey.Tab);
                kb.TypeText("();");
                kb.TypeKey(KeyboardKey.Enter);
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }



            //int nScroll = 50;
            //_dte.ExecuteCommand("Edit.DocumentStart", @"");
            //for (int r = 0; r < nScroll && !_CancellationTokenExecuteCode.IsCancellationRequested; r++)
            //{
            //    //                        _dte.ExecuteCommand("Edit.CharRight", @"");
            //    _dte.ExecuteCommand("Edit.ScrollPageDown", @"");

            //    await Task.Delay(TimeSpan.FromMilliseconds(1000), _CancellationTokenExecuteCode); // wait to allow UI thread to catch  up
            //}

            //for (int r = 0; r < nScroll && !_CancellationTokenExecuteCode.IsCancellationRequested; r++)
            //{
            //    //                        _dte.ExecuteCommand("Edit.CharRight", @"");
            //    _dte.ExecuteCommand("Edit.ScrollPageUp", @"");

            //    await Task.Delay(TimeSpan.FromMilliseconds(1000), _CancellationTokenExecuteCode); // wait to allow UI thread to catch  up
            //}
        }

        public override async Task DoCleanupAsync()
        {
            _dte.ActiveWindow.Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
            await CloseTheSolutionAsync();
        }
    }
}
