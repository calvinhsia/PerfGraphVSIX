
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
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Framework.dll

//Ref:"%VSRoot%\Common7\IDE\PublicAssemblies\envdte.dll"


//Ref: %PerfGraphVSIX%


////Ref: c:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll


//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationFramework.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationCore.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\WindowsBase.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Xaml.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Core.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Windows.Forms.dll


using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.Test.Stress;
using Microsoft.VisualStudio.Shell;
using EnvDTE;

using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using System.IO;

namespace MyCodeToExecute
{
    public class BaseExecCodeClass : IDisposable
    {
        public string FileToExecute;
        public ILogger logger;
        public CancellationToken _CancellationTokenExecuteCode;
        public EnvDTE.DTE g_dte;

        public bool ShowUI = true;

        public int DelayMultiplier = 1; // increase this when running under e.g. MemSpect
        public int NumIterationsBeforeTotalToTakeBaselineSnapshot = 4;
        public string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln"; //could be folder to open too

        public BuildEvents BuildEvents;
        public DebuggerEvents DebuggerEvents;
        public ITakeSample itakeSample;

        public TaskCompletionSource<int> _tcsSolution = new TaskCompletionSource<int>();
        public TaskCompletionSource<int> _tcsProject = new TaskCompletionSource<int>();
        public TaskCompletionSource<int> _tcsDebug = new TaskCompletionSource<int>();
        public string TestName { get { return Path.GetFileNameWithoutExtension(FileToExecute); } }

        public BaseExecCodeClass(object[] args)
        {
            FileToExecute = args[0] as string;
            logger = args[1] as ILogger;
            _CancellationTokenExecuteCode = (CancellationToken)args[2]; // value type
            itakeSample = args[3] as ITakeSample;
            g_dte = args[4] as EnvDTE.DTE;

            //logger.LogMessage("Registering events ");

            BuildEvents = g_dte.Events.BuildEvents;
            DebuggerEvents = g_dte.Events.DebuggerEvents;

            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete += SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution += SolutionEvents_OnAfterCloseSolution;
            BuildEvents.OnBuildBegin += BuildEvents_OnBuildBegin;
            BuildEvents.OnBuildDone += BuildEvents_OnBuildDone;

            DebuggerEvents.OnEnterRunMode += DebuggerEvents_OnEnterRunMode;
            DebuggerEvents.OnEnterDesignMode += DebuggerEvents_OnEnterDesignMode;
        }

        public void UnregisterEvents()
        {
            //logger.LogMessage("UnRegistering events");
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete -= SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution -= SolutionEvents_OnAfterCloseSolution;
            BuildEvents.OnBuildBegin -= BuildEvents_OnBuildBegin;
            BuildEvents.OnBuildDone -= BuildEvents_OnBuildDone;

            DebuggerEvents.OnEnterRunMode -= DebuggerEvents_OnEnterRunMode;
            DebuggerEvents.OnEnterDesignMode -= DebuggerEvents_OnEnterDesignMode;

            BuildEvents = null;
            DebuggerEvents = null;

        }

        public virtual async Task DoInitializeAsync()
        {
            await Task.Yield();
        }
        public virtual async Task DoIterationBodyAsync()
        {
            await Task.Yield();
        }

        public virtual async Task DoCleanupAsync()
        {
            await Task.Yield();
        }

        public virtual async Task DoTheTest(int numIterations, double Sensitivity = 1.0f, int delayBetweenIterationsMsec = 1000)
        {
            await DoInitializeAsync();
            await IterateCode(numIterations, Sensitivity, delayBetweenIterationsMsec);
            await DoCleanupAsync();
        }

        public async Task IterateCode(int numIterations, double Sensitivity, int delayBetweenIterationsMsec)
        {
            try
            {
                using (var measurementHolder = new MeasurementHolder(
                    TestName,
                    new StressUtilOptions()
                    {
                        NumIterations = numIterations,
                        ProcNamesToMonitor = string.Empty,
                        ShowUI = this.ShowUI,
                        logger = logger,
                        Sensitivity = Sensitivity,
                        //actExecuteAfterEveryIterationAsync = async (nIter, mHolder) => // uncomment to suppress dump taking/processing.
                        //{
                        //    await Task.Yield();
                        //    return false;
                        //},
                        lstPerfCountersToUse = PerfCounterData.GetPerfCountersToUse(System.Diagnostics.Process.GetCurrentProcess(), IsForStress: false)
                    },
                    SampleType.SampleTypeIteration))
                {
                    var baseDumpFileName = string.Empty;
                    for (int iteration = 0; iteration < numIterations && !_CancellationTokenExecuteCode.IsCancellationRequested; iteration++)
                    {
                        await DoIterationBodyAsync();
                        await Task.Delay(TimeSpan.FromMilliseconds(delayBetweenIterationsMsec * DelayMultiplier));
                        var desc = string.Format("Iter {0}/{1}", iteration + 1, numIterations);
                        // we need to go thru the extension to get the measurement, so the vsix graph updates and adds to log
                        await itakeSample.DoSampleAsync(measurementHolder, desc);

                        if (_CancellationTokenExecuteCode.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                    if (!_CancellationTokenExecuteCode.IsCancellationRequested)
                    {
                        logger.LogMessage(string.Format("Done all {0} iterations", numIterations));
                    }
                    else
                    {
                        logger.LogMessage("Cancelled Code Execution");
                    }
                }
            }
            catch (LeakException)
            {

            }
            catch (OperationCanceledException)
            {
                logger.LogMessage("Cancelled");
            }
            catch (Exception ex)
            {
                logger.LogMessage(ex.ToString());
            }
        }

        public void Dispose()
        {
            UnregisterEvents();
        }

        public async Task OpenASolutionAsync(int delayAfterOpen = 5)
        {
            _tcsSolution = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            g_dte.Solution.Open(SolutionToLoad);
            await _tcsSolution.Task;
            if (!_CancellationTokenExecuteCode.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(delayAfterOpen * DelayMultiplier), _CancellationTokenExecuteCode);
            }
        }

        public async Task CloseTheSolutionAsync(int delayAfterClose = 0)
        {
            _tcsSolution = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            g_dte.Solution.Close();
            if (!_CancellationTokenExecuteCode.IsCancellationRequested && delayAfterClose > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(delayAfterClose * DelayMultiplier), _CancellationTokenExecuteCode);
            }
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

        void BuildEvents_OnBuildBegin(vsBuildScope Scope, vsBuildAction Action)
        {
            //            logger.LogMessage("BuildEvents_OnBuildBegin " + Scope.ToString() + Action.ToString());
        }
        void BuildEvents_OnBuildDone(vsBuildScope Scope, vsBuildAction Action)
        {
            //            logger.LogMessage("BuildEvents_OnBuildDone " + Scope.ToString() + Action.ToString());
            _tcsProject.TrySetResult(0);
        }

        void DebuggerEvents_OnEnterRunMode(dbgEventReason Reason)
        {
            //logger.LogMessage("DebuggerEvents_OnEnterRunMode " + Reason.ToString()); // dbgEventReasonLaunchProgram
            _tcsDebug.TrySetResult(0);
        }
        void DebuggerEvents_OnEnterDesignMode(dbgEventReason Reason)
        {
            //logger.LogMessage("DebuggerEvents_OnEnterDesignMode " + Reason.ToString()); //dbgEventReasonStopDebugging
            _tcsDebug.TrySetResult(0);
        }

    }
}


/*
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


 *  * */
