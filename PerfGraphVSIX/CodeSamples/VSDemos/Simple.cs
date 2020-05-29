//Desc: simple sample to demonstrate writing/running code from within this extension.
// This code will be compiled and run when you hit the ExecCode button. Any error msgs will be shown in the status log control.
// This allows you to create a stress test by repeating some code, while taking measurements between each iteration.

//  Macro substitution: %PerfGraphVSIX% will be changed to the fullpath to PerfGraphVSIX
//                      %VSRoot% will be changed to the fullpath to VS: e.g. "C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview"

// Minimum references:
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Threading.dll"
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Framework.dll


// Sample Additional references, as needed
////Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.8.0.dll
////Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.10.0.dll
////Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.11.0.dll
////Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.12.1.DesignTime.dll"
////Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.0.DesignTime.dll"
////Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.8.DesignTime.dll"
////Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.15.0.dll
////Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.ComponentModelHost.dll

////Ref:"%VSRoot%\Common7\IDE\PublicAssemblies\envdte.dll"



////Ref: c:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll


////Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationFramework.dll
////Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationCore.dll
////Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\WindowsBase.dll
////Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Xaml.dll
////Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.ComponentModel.Composition.dll
////Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.dll
////Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Core.dll
////Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Windows.Forms.dll



using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

/* This sample allows you to edit/compile/run code inside the VS process from within the same instance of VS
 * You can access VS Services, JTF, etc with the same code as you would from e.g. building a VS component
 * but the Edit/Build/Run cycle is much smaller and faster
 * rIntellisense mostly works. Debugging is via logging or output window pane.
 * */
namespace MyCodeToExecute
{
    public class MySimpleSample
    {
        public IServiceProvider _serviceProvider { get { return _package as IServiceProvider; } }
        public IAsyncServiceProvider _asyncServiceProvider { get { return _package as IAsyncServiceProvider; } }
        private object _package;

        Guid _guidPane = new Guid("{CEEAB38D-8BC4-4675-9DFD-993BBE9996A5}");
        public IVsOutputWindowPane _OutputPane;

        public static async Task DoMain(object[] args)
        {
            var oMySimpleSample = new MySimpleSample();
            await oMySimpleSample.DoInitializeAsync(args);
        }
        async Task DoInitializeAsync(object[] args)
        {
            _package = args[5] as object;// IAsyncPackage, IServiceProvider

            IVsOutputWindow outputWindow = await _asyncServiceProvider.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
            var crPane = outputWindow.CreatePane(
                ref _guidPane,
                "PerfGraphVSIX",
                fInitVisible: 1,
                fClearWithSolution: 0);
            outputWindow.GetPane(ref _guidPane, out _OutputPane);
            _OutputPane.Clear();
            await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await TaskScheduler.Default; // switch to background thread
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();//outputpane must be called from main thread
                _OutputPane.OutputString("Here in MySimpleSample " + DateTime.Now.ToString("MM/dd/yy hh:mm:ss")+ "\r\n");
                await TaskScheduler.Default; // switch to background thread
                _OutputPane.OutputString("Logger message from MySimpleSample\r\n");
            });
        }
    }
}
