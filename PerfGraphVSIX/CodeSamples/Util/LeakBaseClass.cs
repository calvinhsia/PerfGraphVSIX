﻿//Desc: The base class used for many of the leak samples. Handles iteration and measurement for leaks

//Include: MyCodeBaseClass.cs

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.Test.Stress;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using EnvDTE;

using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using System.IO;

namespace MyCodeToExecute
{
    public class LeakBaseClass : MyCodeBaseClass, IDisposable
    {

        /// <summary>
        /// If true, will show graph of measurements, then launch ClrObjectExplorer automatically, with the String and Type differences text file
        /// False means don't show graph and don't launch ClrObjectExplorer
        /// </summary>
        public bool ShowUI = true;
        public double SecsBetweenIterations = 1;
        public int DelayMultiplier = 1; // increase this when running under e.g. MemSpect
        public int NumIterationsBeforeTotalToTakeBaselineSnapshot = 4;

        public BuildEvents BuildEvents; // need to get ref to these for their lifetime
        public DebuggerEvents DebuggerEvents;// need to get ref to these for their lifetime

        public TaskCompletionSource<int> _tcsSolution = new TaskCompletionSource<int>();
        public TaskCompletionSource<int> _tcsProject = new TaskCompletionSource<int>();
        public TaskCompletionSource<int> _tcsDebug = new TaskCompletionSource<int>();

        public IVsUIShell _vsUIShell;


        public LeakBaseClass(object[] args) : base(args)
        {
            _perfGraphToolWindowControl.TabControl.SelectedIndex = 1; // select graph tab

            BuildEvents = _dte.Events.BuildEvents;
            DebuggerEvents = _dte.Events.DebuggerEvents;

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

        public virtual async Task DoTheTest(int numIterations, double Sensitivity = 1.0f, int delayBetweenIterationsMsec = 0)
        {
            try
            {
                //logger.LogMessage(string.Format("got output Window CreatePane={0} OutputWindow = {1}  Pane {2}", crPane, outputWindow, _OutputPane));

                _vsUIShell = await _asyncServiceProvider.GetServiceAsync(typeof(SVsUIShell)) as IVsUIShell;
                //logger.LogMessage(string.Format("Got vsuishell {0}", _vsUIShell));

                await DoInitializeAsync();
                await IterateCode(numIterations, Sensitivity, delayBetweenIterationsMsec);
            }
            finally
            {
            }
            await DoCleanupAsync();
        }

        public virtual async Task DoInitializeAsync()
        {
            await Task.Yield();
        }
        public virtual async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            await Task.Yield();
        }

        public virtual async Task DoCleanupAsync() // cleanup after all iterations
        {
            await Task.Yield();
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
                        logger = _logger,
                        Sensitivity = Sensitivity,
                        SecsBetweenIterations = SecsBetweenIterations,
                        NumIterationsBeforeTotalToTakeBaselineSnapshot = NumIterationsBeforeTotalToTakeBaselineSnapshot,
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
                        await DoIterationBodyAsync(iteration, _CancellationTokenExecuteCode);
                        await Task.Delay(TimeSpan.FromMilliseconds(delayBetweenIterationsMsec * DelayMultiplier));
                        var desc = string.Format("Iter {0}/{1}", iteration + 1, numIterations);
                        // we need to go thru the extension to get the measurement, so the vsix graph updates and adds to log
                        await _itakeSample.DoSampleAsync(measurementHolder, DoForceGC: true, descriptionOverride: desc);

                        if (_CancellationTokenExecuteCode.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                    if (!_CancellationTokenExecuteCode.IsCancellationRequested)
                    {
                        _logger.LogMessage(string.Format("Done all {0} iterations", numIterations));
                    }
                    else
                    {
                        _logger.LogMessage("Cancelled Code Execution");
                    }
                }
            }
            catch (LeakException)
            {

            }
            catch (OperationCanceledException)
            {
                _logger.LogMessage("Cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogMessage(ex.ToString());
            }
        }

        public void Dispose()
        {
            UnregisterEvents();
        }

