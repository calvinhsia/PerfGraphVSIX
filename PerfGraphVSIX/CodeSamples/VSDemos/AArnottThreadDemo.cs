﻿//Desc: sample to show how ThreadPool Starvation can occur, with and without the JoinableTakeFactory

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

//Pragma: showwarnings=true
//Ref: %PerfGraphVSIX%
//Pragma: verbose = False


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
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Reflection;
using System.Xml;

using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using System.IO;

// https://github.com/calvinhsia/ThreadPool

namespace MyCodeToExecute
{
    public class MyClass
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
            var o = new MyClass();
            await o.DoInitializeAsync(args);
        }
        MainWindow _MyWindow;
        async Task DoInitializeAsync(object[] args)
        {
            await Task.Yield();
            var FullPathToThisSourceFile = args[0] as string;
            _logger = args[1] as ILogger;
            _CancellationTokenExecuteCode = (CancellationToken)args[2];
            var itakeSample = args[3] as ITakeSample; // for taking perf counter measurements
            var g_dte = args[4] as EnvDTE.DTE; // if needed
            _package = args[5] as object;// IAsyncPackage, IServiceProvider
            _MyWindow = new MainWindow();

            var timer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            var tcsMyWindow = new TaskCompletionSource<int>();
            timer.Tick += (o, e) =>
            {
                if (_CancellationTokenExecuteCode.IsCancellationRequested)
                {
                    try
                    {
                        timer.Stop();
                        //_MyWindow.Close();
                        if (!tcsMyWindow.Task.IsCompleted)
                        {
                            tcsMyWindow.SetResult(0);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogMessage(ex.ToString());
                    }
                }
            };
            timer.Start();
            _MyWindow.Closed += (o, e) =>
             {
                 timer.Stop();
                 if (!tcsMyWindow.Task.IsCompleted)
                 {
                     tcsMyWindow.SetResult(0);
                 }
             };
            //var taskClose = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            //{
            //    await TaskScheduler.Default;
            //    while (!_CancellationTokenExecuteCode.IsCancellationRequested)
            //    {
            //        await Task.Delay(TimeSpan.FromSeconds(1), _CancellationTokenExecuteCode);
            //        _logger.LogMessage("ChkC " + tcsMyWindow.Task.IsCompleted.ToString());
            //    }
            //    _logger.LogMessage("ChkC dn");
            //    _logger.LogMessage("mtxx" + tcsMyWindow.Task.ToString());
            //    if (!tcsMyWindow.Task.IsCompleted)
            //    {
            //        _logger.LogMessage("mt");
            //        tcsMyWindow.SetResult(0);
            //    }
            //    //await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            //    //_logger.LogMessage("mt");
            //    //_MyWindow.Close();
            //    //_logger.LogMessage("mtcl");
            //});
            _MyWindow.Show();
            await tcsMyWindow.Task;
            _MyWindow.Close();
        }
    }
    public partial class MainWindow : Window
    {
        private TextBox _txtStatus;
        private Button _btnGo;
        private Button _btnDbgBreak;
        private TextBox _txtUI; // intentionally not databound so must be updated from main thread
        const string _toolTipBtnGo = @"
The UI (including the status window) may not be responsive, depending on the options chosen\r\n
After completion, the status window timestamps are accurate (the actual time the msg was logged).\r\n
The CLR will expand the threadpool if a task can't be scheduled to run because no thread is available for 1 second.
The CLR may retire extra idle active threads
";

        public int NTasks { get; set; } = 10;
        public bool CauseStarvation { get; set; }
        public bool UIThreadDoAwait { get; set; } = true;
        public bool UseJTF { get; set; }
        public MainWindow()
        {
            this.Loaded += MainWindow_Loaded;
        }
        private Label Label1;
        private Label Label2;
        private Label Label3;
        private Button TestThreads;
        private Button StartLongProcess;

        private ProgressBar TaskProgress;

        public void AddStatusMsg(string msg, params object[] args)
        {
            if (_txtStatus != null)
            {
                // we want to read the threadid 
                //and time immediately on current thread
                var dt = string.Format("[{0}],TID={1,2},",
                    DateTime.Now.ToString("hh:mm:ss:fff"),
                    Thread.CurrentThread.ManagedThreadId);
                _txtStatus.Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        // this action executes on main thread
                        if (args.Length == 0) // in cases the msg has embedded special chars like "{"
                        {
                            var str = string.Format(dt + "{0}" + Environment.NewLine, new object[] { msg });
                            _txtStatus.AppendText(str);
                        }
                        else
                        {
                            var str = string.Format(dt + msg + "\r\n", args);
                            _txtStatus.AppendText(str);

                        }
                        _txtStatus.ScrollToEnd();
                    }));
            }
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs eLoaded)
        {
            try
            {

                var strxaml =
    $@"<Grid
xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
xmlns:l=""clr-namespace:{this.GetType().Namespace};assembly={
                    System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location)}"" 
        Margin=""5,5,5,5"">

        <Canvas>
            <Ellipse Name=""elips"" Width=""24"" Height=""24"" Fill=""Red""
             Canvas.Left=""96"">

                <Ellipse.Triggers>
                    <EventTrigger RoutedEvent=""Ellipse.Loaded"">
                        <BeginStoryboard>
                            <Storyboard TargetName=""elips"" RepeatBehavior=""Forever"">
                                <DoubleAnimation
                                Storyboard.TargetProperty=""(Canvas.Top)""
                                From=""96"" To=""300"" Duration=""0:0:1""
                                AutoReverse=""True"" />
                            </Storyboard>
                        </BeginStoryboard>
                    </EventTrigger>
                </Ellipse.Triggers>
            </Ellipse>
            <ProgressBar Height=""23"" Canvas.Left=""151"" Canvas.Top=""220"" Width=""369"" Name=""TaskProgress"" />
            <Button Content=""Test threads"" HorizontalAlignment=""Left"" Width=""138"" Margin=""135,50,0,316.5"" Name=""TestThreads""/>
            <Button Content=""Start long process"" Canvas.Left=""136"" Canvas.Top=""121"" Width=""137"" Height=""47"" Name=""StartLongProcess""/>
        </Canvas>

        <Grid Margin=""311,50,0,0.5"">
            <Grid.ColumnDefinitions>
                <ColumnDefinition />
                <ColumnDefinition />
            </Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
                <RowDefinition Height=""auto"" />
                <RowDefinition Height=""auto"" />
                <RowDefinition Height=""auto"" />
            </Grid.RowDefinitions>
            <Label Content=""On UI thread after Yield?"" Grid.Row=""0"" />
            <Label Content=""On UI thread after ConfigureAwait(false)?"" Grid.Row=""1"" />
            <Label Content=""On UI thread after await Test2Async?"" Grid.Row=""2"" />
            <Label Name=""Label1"" Grid.Column=""1"" />
            <Label Name=""Label2"" Grid.Column=""1"" Grid.Row=""1"" />
            <Label Name=""Label3"" Grid.Column=""1"" Grid.Row=""2"" />
        </Grid>    
</Grid>
";
                var strReader = new System.IO.StringReader(strxaml);
                var xamlreader = XmlReader.Create(strReader);
                var grid = (Grid)(XamlReader.Load(xamlreader));
                this.Label1 = (Label)grid.FindName("Label1");
                this.Label2 = (Label)grid.FindName("Label2");
                this.Label3 = (Label)grid.FindName("Label3");
                this.StartLongProcess = (Button)grid.FindName("StartLongProcess");
                this.TestThreads = (Button)grid.FindName("TestThreads");
                this.TaskProgress = (ProgressBar)grid.FindName("TaskProgress");
                this.TestThreads.Click += Button_Click;
                this.StartLongProcess.Click += StartLongProcess_Click;


                grid.DataContext = this;
                this.Content = grid;
            }
            catch (Exception ex)
            {
                this.Content = ex.ToString();
            }

        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            TestAsync(); // fix me! There's a warning here.
        }

        private async Task TestAsync()
        {
            await Test2Async();
            RecordUIThreadCheck(Label3);
        }

        private async Task Test2Async()
        {
            await Task.Yield();
            RecordUIThreadCheck(Label1);

            await Task.Delay(100).ConfigureAwait(false); // also try 0
            RecordUIThreadCheck(Label2);
        }

        private void RecordUIThreadCheck(Label label)
        {
            bool onUIThread = this.Dispatcher.Thread == Thread.CurrentThread;
            var _ = Dispatcher.BeginInvoke(new Action(() => label.Content = onUIThread));
        }

        private async void StartLongProcess_Click(object sender, RoutedEventArgs e)
        {
            StartLongProcess.IsEnabled = false;
            for (int i = 0; i <= 100; i++)
            {
                TaskProgress.Value = i;
                await Task.Delay(50);
            }

            StartLongProcess.IsEnabled = true;
        }

    }


}
