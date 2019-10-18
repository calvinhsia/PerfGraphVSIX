﻿
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


using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.VisualStudio.Shell;

using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace MyCustomCode
{
    public class MyClass
    {
        string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
        int NumberOfIterations = 70;
        int DelayMultiplier = 1; // increase this when running under e.g. MemSpect
        int nTimes = 0;
        TaskCompletionSource<int> _tcs;
        CancellationToken _CancellationToken;
        JoinableTask _tskDoPerfMonitoring;
        ILogger logger;
        Action<string> actTakeSample;
        public EnvDTE.DTE g_dte;

        public static async Task DoMain(object[] args)
        {
            var oMyClass = new MyClass(args);
            await oMyClass.DoSomeWorkAsync();
        }
        public MyClass(object[] args)
        {
            _tcs = new TaskCompletionSource<int>();
            logger = args[0] as ILogger;
            _CancellationToken = (CancellationToken)args[1]; // value type
            g_dte = args[2] as EnvDTE.DTE;
            actTakeSample = args[3] as Action<string>;
        }
        private async Task DoSomeWorkAsync()
        {
            //            logger.LogMessage("in DoSomeWorkAsync");
            try
            {
                if (nTimes++ == 0)
                {
                    logger.LogMessage("Registering solution events");
                    Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete += SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
                    Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution += SolutionEvents_OnAfterCloseSolution;
                    //await OpenASolutionAsync();
                    //foreach (EnvDTE.Window win in g_dte.Windows)
                    //{
                    //    logger.LogMessage("Win " + win.Kind + " " + win.ToString());
                    //    if (win.Kind == "Document") // "Tool"
                    //    {
                    //        logger.LogMessage("   " + win.Document.Name);
                    //    }
                    //}
                    //g_dte.ExecuteCommand("File.OpenFile", @"C:\Users\calvinh\Source\repos\hWndHost\Reflect\Reflect.xaml.cs");
                    //g_dte.ExecuteCommand("File.NewFile", "temp.cs");
                    //System.Windows.Forms.SendKeys.Send("using System;{ENTER}");
                    //await Task.Delay(1000);
                    //System.Windows.Forms.SendKeys.Send("class testing {{}");
                    //await Task.Delay(1000);
                    //Func<Task> undoAll = async () =>
                    //  {
                    //      var done = false;
                    //      logger.LogMessage("Start undo loop");
                    //      while (!done)
                    //      {
                    //          try
                    //          {
                    //              logger.LogMessage(" in undo loop");
                    //              g_dte.ExecuteCommand("Edit.Undo");
                    //              await Task.Delay(100);
                    //          }
                    //          catch (Exception)
                    //          {
                    //              done = true;
                    //              logger.LogMessage("Done undo loop");
                    //          }
                    //      }
                    //  };
                    //await undoAll();
                    //g_dte.ExecuteCommand("File.Close", @"");
                    //await Task.Delay(1000);


                    //var ox = new System.Windows.Window();
                    //var tb = new System.Windows.Controls.TextBox()
                    //{
                    //    AcceptsReturn = true
                    //};
                    //ox.Content = tb;
                    //ox.ShowDialog();
                }
                // Keep in mind that the UI will be unresponsive if you have no await and no main thread idle time

                for (int i = 0; i < NumberOfIterations && !_CancellationToken.IsCancellationRequested; i++)
                {
                    var desc = string.Format("Start of Iter {0}/{1}", i + 1, NumberOfIterations);
                    DoSample(desc);
                    await Task.Delay(1000); // wait one second to allow UI thread to catch  up

                    await OpenASolutionAsync();
                    if (_CancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    await CloseTheSolutionAsync();
                    g_dte.ExecuteCommand("File.CloseSolution", @"");
                    await Task.Delay(5000);
                    //                    logger.LogMessage("End of Iter {0}", i);
                }
                var msg = "Cancelled Code Execution";
                if (!_CancellationToken.IsCancellationRequested)
                {
                    msg = string.Format("Done all {0} iterations", NumberOfIterations);
                }
                DoSample(msg);
            }
            catch (Exception ex)
            {
                logger.LogMessage(ex.ToString());
            }
            finally
            {
                logger.LogMessage("UnRegistering solution events");
                Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete -= SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
                Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution -= SolutionEvents_OnAfterCloseSolution;
            }
        }

        void DoSample(string desc)
        {
            if (actTakeSample != null)
            {
                actTakeSample(desc);
            }
        }

        async Task OpenASolutionAsync()
        {
            _tcs = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            g_dte.Solution.Open(SolutionToLoad);
            await _tcs.Task;
            if (!_CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(5000 * DelayMultiplier);
            }
        }

        async Task CloseTheSolutionAsync()
        {
            _tcs = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            g_dte.Solution.Close();
            if (!_CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(5000 * DelayMultiplier);
            }
        }

        private void SolutionEvents_OnAfterCloseSolution(object sender, EventArgs e)
        {
            //            logger.LogMessage("SolutionEvents_OnAfterCloseSolution");
            _tcs.TrySetResult(0);
        }

        private void SolutionEvents_OnAfterBackgroundSolutionLoadComplete(object sender, EventArgs e)
        {
            logger.LogMessage("SolutionEvents_OnAfterBackgroundSolutionLoadComplete");
            _tcs.TrySetResult(0);
        }
    }
}
