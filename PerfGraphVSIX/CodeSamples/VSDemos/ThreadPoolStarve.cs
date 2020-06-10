//Desc: sample to show how ThreadPool Starvation can occur, with and without the JoinableTakeFactory

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

// https://github.com/calvinhsia/ThreadPool

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
            await Task.Yield();
            _MyWindow = new MainWindow(_CancellationTokenExecuteCode);

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
When running in the VS process, the CLR may already have increased the # of threadpool threads substantially.
The CLR may retire extra idle active threads
";
        CancellationToken _CancellationTokenExecuteCode;

        public int NTasks { get; set; } = 15; // if we're using the vs threadpool, it may already have grown substantially as VS is used.
        public bool CauseStarvation { get; set; }
        public bool UIThreadDoAwait { get; set; } = true;
        public bool UseJTF { get; set; }
        public CancellationTokenSource ctsExecute = new CancellationTokenSource();
        public MainWindow(CancellationToken cancellationToken)
        {
            this._CancellationTokenExecuteCode = cancellationToken;
            this.Loaded += MainWindow_Loaded;
        }
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
                Title = "ThreadPool Starvation Demo";

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

        <StackPanel Grid.Row=""0"" HorizontalAlignment=""Left"" Height=""30"" VerticalAlignment=""Top"" Orientation=""Horizontal"">
            <Label Content=""#Tasks""/>
            <TextBox Text=""{{Binding NTasks}}"" Width=""40"">
                <TextBox.ToolTip>
                    <ToolTip xml:space=""preserve"">
If we're using the vs threadpool, it may already have grown substantially as VS is used.
Each starvation can cause the CLR to grow the threadpool
                    </ToolTip>
                </TextBox.ToolTip>
            </TextBox>
            <CheckBox Margin=""15,0,0,10"" Content=""_CauseStarvation""  IsChecked=""{{Binding CauseStarvation}}"" 
                ToolTip=""In the task, for Non-JTF: use Thread.Sleep to cause starvation, else use Await. For JTF, use JTF.Run to cause starvation""/>
            <CheckBox Margin=""15,0,0,10"" Content=""_UIThreadDoAwait""  IsChecked=""{{Binding UIThreadDoAwait}}"" ToolTip=""In the main (UI) thread, use Await, else use Thread.Sleep (and the UI is not responsive!!)""/>
            <CheckBox Margin=""15,0,0,10"" Content=""_JTFDemo""  IsChecked=""{{Binding UseJTF}}"" 
                ToolTip=""Use Joinable Task Factory and switch to main thread to update a textbox""
                />
            <Button x:Name=""_btnGo"" Content=""_Go"" Width=""45"" ToolTip=""{_toolTipBtnGo}""/>
        </StackPanel>
        <StackPanel Margin=""55,0,0,10"" Orientation=""Horizontal"" HorizontalAlignment=""Right"">
            <Button x:Name=""_btnDbgBreak"" Content=""_DebugBreak"" 
                ToolTip=""invoke debugger: examine Threads (Threads or Parallel Stacks window) to see how busy the threadpool is and examine what each thread is doing""/>
            <TextBox x:Name=""_txtUI"" Grid.Column=""1"" Text=""sample text"" Width=""200"" IsReadOnly=""True"" IsUndoEnabled=""False"" HorizontalAlignment=""Right""/>
        </StackPanel>
        
        <TextBox x:Name=""_txtStatus"" Grid.Row=""1"" FontFamily=""Consolas"" FontSize=""10""
            ToolTip=""DblClick to open in Notepad"" 
        IsReadOnly=""True"" VerticalScrollBarVisibility=""Auto"" HorizontalScrollBarVisibility=""Auto"" IsUndoEnabled=""False"" VerticalAlignment=""Top""/>
    </Grid>
";
                var strReader = new System.IO.StringReader(strxaml);
                var xamlreader = XmlReader.Create(strReader);
                var grid = (Grid)(XamlReader.Load(xamlreader));
                grid.DataContext = this;
                this.Content = grid;
                this._txtStatus = (TextBox)grid.FindName("_txtStatus");
                this._btnGo = (Button)grid.FindName("_btnGo");
                this._btnGo.Click += BtnGo_Click;
                this._btnDbgBreak = (Button)grid.FindName("_btnDbgBreak");
                this._txtUI = (TextBox)grid.FindName("_txtUI");
                this._btnDbgBreak.Click += (o, e) =>
                {
                    Debugger.Break();
                };
                this.Closed += (o, e) =>
                {
                    ctsExecute.Cancel();
                };

                _txtStatus.MouseDoubleClick += (od, ed) =>
                {
                    var fname = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".txt");
                    System.IO.File.WriteAllText(fname, _txtStatus.Text);
                    Process.Start(fname);
                };
            }
            catch (Exception ex)
            {
                this.Content = ex.ToString();
            }
        }
