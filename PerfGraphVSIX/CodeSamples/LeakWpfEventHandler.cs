
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
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.ComponentModelHost.dll

//Ref:"%VSRoot%\Common7\IDE\PublicAssemblies\envdte.dll"


//Ref: %PerfGraphVSIX%


////Ref: c:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll


//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationFramework.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationCore.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\WindowsBase.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Xaml.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Xml.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.ComponentModel.Composition.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Core.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Windows.Forms.dll


using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.Test.Stress;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.ComponentModelHost;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Reflection;
using System.Xml;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using System.IO;

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
        public Microsoft.VisualStudio.Shell.IAsyncServiceProvider _asyncServiceProvider { get { return _package as Microsoft.VisualStudio.Shell.IAsyncServiceProvider; } }
        private object _package;
        public ILogger _logger; // log to PerfGraph ToolWindow
        public CancellationToken _CancellationTokenExecuteCode;

        Guid _guidPane = new Guid("{CEEAB38D-8BC4-4675-9DFD-993BBE9996A5}");
        public IVsOutputWindowPane _OutputPane;

        public static async Task DoMain(object[] args)
        {
            var oMySimpleSample = new MySimpleSample();
            await oMySimpleSample.DoInitializeAsync(args);
        }
        async Task DoInitializeAsync(object[] args)
        {
            var FullPathToThisSourceFile = args[0] as string;
            _logger = args[1] as ILogger;
            _CancellationTokenExecuteCode = (CancellationToken)args[2];
            var itakeSample = args[3] as ITakeSample; // for taking perf counter measurements
            var g_dte = args[4] as EnvDTE.DTE; // if needed
            _package = args[5] as object;// IAsyncPackage, IServiceProvider

            IVsOutputWindow outputWindow = await _asyncServiceProvider.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
            var crPane = outputWindow.CreatePane(
                ref _guidPane,
                "PerfGraphVSIX",
                fInitVisible: 1,
                fClearWithSolution: 0);
            outputWindow.GetPane(ref _guidPane, out _OutputPane);
            _OutputPane.Clear();
            var ox = new MyWindow(this);
//            ox.ShowDialog();
            ox.Show();
        }
    }
    class MyWindow : Window
    {
        MySimpleSample _mySimpleSample;
        public MyWindow(MySimpleSample mySimpleSample)
        {
            this._mySimpleSample = mySimpleSample;
            this.Loaded += (ol, el) =>
             {
                 try
                 {
                     _mySimpleSample._logger.LogMessage("In Form Load");

                     var strxaml =
         string.Format(@"<Grid
xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
xmlns:l=""clr-namespace:{0};assembly={1}"" 
        Margin=""5,5,5,5"">
        <Grid.RowDefinitions>
            <RowDefinition Height=""auto""/>
            <RowDefinition Height=""*""/>
        </Grid.RowDefinitions>
        <StackPanel Grid.Row=""0"" HorizontalAlignment=""Left"" Height=""30"" VerticalAlignment=""Top"" Orientation=""Horizontal"">
            <Button x:Name=""_btnGo"" Content=""_Go"" Width=""45""/>
        </StackPanel>
        
    </Grid>
", this.GetType().Namespace,
         System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location)); 
                     
                     var strReader = new System.IO.StringReader(strxaml);
                     var xamlreader = XmlReader.Create(strReader);

                     var grid = (Grid)(XamlReader.Load(xamlreader));
                     grid.DataContext = this;
                     this.Content = grid;
                     var btnGo = (Button)grid.FindName("_btnGo");
                     btnGo.Click += (o, e) =>
                     {
                         this.Close();
                     };

                 }
                 catch (global::System.Exception ex)
                 {
                     this.Content = ex;
                 }
             };
        }

    }
    class MyButton: Button
    {

    }
    class MyCheckBox: CheckBox
    {

    }
}
