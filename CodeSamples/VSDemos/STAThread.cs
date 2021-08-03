//Desc: create another STA UI thread within VS
//Desc: can also be used in Test frameworks that have no UI
//Desc: Demonstrate UI delays detected in VS

//Include: ..\Util\MyCodeBaseClass.cs
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.ComponentModelHost.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.ComponentModel.Composition.dll


using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
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


namespace MyCodeToExecute
{
    public class MyClass : MyCodeBaseClass
    {
        public static async Task DoMain(object[] args)
        {
            var o = new MyClass(args);
            await o.DoInitializeAsync();
        }
        MainWindow _MyWindow;
        MyClass(object[] args) : base(args) { }
        async Task DoInitializeAsync()
        {
            var tcsStaThread = new TaskCompletionSource<int>();
            _logger.LogMessage($"Starting");

            await RunInSTAExecutionContextAsync(async () =>
            {
                await Task.Yield();
                _logger.LogMessage($"In InvokeAsync on myUIThread");
                _MyWindow = new MainWindow(this);
                var isClosed = false;
                _MyWindow.Closed += (o, e) =>
                {
                    isClosed = true;
                };
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    while (!_CancellationTokenExecuteCode.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1), _CancellationTokenExecuteCode);
                            if (isClosed)
                            {
                                break;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }
                    _MyWindow.Dispatcher.Invoke(() =>
                    {
                        _MyWindow.Close();
                    });
                });
                _MyWindow.ShowDialog();
            });
        }
        public async Task RunInSTAExecutionContextAsync(Func<Task> actionAsync)
        {
            var tcsStaThread = new TaskCompletionSource<int>();
            _logger.LogMessage($"Creating ExecutionContext");
            var execContext = CreateExecutionContext(tcsStaThread);
            _logger.LogMessage($"Created ExecutionContext");
            var tcs = new TaskCompletionSource<int>();
            Exception except = null;
            await execContext.Dispatcher.InvokeAsync(async () =>
            {
                await Task.Yield();
                try
                {
                    await actionAsync();
                }
                catch (Exception ex)
                {
                    except = ex;
                }
                _logger.LogMessage($"Before invokeshutdown");
                execContext.Dispatcher.InvokeShutdown();
                _logger.LogMessage($" after invokeshutdown");
                tcs.SetResult(0);
                _logger.LogMessage($" after set tcs");
            });
            await tcs.Task;
            if (except != null)
            {
                _logger.LogMessage($"Exception in execution thread {except}");
                throw except;
            }
            _logger.LogMessage($"done task. Now waiting for STAThread to end");
            await tcsStaThread.Task; // wait for sta thread to finish
        }

        MyExecutionContext CreateExecutionContext(TaskCompletionSource<int> tcsStaThread)
        {
            const string Threadname = "MyStaThread";
            var tcsGetExecutionContext = new TaskCompletionSource<MyExecutionContext>();

            _logger.LogMessage($"Creating {Threadname}");
            var myStaThread = new Thread(() =>
            {
                // Create the context, and install it:
                _logger.LogMessage($"{Threadname} start");
                var dispatcher = Dispatcher.CurrentDispatcher;
                var syncContext = new DispatcherSynchronizationContext(dispatcher);

                SynchronizationContext.SetSynchronizationContext(syncContext);

                tcsGetExecutionContext.SetResult(new MyExecutionContext
                {
                    DispatcherSynchronizationContext = syncContext,
                    Dispatcher = dispatcher
                });

                // Start the Dispatcher Processing
                _logger.LogMessage($"MyStaThread before Dispatcher.run");
                Dispatcher.Run();
                _logger.LogMessage($"MyStaThread After Dispatcher.run");
                tcsStaThread.SetResult(0);
            });

            myStaThread.SetApartmentState(ApartmentState.STA);
            myStaThread.Name = Threadname;
            myStaThread.Start();
            _logger.LogMessage($"Starting {Threadname}");
            return tcsGetExecutionContext.Task.Result;
        }

        public class MyExecutionContext
        {
            public DispatcherSynchronizationContext DispatcherSynchronizationContext { get; set; }
            public Dispatcher Dispatcher { get; set; }
        }
    }

    public partial class MainWindow : Window
    {
        CancellationToken _CancellationTokenExecuteCode => myClass._CancellationTokenExecuteCode;
        MyClass myClass;
        public int UIDelayMSecs { get; set; } = 3000;
        public MainWindow(MyClass myclass)
        {
            this.myClass = myclass;
            Width = 800;
            Height = 400;
            this.Loaded += MainWindow_Loaded;
        }
        void LogMessage(string msg, params object[] args)
        {
            myClass._logger.LogMessage(msg, args);
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs eLoaded)
        {
            try
            {
                Title = "Private STA UI Thread and UIDelay Demo";
                // xmlns:l="clr-namespace:WpfApp1;assembly=WpfApp1"
                // the C# string requires quotes to be doubled
                var strxaml =
    $@"<Grid
xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
xmlns:l=""clr-namespace:{this.GetType().Namespace};assembly={
                    System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location)}"" 
        Margin=""5,5,5,5"">
        <Grid.RowDefinitions>
            <RowDefinition Height=""auto""/>
            <RowDefinition Height=""*""/>
        </Grid.RowDefinitions>
        <Canvas Margin=""11,0,0,0.5"">
            <Ellipse Name=""Ball"" Width=""24"" Height=""24"" Fill=""Green""
             Canvas.Left=""396"">
                <Ellipse.Triggers>
                    <EventTrigger RoutedEvent=""Ellipse.Loaded"">
                        <BeginStoryboard>
                            <Storyboard TargetName=""Ball"" RepeatBehavior=""Forever"">
                                <DoubleAnimation
                                Storyboard.TargetProperty=""(Canvas.Left)""
                                From=""96"" To=""300"" Duration=""0:0:1""
                                AutoReverse=""True"" />
                            </Storyboard>
                        </BeginStoryboard>
                    </EventTrigger>
                </Ellipse.Triggers>
            </Ellipse>
        </Canvas>
        <StackPanel Grid.Row=""1"" Margin=""10,50,20,20"">
            <TextBox xml:space=""preserve"" >
This Window is running on it's own private UI thread, as can be seen in the Log Messages, which show the ThreadId
The bouncing ball will be jerky if the thread is too busy to update the User Interface (UIDelay)
Run the ChildProc sample at the same time because it shows a bouncing ball in the PerfGraph window on the VS UI thread
If you cause a UI delay in the VS main thread, this thread will be less affected.
The ChildProc sample has a bouncing ball that will not be animated during UI delays.
The Telemetry monitor sample will show a vs/delays/ui/start event
            </TextBox>
            <StackPanel Orientation=""Horizontal"">
                <Label Content=""UIDelay on VS main thread in msecs""/>
                <TextBox Text = ""{{Binding UIDelayMSecs}}""/>
            </StackPanel>
            <Button Content=""Cause UI Delay on Main Thread of this form"" Name=""btnUIDelayMySTA"" Width=""250"" HorizontalAlignment=""Left""
ToolTip=""Cause UI delay on main thread of this form""
/>
            <Button Content=""Cause UI Delay on VS Main Thread"" Name=""btnUIDelay"" Width=""250"" HorizontalAlignment=""Left""
ToolTip=""Cause UI delay on main thread of VS: if you have telemetry monitor or ChildProc sample running you can see the effective UI delay""
/>
            <Button Content=""Close"" Name=""btnClose"" Width=""100"" HorizontalAlignment=""Left""/>
        </StackPanel>
    </Grid>
";
                var strReader = new System.IO.StringReader(strxaml);
                var xamlreader = XmlReader.Create(strReader);
                var grid = (Grid)(XamlReader.Load(xamlreader));
                grid.DataContext = this;
                var btnClose = (Button)grid.FindName("btnClose");
                btnClose.Click += (o, e) =>
                {
                    this.Close();
                };
                var btnUIDelay = (Button)grid.FindName("btnUIDelay");
                btnUIDelay.Click += async (o, e) =>
                {
                    // switch to main thread of VS
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    LogMessage($"Causing UI delay on main thread of VS by Thread.Sleep({UIDelayMSecs})");
                    Thread.Sleep(TimeSpan.FromMilliseconds(UIDelayMSecs));
                };
                var btnUIDelayMySTA = (Button)grid.FindName("btnUIDelayMySTA");
                btnUIDelayMySTA.Click += async (o, e) =>
                {
                    LogMessage($"Causing UI delay on main thread of STA form by Thread.Sleep({UIDelayMSecs})");
                    Thread.Sleep(TimeSpan.FromMilliseconds(UIDelayMSecs));
                };
                this.Content = grid;
            }
            catch (Exception ex)
            {
                this.Content = ex.ToString();
            }
        }
    }
}
