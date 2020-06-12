//Desc: Andrew Arnott's sample Async Thread demo
//Desc: https://github.com/AArnott/AsyncAndThreadingDemo.Wpf
//Include: ..\Util\MyCodeBaseClass.cs
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.ComponentModel.Composition.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.ComponentModelHost.dll


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
using System.ComponentModel;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using System.IO;

// https://github.com/calvinhsia/ThreadPool

namespace MyCodeToExecute
{
    public class MyClass: MyCodeBaseClass
    {
        public static async Task DoMain(object[] args)
        {
            var o = new MyClass(args);
            await o.DoInitializeAsync();
        }
        MainWindow _MyWindow;
        MyClass(object[] args): base(args)
        {

        }
        async Task DoInitializeAsync()
        {

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _MyWindow = new MainWindow(_serviceProvider);

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
        public MainWindow(IServiceProvider _serviceProvider)
        {
            this._serviceProvider = _serviceProvider;
            this.joinableTaskCollection = this.joinableTaskContext.CreateCollection();
            this.joinableTaskFactory = this.joinableTaskContext.CreateFactory(this.joinableTaskCollection);
            this.Loaded += MainWindow_Loaded;
        }
        private IServiceProvider _serviceProvider;
        private JoinableTaskContext jtContext;
        private Label Label1;
        private Label Label2;
        private Label Label3;
        private Button TestThreads;
        private Button StartLongProcess;

        // Have only 1 of these in the entire application!
        private readonly JoinableTaskContext joinableTaskContext = new JoinableTaskContext();

        private readonly JoinableTaskFactory joinableTaskFactory;
        private readonly JoinableTaskCollection joinableTaskCollection;
        private readonly CancellationTokenSource disposalTokenSource = new CancellationTokenSource();

        private ProgressBar TaskProgress;

        private void MainWindow_Loaded(object sender, RoutedEventArgs eLoaded)
        {
            try
            {

                //jtContext = _serviceProvider.GetService<JoinableTaskContext>();
                //jtf = jtContext.Factory;


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
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            this.disposalTokenSource.Cancel();
            this.joinableTaskContext.Factory.Run(() => this.joinableTaskCollection.JoinTillEmptyAsync());
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
