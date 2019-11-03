
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
using EnvDTE;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace MyCustomCode
{
    public class MyClass
    {
        string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";
        int NumberOfIterations = 97;
        int DelayMultiplier = 1; // increase this when running under e.g. MemSpect
        int nTimes = 0;
        TaskCompletionSource<int> _tcsSolution;
        TaskCompletionSource<int> _tcsProject;
        CancellationToken _CancellationTokenExecuteCode;
        JoinableTask _tskDoPerfMonitoring;
        ILogger logger;
        Action<string> actTakeSample;
        public EnvDTE.DTE g_dte;
        BuildEvents BuildEvents = null;
        public static async Task DoMain(object[] args)
        {
            var oMyClass = new MyClass(args);
            await oMyClass.DoSomeWorkAsync();
        }
        public MyClass(object[] args)
        {
            _tcsSolution = new TaskCompletionSource<int>();
            logger = args[0] as ILogger;
            _CancellationTokenExecuteCode = (CancellationToken)args[1]; // value type
            g_dte = args[2] as EnvDTE.DTE;
            BuildEvents = g_dte.Events.BuildEvents;
            actTakeSample = args[3] as Action<string>;
        }
        private async Task DoSomeWorkAsync()
        {
            //            logger.LogMessage("in DoSomeWorkAsync");
            // Build.BuildSolution, Build.CleanSolution
            try
            {
                if (nTimes++ == 0)
                {
                    logger.LogMessage("Registering solution events");
                    Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete += SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
                    Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution += SolutionEvents_OnAfterCloseSolution;

                    BuildEvents.OnBuildBegin += BuildEvents_OnBuildBegin;
                    BuildEvents.OnBuildDone += BuildEvents_OnBuildDone;


                    await OpenASolutionAsync();
                    await Task.Delay(5000);
                }
                // Keep in mind that the UI will be unresponsive if you have no await and no main thread idle time

                for (int i = 0; i < NumberOfIterations && !_CancellationTokenExecuteCode.IsCancellationRequested; i++)
                {
                    var desc = string.Format("Start of Iter {0}/{1}", i + 1, NumberOfIterations);
                    DoSample(desc);
                    await Task.Delay(1000); // wait one second to allow UI thread to catch  up

                    if (_CancellationTokenExecuteCode.IsCancellationRequested)
                    {
                        break;
                    }
//                    logger.LogMessage("Build.CleanSolution");
                    _tcsProject = new TaskCompletionSource<int>();
                    g_dte.ExecuteCommand("Build.CleanSolution", @"");
                    await _tcsProject.Task;

//                    logger.LogMessage("Build.BuildSolution");
                    _tcsProject = new TaskCompletionSource<int>();
                    g_dte.ExecuteCommand("Build.BuildSolution", @"");
                    await _tcsProject.Task;
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
//                await CloseTheSolutionAsync();
                logger.LogMessage("UnRegistering solution events");
                Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete -= SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
                Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution -= SolutionEvents_OnAfterCloseSolution;
                BuildEvents.OnBuildBegin -= BuildEvents_OnBuildBegin;
                BuildEvents.OnBuildDone -= BuildEvents_OnBuildDone;
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
            _tcsSolution = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            g_dte.Solution.Open(SolutionToLoad);
            await _tcsSolution.Task;
            if (!_CancellationTokenExecuteCode.IsCancellationRequested)
            {
                await Task.Delay(5000 * DelayMultiplier, _CancellationTokenExecuteCode);
            }
        }

        async Task CloseTheSolutionAsync()
        {
            _tcsSolution = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            g_dte.Solution.Close();
            if (!_CancellationTokenExecuteCode.IsCancellationRequested)
            {
                await Task.Delay(5000 * DelayMultiplier, _CancellationTokenExecuteCode);
            }
        }
        void BuildEvents_OnBuildBegin(vsBuildScope Scope, vsBuildAction Action)
        {
//            logger.LogMessage("BuildEvents_OnBuildBegin " + Scope.ToString() + Action.ToString());
        }
        void BuildEvents_OnBuildDone(vsBuildScope Scope, vsBuildAction Action)
        {
//            logger.LogMessage("BuildEvents_OnBuildDone " + Scope.ToString() + Action.ToString());
            _tcsProject.TrySetResult(0);
        }
        private void SolutionEvents_OnAfterCloseSolution(object sender, EventArgs e)
        {
            //            logger.LogMessage("SolutionEvents_OnAfterCloseSolution");
            _tcsSolution.TrySetResult(0);
        }

        private void SolutionEvents_OnAfterBackgroundSolutionLoadComplete(object sender, EventArgs e)
        {
            //logger.LogMessage("SolutionEvents_OnAfterBackgroundSolutionLoadComplete");
            _tcsSolution.TrySetResult(0);
        }
    }
}
