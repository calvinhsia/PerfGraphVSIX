//Desc: create another STA UI thread within VS
//Desc: can also be used in Test frameworks that have no UI

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
            var execContext = CreateExecutionContext(tcsStaThread);
            var tcs = new TaskCompletionSource<int>();
            Exception except = null;
            await execContext.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    _logger.LogMessage($"In InvokeAsync on myUIThread");
                    _MyWindow = new MainWindow(this);
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
                                _MyWindow.Close();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogMessage(ex.ToString());
                            }
                        }
                    };
                    timer.Start();
                    bool fDidShutdown = false;
                    void DoShutDown()
                    {
                        if (!fDidShutdown)
                        {
                            timer.Stop();
                            fDidShutdown = true;
                            _logger.LogMessage($"Before invokeshutdown");
                            execContext.Dispatcher.InvokeShutdown();
                            _logger.LogMessage($" after invokeshutdown");
                            tcs.SetResult(0);
                            _logger.LogMessage($" after set tcs");
                        }
                    }
                    _MyWindow.Closed += (o, e) =>
                     {
                         DoShutDown();
                     };
                    _MyWindow.ShowDialog();
                    DoShutDown();
                }
                catch (Exception ex)
                {
                    except = ex;
                    tcs.SetResult(0);
                }
            });
            await tcs.Task;
            if (except != null)
            {
                _logger.LogMessage($"Exception in execution thread {except}");
                throw except;
            }
            _logger.LogMessage($"done task. Now waiting for STAThread to end");
            await tcsStaThread.Task; // wait for sta thread to finish
            _logger.LogMessage($"Test end");
        }
        private MyExecutionContext CreateExecutionContext(TaskCompletionSource<int> tcsStaThread)
        {
            const string Threadname = "MyStaThread";
            var tcsGetExecutionContext = new TaskCompletionSource<MyExecutionContext>();

            var myStaThread = new Thread(() =>
            {
                _logger.LogMessage($"MyStaThread start");
                var dispatcher = Dispatcher.CurrentDispatcher;
                var syncContext = new DispatcherSynchronizationContext(dispatcher);
                SynchronizationContext.SetSynchronizationContext(syncContext);

                tcsGetExecutionContext.SetResult(new MyExecutionContext
                {
                    DispatcherSynchronizationContext = syncContext,
                    Dispatcher = dispatcher
                });

                _logger.LogMessage($"MyStaThread start Dispatcher.Run");
                Dispatcher.Run();
                _logger.LogMessage($"MyStaThread After Dispatcher.run");
                tcsStaThread.SetResult(0);
            });

            myStaThread.SetApartmentState(ApartmentState.STA);
            myStaThread.Name = Threadname;
            myStaThread.Start();
            return tcsGetExecutionContext.Task.Result;
        }

        internal class MyExecutionContext
        {
            public DispatcherSynchronizationContext DispatcherSynchronizationContext { get; set; }
            public Dispatcher Dispatcher { get; set; }
        }
    }

    public partial class MainWindow : Window
    {
        CancellationToken _CancellationTokenExecuteCode => myClass._CancellationTokenExecuteCode;
        MyClass myClass;
        public MainWindow(MyClass myclass)
        {
            this.myClass = myclass;
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
                Title = "Private STA UI Thread Demo";

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
        <Canvas Margin=""511,0,0,0.5"">
            <Ellipse Name=""Ball"" Width=""24"" Height=""24"" Fill=""Blue""
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
        <TextBox xml:space=""preserve"" Margin=""10,50,20,20"">
This Window is running on it's own private UI thread, as can be seen in the Log Messages, which show the ThreadId
The bouncing ball will be jerky if the thread is busy (UIDelay)
If you cause a UI delay in the VS main thread, this thread will be less affected.
        </TextBox>
    </Grid>
";
                var strReader = new System.IO.StringReader(strxaml);
                var xamlreader = XmlReader.Create(strReader);
                var grid = (Grid)(XamlReader.Load(xamlreader));
                grid.DataContext = this;
                this.Content = grid;
            }
            catch (Exception ex)
            {
                this.Content = ex.ToString();
            }
        }
    }
}
