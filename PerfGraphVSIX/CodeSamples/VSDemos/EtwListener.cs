//Desc: Shows how to Listen to ETW events

//Include: ..\Util\MyCodeBaseClass.cs
//Include: ..\Util\CloseableTabItem.cs

//Ref: %VSRoot%\Common7\IDE\PrivateAssemblies\Microsoft.Diagnostics.Tracing.TraceEvent.dll

/*
 */

using System;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PerfGraphVSIX;
using Microsoft.Test.Stress;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

using System.Xml;
using System.Windows.Markup;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;


namespace MyCodeToExecute
{
    public class MyClass : MyCodeBaseClass
    {
        public static async Task DoMain(object[] args)
        {
            var ox = new MyClass(args);
            await ox.DoItAsync();
        }
        MyClass(object[] args) : base(args) { }


        async Task DoItAsync()
        {
            try
            {
                await Task.Yield();
                CloseableTabItem tabItemTabProc = GetTabItem();
                tabItemTabProc.Content = new MyUserControl(this, tabItemTabProc);

            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogMessage($"Exception {ex.ToString()}");
            }
        }
    }
    class MyUserControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void RaisePropChanged([CallerMemberName] string propName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }
        string _TypeName;
        public string TypeName { get { return _TypeName; } set { _TypeName = value; RaisePropChanged(); } }
        int _AllocationAmount;
        public int AllocationAmount { get { return _AllocationAmount; } set { _AllocationAmount = value; RaisePropChanged(); } }


        private TextBox _txtStatus;
        private Button _btnGo;
        private CancellationTokenSource _cts;
        private bool _isTracing;
        private TraceEventSession _kernelsession;
        private TraceEventSession _userSession;

        public MyUserControl(MyClass myClass, CloseableTabItem tabItemTabProc)
        {
            tabItemTabProc.TabItemClosed += (o, e) =>
             {
                 CleanUp();
             };
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
        <StackPanel Grid.Row=""0"" HorizontalAlignment=""Left"" VerticalAlignment=""Top"" Orientation=""Vertical"">
            <Button x:Name=""_btnGo"" Content=""_Go"" Width=""45"" ToolTip="""" HorizontalAlignment = ""Left""/>
            <StackPanel Orientation=""Horizontal"">
                <Label Content=""TypeName""/>
                <TextBox Text = ""{{Binding TypeName}}"" Width = ""400""/>
            </StackPanel>
            <StackPanel Orientation=""Horizontal"">
                <Label Content=""AllocationAmount""/>
                <TextBox Text = ""{{Binding AllocationAmount}}"" Width = ""400""/>
            </StackPanel>

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
        }
        void CleanUp()
        {
            _kernelsession?.Dispose();
            _userSession?.Dispose();
        }

        public void AddStatusMsg(string msg, params object[] args)
        {
            if (_txtStatus != null)
            {
                // we want to read the threadid 
                //and time immediately on current thread
                var dt = string.Format("[{0,13}],TID={1,3},",
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
        async void BtnGo_Click(object sender, RoutedEventArgs e)
        {
            await Task.Yield();
            try
            {
                if (!_isTracing)
                {
                    _cts = new CancellationTokenSource();
                    _isTracing = true;
                    AddStatusMsg($"Listening");
                    _btnGo.Content = "Stop";
                    await DoTracingAsync();
                }
                else
                {
                    _isTracing = false;
                    AddStatusMsg($"not Listening");
                    _cts.Cancel();
                    CleanUp();
                    _btnGo.Content = "Go";
                }
            }
            catch (Exception ex)
            {
                AddStatusMsg(ex.ToString());
            }
        }
        class MyObserver<T> : IObserver<T>
        {
            private readonly Action<T> _action;

            public MyObserver(Action<T> action)
            {
                this._action = action;
            }
            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(T value)
            {
                _action(value);
            }
        }
        async Task DoTracingAsync()
        {

            if (TraceEventSession.IsElevated() != true)
            {
                throw new InvalidOperationException("Must run as admin");
            }
            _userSession = new TraceEventSession($"PerfGraphEtwListener");

            //            _userSession.EnableProvider("*Microsoft-VisualStudio-Common", matchAnyKeywords: 0xFFFFFFDF);
            //            _userSession.EnableProvider("*Microsoft-VisualStudio-Common");
            //            _userSession.EnableProvider(new Guid("25c93eda-40a3-596d-950d-998ab963f367"));
            // Microsoft-VisualStudio-Common {25c93eda-40a3-596d-950d-998ab963f367}

            //_userSession.EnableProvider(new Guid("EE328C6F-4C94-45F7-ACAF-640C6A447654")); // Retail Asserts
            //_userSession.EnableProvider(new Guid("143A31DB-0372-40B6-B8F1-B4B16ADB5F54"), TraceEventLevel.Verbose, ulong.MaxValue); //MeasurementBlock
            //_userSession.EnableProvider(new Guid("641D7F6C-481C-42E8-AB7E-D18DC5E5CB9E"), TraceEventLevel.Verbose, ulong.MaxValue); // Codemarker
            //_userSession.EnableProvider(new Guid("BF965E67-C7FB-5C5B-D98F-CDF68F8154C2"), TraceEventLevel.Verbose, ulong.MaxValue); // // RoslynEventSource


            _userSession.EnableProvider(ClrTraceEventParser.ProviderGuid, TraceEventLevel.Verbose, (ulong)(ClrTraceEventParser.Keywords.Default));
            var gcAllocStream = _userSession.Source.Clr.Observe<GCAllocationTickTraceData>();
            var desiredpid = Process.GetProcessesByName("devenv")[0].Id;
            gcAllocStream.Subscribe(new MyObserver<GCAllocationTickTraceData>((d) =>
            {
                if (d.ProcessID == desiredpid)
                {
                    TypeName = d.TypeName;
                    AllocationAmount = d.AllocationAmount;
                }
            }));
            var gcHeapStatsStream = _userSession.Source.Clr.Observe<GCHeapStatsTraceData>();
            gcHeapStatsStream.Subscribe(new MyObserver<GCHeapStatsTraceData>((d) =>
            {
                if (d.ProcessID == desiredpid)
                {
                    //                    AddStatusMsg($"Gen 0 {d.GenerationSize0,12:n0}  {d.GenerationSize1,12:n0} {d.GenerationSize2,12:n0} {d.GenerationSize3,12:n0}  {d.GenerationSize4,12:n0}");
                }
            })); ;
            _userSession.Source.AllEvents += (e) =>
            {
                if (e.ProcessID == desiredpid)
                {
                    //                     AddStatusMsg($"{e.EventName}");
                }
            };
            _kernelsession = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
            _kernelsession.EnableKernelProvider(
                KernelTraceEventParser.Keywords.ImageLoad |
                KernelTraceEventParser.Keywords.Process |
                KernelTraceEventParser.Keywords.FileIO |
                KernelTraceEventParser.Keywords.NetworkTCPIP
                );
            _kernelsession.Source.Kernel.ImageLoad += (d) =>
            {

            };
            _kernelsession.Source.Kernel.ProcessStart += (d) =>
            {

            };
            _kernelsession.Source.Kernel.ProcessStop += (d) =>
            {

            };
            _kernelsession.Source.Kernel.TcpIpConnect += (d) =>
            {

            };
            _kernelsession.Source.Kernel.FileIORead += (d) =>
            {

            };
            _kernelsession.Source.Kernel.FileIOCreate += (d) =>
            {

            };
            _ = Task.Run(() =>
            {
                _kernelsession.Source.Process();
            });
            await Task.Run(() =>
            {
                _userSession.Source.Process();
            });
        }
    }
}
