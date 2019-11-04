
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
using System.IO;

namespace MyCodeToExecute
{
    public class BaseExecCodeClass
    {
        public string FileToExecute;
        public ILogger logger;
        public CancellationToken _CancellationTokenExecuteCode;
        public EnvDTE.DTE g_dte;
        public Action<string> actTakeSample;

        public int DelayMultiplier = 1; // increase this when running under e.g. MemSpect
        public string SolutionToLoad = @"C:\Users\calvinh\Source\repos\hWndHost\hWndHost.sln";

        public TaskCompletionSource<int> _tcsSolution;
        JoinableTask _tskDoPerfMonitoring;

        public BaseExecCodeClass(object[] args)
        {
            FileToExecute = args[0] as string;
            logger = args[1] as ILogger;
            _CancellationTokenExecuteCode = (CancellationToken)args[2]; // value type
            g_dte = args[3] as EnvDTE.DTE;
            actTakeSample = args[4] as Action<string>;
            logger.LogMessage("Registering events ");
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete += SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution += SolutionEvents_OnAfterCloseSolution;
        }
        public void UnregisterEvents()
        {
            logger.LogMessage("UnRegistering events");
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete -= SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution -= SolutionEvents_OnAfterCloseSolution;
        }


        public void TakeSample(string desc)
        {
            if (actTakeSample != null)
            {
                actTakeSample(Path.GetFileNameWithoutExtension(FileToExecute) + " " + desc);
            }
        }

        public async Task OpenASolutionAsync()
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

        public async Task CloseTheSolutionAsync()
        {
            _tcsSolution = new TaskCompletionSource<int>();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            g_dte.Solution.Close();
            if (!_CancellationTokenExecuteCode.IsCancellationRequested)
            {
                await Task.Delay(5000 * DelayMultiplier, _CancellationTokenExecuteCode);
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
