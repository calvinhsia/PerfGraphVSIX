//Desc: The base class used for many of the samples

// This code will be compiled and run when you hit the ExecCode button. Any error msgs will be shown in the status log control.
// This allows you to create a stress test by repeating some code, while taking measurements between each iteration.

//  Macro substitution: %PerfGraphVSIX% will be changed to the fullpath to PerfGraphVSIX
//                      %VSRoot% will be changed to the fullpath to VS: e.g. "C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview"
//                      "C:\Program Files (x86)" will be changed to the appropriate environment variable ('ProgramFiles' or 'ProgramFiles(x86)') depending on 32 or 64 bit

//Ref32: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.8.0.dll
//Ref32: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.10.0.dll
//Ref32: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.11.0.dll
//Ref32: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.12.1.DesignTime.dll"
//Ref32: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.0.DesignTime.dll"
//Ref32: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.8.DesignTime.dll"
//Ref32: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Threading.dll"
//Ref32: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.dll
//Ref32: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.15.0.dll
//Ref32: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Framework.dll

//Ref32:"%VSRoot%\Common7\IDE\PublicAssemblies\envdte.dll"


//Ref64: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.15.0.dll
//Ref64: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Interop.dll
//Ref64: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Threading.16.0\Microsoft.VisualStudio.Threading.dll
//Ref64: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Framework.dll

//Ref: %PerfGraphVSIX%


////Ref: c:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll

////Note: "C:\Program Files (x86)\" is replaced with environment variable: 'ProgramFiles(x86)'

//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationFramework.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationCore.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\WindowsBase.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Xaml.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Core.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Windows.Forms.dll
//Include: CloseableTabItem.cs

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
using System.Net.NetworkInformation;
using System.Windows.Controls;

namespace MyCodeToExecute
{
    public class MyCodeBaseClass
    {
        public string _FileToExecute;
        public ILogger _logger;
        public CancellationToken _CancellationTokenExecuteCode;
        public EnvDTE.DTE _dte;
        public IServiceProvider _serviceProvider { get { return _package as IServiceProvider; } }
        public Microsoft.VisualStudio.Shell.IAsyncServiceProvider _asyncServiceProvider { get { return _package as Microsoft.VisualStudio.Shell.IAsyncServiceProvider; } }
        private object _package;
        public ITakeSample _itakeSample;
        public PerfGraphToolWindowControl _perfGraphToolWindowControl;

        public string TestName { get { return Path.GetFileNameWithoutExtension(_FileToExecute); } }

        public MyCodeBaseClass(object[] args)
        {
            _FileToExecute = args[0] as string;
            _logger = args[1] as ILogger;
            _CancellationTokenExecuteCode = (CancellationToken)args[2]; // value type
            _itakeSample = args[3] as ITakeSample;
            _dte = args[4] as EnvDTE.DTE;
            _package = args[5] as object;// IAsyncPackage;
            _perfGraphToolWindowControl = _itakeSample as PerfGraphToolWindowControl;
        }

        public async Task<IVsOutputWindowPane> GetOutputPaneAsync()
        {
            // this shows how to get VS Services
            // you can add ref to a DLL if needed, and add Using's if needed
            // if you're outputting to the OutputWindow, be aware that the OutputPanes are editor instances, which will
            // look like a leak as they accumulate data.
            Guid guidPane = new Guid("{CEEAB38D-8BC4-4675-9DFD-993BBE9996A5}");
            IVsOutputWindow outputWindow = await _asyncServiceProvider.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
            var crPane = outputWindow.CreatePane(
                ref guidPane,
                "PerfGraphVSIX",
                fInitVisible: 1,
                fClearWithSolution: 0);
            outputWindow.GetPane(ref guidPane, out var OutputPane);
            OutputPane.Activate();
            return OutputPane;
        }

        public CloseableTabItem GetTabItem()
        {
            int iTabItemIndex = 0;
            //first we need to see if there is already an existing one, then close it
            // need to give it a chance to clean up, so invoke the CloseTabItem Method
            foreach (TabItem tabitem in _perfGraphToolWindowControl.TabControl.Items)
            {
                if (tabitem.Name == Path.GetFileNameWithoutExtension(_FileToExecute))
                {
                    // problem: each time asm is recompiled/reloaed, the type "CloseableTabItem" has different identity, so casting can cause error in this case
                    // so need to call via reflection
                    if (tabitem.GetType().Name == "CloseableTabItem")
                    {
                        // found an existing one. Need to close it
                        tabitem.GetType().GetMethod("CloseTabItem").Invoke(tabitem, null);

                        //tabItemTabProc = (CloseableTabItem)tabitem; // this throws invalid cast exception if recompiled
                        //tabItemTabProc.CloseTabItem();
                        break;
                    }
                }
                iTabItemIndex++;
            }
            var tabItemTabProc = new CloseableTabItem(Path.GetFileNameWithoutExtension(_FileToExecute), string.Empty);
            _perfGraphToolWindowControl.TabControl.Items.Add(tabItemTabProc);
            _perfGraphToolWindowControl.TabControl.SelectedIndex = _perfGraphToolWindowControl.TabControl.Items.Count - 1; // select User output tab
            return tabItemTabProc;
        }
    }
}

