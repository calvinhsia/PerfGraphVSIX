//Desc: sample to demonstrate Getting VS services and monitoring selection events
// This code will be compiled and run when you hit the ExecCode button. Any error msgs will be shown in the status log control.
// This allows you to create a stress test by repeating some code, while taking measurements between each iteration.

//  Macro substitution: %PerfGraphVSIX% will be changed to the fullpath to PerfGraphVSIX
//                      %VSRoot% will be changed to the fullpath to VS: e.g. "C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview"

// Sample Additional references, as needed
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.8.0.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.10.0.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.11.0.dll
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.12.1.DesignTime.dll"
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.0.DesignTime.dll"
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.8.DesignTime.dll"
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.15.0.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.ComponentModelHost.dll

//Include: ..\Util\MyCodeBaseClass.cs


using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

/* This sample allows you to edit/compile/run code inside the VS process from within the same instance of VS
 * You can access VS Services, JTF, etc with the same code as you would from e.g. building a VS component
 * but the Edit/Build/Run cycle is much smaller and faster
 * rIntellisense mostly works. Debugging is via logging or output window pane.
 * */
namespace MyCodeToExecute
{
    public class MySimpleSample : MyCodeBaseClass, IVsSelectionEvents
    {

        public IVsOutputWindowPane _OutputPane;

        public static async Task DoMain(object[] args)
        {
            var oMySimpleSample = new MySimpleSample(args);
            await oMySimpleSample.DoInitializeAsync();
        }
        MySimpleSample(object[] args) : base(args) { }
        async Task DoInitializeAsync()
        {
            _OutputPane = await GetOutputPaneAsync();
            _OutputPane.Clear();
            await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await TaskScheduler.Default; // switch to background thread
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();//outputpane must be called from main thread
                _OutputPane.OutputString("Here in MySimpleSample " + DateTime.Now.ToString("MM/dd/yy hh:mm:ss") + "\r\n");
                await TaskScheduler.Default; // switch to background thread
                _OutputPane.OutputString("Logger message from MySimpleSample\r\n");

                var SolutionService = await _asyncServiceProvider.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
                _OutputPane.OutputString($"{nameof(SolutionService)} {SolutionService}\n");

                var oleMenuCommandService = await _asyncServiceProvider.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
                _OutputPane.OutputString($"{nameof(oleMenuCommandService)} = {oleMenuCommandService}\n");

                var VsShellMonitorSelection = await _asyncServiceProvider.GetServiceAsync(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
                _OutputPane.OutputString($"{nameof(VsShellMonitorSelection)} = {VsShellMonitorSelection}\n");
                uint cookieSelectionEvents = 0;
                try
                {
                    VsShellMonitorSelection.AdviseSelectionEvents(this, out cookieSelectionEvents);
                    while (!_CancellationTokenExecuteCode.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), _CancellationTokenExecuteCode);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                VsShellMonitorSelection.UnadviseSelectionEvents(cookieSelectionEvents);
            });
        }
        public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            _OutputPane.OutputString($"{nameof(OnCmdUIContextChanged)} UICtxCookie={dwCmdUICookie} fActive= {fActive}\n");
            return VSConstants.S_OK;
        }
        public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            _OutputPane.OutputString($"{nameof(OnElementValueChanged)} ElementId={elementid}  ValOld={varValueOld} ValNew={varValueNew}\n");
            return VSConstants.S_OK;
        }
        public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        {
            _OutputPane.OutputString($"{nameof(OnSelectionChanged)}\n");
            return VSConstants.S_OK;
        }
    }
}