#if false
if you see tasks starting 1 second apart: 
    ThreadPool Tasks are queued...
    The scheduler waits up to 1 second for an available thread.
        If available, the task is run on that thread
        else Starvation: another threadpool thread is created.
PerfView trace, Events View, ThreadPoolWorkerThreadAdjustment Events:
Event Name                                                                 	Time MSec	Process Name  	Reason      	AverageThroughput
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Adjustment	3,370.323	WpfApp1 (7260)	Initializing	     0.000       
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Sample    	3,370.329	WpfApp1 (7260)	            	                 
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Stats     	3,370.332	WpfApp1 (7260)	            	                 
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Adjustment	4,366.461	WpfApp1 (7260)	Starvation  	     0.000       
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Adjustment	5,367.261	WpfApp1 (7260)	Starvation  	     0.000       
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Adjustment	6,366.221	WpfApp1 (7260)	Starvation  	     0.000       
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Adjustment	7,366.493	WpfApp1 (7260)	Starvation  	     0.000       
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Adjustment	8,366.488	WpfApp1 (7260)	Starvation  	     0.000       

#endif
        private async void BtnGo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var oWatcher = new MyThreadPoolWatcher(this))
                {
                    _txtUI.Text = "0"; // must be done on UI thread
                    _btnGo.IsEnabled = false;
                    _txtStatus.Clear();
                    AddStatusMsg($"{nameof(UseJTF)}={UseJTF}  {nameof(CauseStarvation)}={CauseStarvation}  {nameof(UIThreadDoAwait)}={UIThreadDoAwait}");
                    ShowThreadPoolStats();
                    ctsExecute = new CancellationTokenSource();
                    await Task.Delay(TimeSpan.FromSeconds(.5));
                    oWatcher.DetectedStarvation += (o, e) =>
                    {
                        AddStatusMsg("Cancelling because starvation detected");
                        ctsExecute.Cancel();
                    };
                    if (!UseJTF)
                    {
                        await DoThreadPoolAsync(ctsExecute.Token);
                    }
                    else
                    {
                        await DoJTFAsync(ctsExecute.Token);
                    }
                    AddStatusMsg($"Done");
                    ShowThreadPoolStats();
                }
            }
            catch (Exception ex)
            {
                AddStatusMsg(ex.ToString());
            }
            _btnGo.IsEnabled = true;
        }

        private async Task DoThreadPoolAsync(CancellationToken tokenStarveDetected)
        {
            var tcs = new TaskCompletionSource<int>();
            var lstTasks = new List<Task>();
            // here we demonstrate getting ThreadStarvation by using the same thread to do all the work.
            for (int ii = 0; ii < NTasks; ii++)
            {
                var i = ii;// local copy of iteration var
                if (tokenStarveDetected.IsCancellationRequested)
                {
                    break;
                }
                lstTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var tid = Thread.CurrentThread.ManagedThreadId;
                        AddStatusMsg($"Task {i} Start");
                        // in this method we do the work that might take a long time in bkgd thread (several seconds)
                        // keep in mind how the thread that does the work is used: 
                        // if it's calling Thread.Sleep, the CPU load will be low, but the threadpool thread will be occupied
                        if (CauseStarvation)
                        {
                            while (!tcs.Task.IsCompleted && !tokenStarveDetected.IsCancellationRequested)
                            {
                                // 1 sec is the threadpool starvation threshold. We'll sleep a different amount so we can tell its not this sleep causing the 1 sec pauses.
                                Thread.Sleep(TimeSpan.FromSeconds(0.3));
                            }
                        }
                        else
                        {
                            // if the tcs isn't complete, then the curthread will be relinquished back to the theadpool with a continuation queued when the task is done
                            await tcs.Task;
                        }
                        AddStatusMsg($"Task {i} Done on " + (tid == Thread.CurrentThread.ManagedThreadId ? "Same" : "diff") + " Thread");
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }));
            }
            var taskSetDone = Task.Run(async () =>
            { // a task to set the done signal
                try
                {
                    AddStatusMsg("Starting TaskCompletionSource Task");
                    await Task.Delay(TimeSpan.FromSeconds(10), tokenStarveDetected);
                    AddStatusMsg("Setting Task Completion Source");
                    tcs.TrySetResult(1);
                    AddStatusMsg("Set  Task Completion Source");
                }
                catch (OperationCanceledException)
                {
                }
            });
            if (UIThreadDoAwait)
            {   // await all the tasks
                await Task.WhenAll(lstTasks.Union(new[] { taskSetDone }));
            }
            else
            {  // keeps the UI thread really busy, even though CPU not in use
                while (lstTasks.Union(new[] { taskSetDone }).Any(t => !t.IsCompleted))
                {
                    //                    await Task.Yield();
                    Thread.Sleep(TimeSpan.FromSeconds(1));// Sleep surrenders the CPU, but the thread is still in use.
                }
            }
        }

        private async Task DoJTFAsync(CancellationToken tokenStarveDetected)
        {
            var tcs = new TaskCompletionSource<int>();
            JoinableTaskFactory jtf;
            if (Process.GetCurrentProcess().ProcessName == "devenv")
            {
                jtf = ThreadHelper.JoinableTaskFactory; // use the VS JTF
            }
            else
            {
                var jtfContext = new JoinableTaskContext();
                jtf = jtfContext.CreateFactory(jtfContext.CreateCollection());
            }

            var lstTasks = new List<JoinableTask>();
            for (int ii = 0; ii < NTasks; ii++)
            {
                var i = ii;// local copy of iteration var
                if (tokenStarveDetected.IsCancellationRequested)
                {
                    break;
                }
                lstTasks.Add(jtf.RunAsync(async () =>
                {
                    try
                    {
                        tokenStarveDetected.ThrowIfCancellationRequested();
                        Debug.Assert(jtf.Context.IsOnMainThread, "We are on UI thread");
                        await TaskScheduler.Default; // switch to bgd thread
                        Debug.Assert(!jtf.Context.IsOnMainThread, "We are on TP thread");
                        AddStatusMsg($"In Task jtf.runasync {i}");
                        var tid = Thread.CurrentThread.ManagedThreadId;
                        if (CauseStarvation)
                        {
                            // synchronous call: the curthread is not relinquished to the threadpool
                            jtf.Run(async () =>
                            {
                                await jtf.SwitchToMainThreadAsync();
                                UpdateUiTxt();
                                await TaskScheduler.Default; // switch to tp thread
                                while (!tcs.Task.IsCompleted && !tokenStarveDetected.IsCancellationRequested)
                                {
                                    // 1 sec is the threadpool starvation threshold. We'll sleep a different amount so we can tell its not this sleep causing the 1 sec pauses.
                                    Thread.Sleep(TimeSpan.FromSeconds(0.6));
                                }
                            });
                        }
                        else
                        {
                            await jtf.SwitchToMainThreadAsync(); // curthread is immediately relinquished
                            UpdateUiTxt();
                            await TaskScheduler.Default; // switch to tp thread
                            await tcs.Task;
                        }
                        await Task.Delay(TimeSpan.FromSeconds(2.5), tokenStarveDetected);

                        //                    Thread.Sleep(TimeSpan.FromSeconds(2.5));// simulate long time on main thread
                        await TaskScheduler.Default; // switch to tp thread
                                                     //await Task.Yield().ConfigureAwait(false); // this will allow the continuation to go on any thread, not the thread of the captured context.
                                                     //AddStatusMsg($"In Task jtf.runasync {i} bgd");
                                                     //await jtf.SwitchToMainThreadAsync();
                                                     //UpdateUiTxt();
                        AddStatusMsg($"Task jtf.runasync {i} Done on " + (tid == Thread.CurrentThread.ManagedThreadId ? "Same" : "diff") + " Thread");
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }));
            }
            lstTasks.Add(jtf.RunAsync(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), tokenStarveDetected);
                    AddStatusMsg("Setting Task Completion Source");
                    tcs.TrySetResult(1);
                }
                catch (OperationCanceledException)
                {
                }
            }));
            if (UIThreadDoAwait)
            {
                await Task.WhenAll(lstTasks.Select(j => j.Task));
            }
            else
            {
                while (lstTasks.Where(j => !j.Task.IsCompleted).Count() > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1)); // need this await so ui thread can be used by other tasks. Else deadlock
                    await Task.Yield();
                }
            }
        }

        private void UpdateUiTxt()
        {
            var val = int.Parse(_txtUI.Text); // will throw if not on UI thread
            _txtUI.Text = (val + 1).ToString();
        }


        private void ShowThreadPoolStats()
        {
            ThreadPool.GetMaxThreads(out var workerThreads, out var completionPortThreads);  /// 2047, 1000
            AddStatusMsg($"  Max    #workerThreads={workerThreads} #completionPortThreads={completionPortThreads}");
            ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);   // 8, 8
            AddStatusMsg($"  Min    #minWorkerThreads={minWorkerThreads} #minCompletionPortThreads={minCompletionPortThreads}");
            ThreadPool.GetAvailableThreads(out var availWorkerThreads, out var availCompletionPortThreads);
            AddStatusMsg($"  Avail  #availWorkerThreads={availWorkerThreads} #availCompletionPortThreads={availCompletionPortThreads}");
        }
    }

    // https://devdiv.visualstudio.com/DevDiv/_git/VS?path=%2Fsrc%2Fenv%2Fshell%2FUIInternal%2FPackages%2FDiagnostics%2FThreadPoolWatcher.cs&_a=contents&version=GBmaster
    internal class MyThreadPoolWatcher : IDisposable
    {
        private readonly MainWindow _mainWindow;
        private readonly TaskCompletionSource<int> _tcsWatcherThread;
        private readonly CancellationTokenSource _ctsWatcherThread;
        private readonly Thread _threadWatcher;
        public event EventHandler DetectedStarvation;
        public MyThreadPoolWatcher(MainWindow mainWindow)
        {
            this._mainWindow = mainWindow;
            this._tcsWatcherThread = new TaskCompletionSource<int>();
            this._ctsWatcherThread = new CancellationTokenSource();
            // to detect a threadpool starvation, we need a non-threadpool thread
            this._threadWatcher = new Thread(() =>
            {
                var sw = new Stopwatch();
                mainWindow.AddStatusMsg($"{nameof(MyThreadPoolWatcher)}");
                while (!_ctsWatcherThread.IsCancellationRequested)
                {
                    // continuously monitor how long it takes to execute a vary fast WorkItem in threadpool
                    sw.Restart();
                    var tcs = new TaskCompletionSource<int>(0);
                    ThreadPool.QueueUserWorkItem((o) =>
                    {
                        tcs.SetResult(0); // the very simple workitem that should execute very quickly
                    });
                    var timeout = Task.Delay(TimeSpan.FromMilliseconds(250));
                    var ndx = Task.WaitAny(new[] { tcs.Task, timeout });// wait for the workitem to be completed. Can't use async here
                    sw.Stop();
                    if (!tcs.Task.IsCompleted) //detect if it took > thresh to execute task
                    {
                        // when starvation has been detected, the CLR has waited 1 second for an available thread and created another thread
                        mainWindow.AddStatusMsg($"Detected ThreadPool Starvation !!!!!!!! {sw.Elapsed.TotalSeconds:n2} secs");
                        DetectedStarvation?.Invoke(this, new EventArgs());
                        Thread.Sleep(TimeSpan.FromSeconds(1)); // don't try to detect starvation for another second
                        tcs.Task.Wait(); // wait for it to complete if still not done
                    }
                }
                _tcsWatcherThread.TrySetResult(0);
            }, maxStackSize:262144)
            {
                Name = nameof(MyThreadPoolWatcher),
                IsBackground = true
            };
            this._threadWatcher.Start();
        }

        public void Dispose()
        {
            //            _mainWindow.AddStatusMsg($"{nameof(MyThreadPoolWatcher)} Dispose");
            this._ctsWatcherThread.Cancel();
            while (!_tcsWatcherThread.Task.IsCompleted)
            {
                Task.Delay(TimeSpan.FromSeconds(0.2)).Wait();
            }
            this._ctsWatcherThread.Dispose();
            _mainWindow.AddStatusMsg($"{nameof(MyThreadPoolWatcher)} Disposed");
        }
    }

}