        /// <param name="slnFile">Can be a folder too</param>
        public async Task OpenASolutionAsync(string slnFile, int delayAfterOpen = 5)
        {
            if (_dte.Solution != null && _dte.Solution.FullName.ToLower() != slnFile.ToLower())
            {
                if (!File.Exists(slnFile))
                {
                    throw new FileNotFoundException(slnFile);
                }
                _tcsSolution = new TaskCompletionSource<int>();
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _dte.Solution.Open(slnFile);
                await _tcsSolution.Task;
                if (!_CancellationTokenExecuteCode.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delayAfterOpen * DelayMultiplier), _CancellationTokenExecuteCode);
                }
            }
        }

        public async Task CloseTheSolutionAsync(int delayAfterClose = 0)
        {
            _tcsSolution = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _dte.Solution.Close();
            if (!_CancellationTokenExecuteCode.IsCancellationRequested && delayAfterClose > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(delayAfterClose * DelayMultiplier), _CancellationTokenExecuteCode);
            }
        }

        private void SolutionEvents_OnAfterCloseSolution(object sender, EventArgs e)
        {
            //           _logger.LogMessage("SolutionEvents_OnAfterCloseSolution");
            _tcsSolution.TrySetResult(0);
        }

        private void SolutionEvents_OnAfterBackgroundSolutionLoadComplete(object sender, EventArgs e)
        {
            //logger.LogMessage("SolutionEvents_OnAfterBackgroundSolutionLoadComplete");
            _tcsSolution.TrySetResult(0);
        }

        void BuildEvents_OnBuildBegin(vsBuildScope Scope, vsBuildAction Action)
        {
            //           _logger.LogMessage("BuildEvents_OnBuildBegin " + Scope.ToString() + Action.ToString());
        }
        void BuildEvents_OnBuildDone(vsBuildScope Scope, vsBuildAction Action)
        {
            //           _logger.LogMessage("BuildEvents_OnBuildDone " + Scope.ToString() + Action.ToString());
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
        bool stopIter = false;
        public async Task IterateSolutionItemsAsync(Func<Project, ProjectItem, int, Task<bool>> func)
        {
            var projs = _dte.Solution.Projects;
            stopIter = false;
            foreach (Project proj in projs)
            {
                //                _OutputPane.OutputString(string.Format("Proj {0} {1}\n", proj.Name, proj.Kind));
                await IterateProjItemsAsync(proj, proj.ProjectItems, func, 0);
                if (stopIter)
                {
                    break;
                }
            }
        }
        async Task IterateProjItemsAsync(Project proj, ProjectItems items, Func<Project, ProjectItem, int, Task<bool>> func, int nLevel)
        {
            if (items != null)
            {
                foreach (ProjectItem item in items)
                {
                    if (await func(proj, item, nLevel))
                    {
                        //       _OutputPane.OutputString(string.Format("  Item {0} {1} {2}\n", new string(' ', 2 * nLevel), item.Name, item.Kind));
                        await IterateProjItemsAsync(proj, item.ProjectItems, func, nLevel + 1);
                    }
                    else
                    {
                        stopIter = true;
                        break;
                    }
                }
            }
        }

    }
}


/*
                    //await OpenASolutionAsync();
                    //foreach (EnvDTE.Window win in _dte.Windows)
                    //{
                    //   _logger.LogMessage("Win " + win.Kind + " " + win.ToString());
                    //    if (win.Kind == "Document") // "Tool"
                    //    {
                    //       _logger.LogMessage("   " + win.Document.Name);
                    //    }
                    //}
                    //_dte.ExecuteCommand("File.OpenFile", @"C:\Users\calvinh\Source\repos\hWndHost\Reflect\Reflect.xaml.cs");
                    //_dte.ExecuteCommand("File.NewFile", "temp.cs");
                    //System.Windows.Forms.SendKeys.Send("using System;{ENTER}");
                    //await Task.Delay(1000);
                    //System.Windows.Forms.SendKeys.Send("class testing {{}");
                    //await Task.Delay(1000);
                    //Func<Task> undoAll = async () =>
                    //  {
                    //      var done = false;
                    //     _logger.LogMessage("Start undo loop");
                    //      while (!done)
                    //      {
                    //          try
                    //          {
                    //             _logger.LogMessage(" in undo loop");
                    //              _dte.ExecuteCommand("Edit.Undo");
                    //              await Task.Delay(100);
                    //          }
                    //          catch (Exception)
                    //          {
                    //              done = true;
                    //             _logger.LogMessage("Done undo loop");
                    //          }
                    //      }
                    //  };
                    //await undoAll();
                    //_dte.ExecuteCommand("File.Close", @"");
                    //await Task.Delay(1000);


                    //var ox = new System.Windows.Window();
                    //var tb = new System.Windows.Controls.TextBox()
                    //{
                    //    AcceptsReturn = true
                    //};
                    //ox.Content = tb;
                    //ox.ShowDialog();


 *  * */
