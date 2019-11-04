
// This code will be compiled and run when you hit the ExecCode button. Any error msgs will be shown in the status log control.
// This allows you to create a stress test by repeating some code, while taking measurements between each iteration.

//  Macro substitution: %PerfGraphVSIX% will be changed to the fullpath to PerfGraphVSIX
//                      %VSRoot% will be changed to the fullpath to VS: e.g. "C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview"

//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.8.0.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.10.0.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.11.0.dll
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.12.1.DesignTime.dll"
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.0.DesignTime.dll"
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.8.DesignTime.dll"
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Threading.dll"
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.15.0.dll

//Ref:"%VSRoot%\Common7\IDE\PublicAssemblies\envdte.dll"

//Ref: %PerfGraphVSIX%


////Ref: c:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll


//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationFramework.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationCore.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\WindowsBase.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Xaml.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Windows.Forms.dll
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
        int NumberOfIterations = 7;
 
        public static async Task DoMain(object[] args)
        {
            var oMyClass = new MyClass(args);
            await oMyClass.DoSomeWorkAsync();
        }
        public MyClass(object[] args): base(args)
        {
            SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
        }
        private async Task DoSomeWorkAsync()
        {
            try
            {
                // Keep in mind that the UI will be unresponsive if you have no await and no main thread idle time

                for (int i = 0; i < NumberOfIterations && !_CancellationTokenExecuteCode.IsCancellationRequested; i++)
                {
                    var desc = string.Format("Start of Iter {0}/{1}", i + 1, NumberOfIterations);
                    TakeSample(desc);
                    await Task.Delay(1000); // wait one second to allow UI thread to catch  up
                    await OpenASolutionAsync();
                    if (_CancellationTokenExecuteCode.IsCancellationRequested)
                    {
                        break;
                    }
                    await CloseTheSolutionAsync();
//                    g_dte.ExecuteCommand("File.CloseSolution", @"");
                    await Task.Delay(5000);
                    //                    logger.LogMessage("End of Iter {0}", i);
                }
                var msg = "Cancelled Code Execution";
                if (!_CancellationTokenExecuteCode.IsCancellationRequested)
                {
                    msg = string.Format("Done all {0} iterations", NumberOfIterations);
                }
                DoSample(msg);
            }
            catch (OperationCanceledException ex)
            {
                logger.LogMessage("Cancelled");
            }
            catch (Exception ex)
            {
                logger.LogMessage(ex.ToString());
            }
            finally
            {
                UnregisterEvents();
            }
        }
    }
}
