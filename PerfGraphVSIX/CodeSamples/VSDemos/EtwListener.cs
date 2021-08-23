//Desc: Shows how to Listen to ETW events

//Include: ..\Util\MyCodeBaseClass.cs
//Include: ..\Util\CloseableTabItem.cs
//Include: ..\Util\AssemblyCreator.cs

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
using Microsoft.Performance.ResponseTime;
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
                // run it inproc as a tab on PerfGraph toolwindow
                //CloseableTabItem tabItemTabProc = GetTabItem();
                //            var desiredpid = Process.GetProcessesByName("devenv")[0].Id;
                //tabItemTabProc.Content = new MyUserControl(tabItemTabProc, desiredpid);
                // run it out of proc so our memory use doesn't affect the numbers
                // Or we can generate a 64 bit exe and run it
                var vsRoot = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                var addDir = Path.Combine(vsRoot, "PublicAssemblies") + ";" + Path.Combine(vsRoot, "PrivateAssemblies");
                // now we create an assembly, load it in a 64 bit process which will invoke the same method using reflection
                var asmEtwListener = Path.ChangeExtension(Path.GetTempFileName(), ".exe");

                var type = new AssemblyCreator().CreateAssembly
                    (
                        asmEtwListener,
                        PortableExecutableKinds.PE32Plus,
                        ImageFileMachine.AMD64,
                        AdditionalAssemblyPaths: addDir, // Microsoft.VisualStudio.Shell.Interop
                        logOutput: false // for diagnostics
                    );
                var pidToMonitor = Process.GetCurrentProcess().Id;
                var outputLogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyTestAsm.log");
                File.Delete(outputLogFile);
                var args = $@"""{Assembly.GetExecutingAssembly().Location
                    }"" {nameof(MyEtwMainWindow)} {
                        nameof(MyEtwMainWindow.MyMainMethod)} ""{outputLogFile}"" ""{addDir}"" ""{pidToMonitor}"" true";
                var pListener = Process.Start(
                    asmEtwListener,
                    args);
                //pListener.WaitForExit(30 * 1000);
                //File.Delete(asmEtwListener);
                //var result = File.ReadAllText(outputLogFile);
                _logger.LogMessage($"Launched EtwListener {pListener.Id}");

            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogMessage($"Exception {ex.ToString()}");
            }
        }
    }

    internal class MyEtwMainWindow
    {
        // arg1 is a file to write our results, arg2 and arg3 show we can pass simple types. e.g. Pass the name of a named pipe.
        [STAThread]
        internal static async Task MyMainMethod(string outLogFile, string addDirs, int pidToMonitor, bool boolarg)
        {
            _additionalDirs = addDirs;
            File.AppendAllText(outLogFile, $"Starting {nameof(MyEtwMainWindow)}  {Process.GetCurrentProcess().Id}");

            var tcs = new TaskCompletionSource<int>();
            var thread = new Thread((s) =>
            {
                try
                {
                    AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                    var procTitle = Process.GetProcessById(pidToMonitor).MainWindowTitle;
                    var oWindow = new Window()
                    {
                        Title = $"MyEtwMonitorWindow monitoriing {pidToMonitor} {procTitle}"
                    };
                    File.AppendAllText(outLogFile, $"Starting {nameof(MyEtwMainWindow)}  {Process.GetCurrentProcess().Id} createed window");

                    oWindow.Content = new MyUserControl(null, pidToMonitor);

                    //var traceevent = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\PrivateAssemblies\Microsoft.Diagnostics.Tracing.TraceEvent.dll";
                    //var asm = Assembly.LoadFrom(traceevent);
                    oWindow.ShowDialog();
                }
                catch (Exception ex)
                {
                    File.AppendAllText(outLogFile, $"Exception {nameof(MyEtwMainWindow)}  {Process.GetCurrentProcess().Id}   {ex.ToString()}");
                }
                tcs.SetResult(0);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start(0);
            await tcs.Task;
        }
        static string _additionalDirs;// = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\PublicAssemblies;C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\PrivateAssemblies";
        static private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            Assembly asm = null;
            var requestName = args.Name.Substring(0, args.Name.IndexOf(",")) + ".dll"; // Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            var split = _additionalDirs.Split(new[] { ';' });
            foreach (var dir in split)
            {
                var trypath = Path.Combine(dir, requestName);
                if (File.Exists(trypath))
                {
                    asm = Assembly.LoadFrom(trypath);
                    if (asm != null)
                    {
                        break;
                    }
                }
            }
            return asm;
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
        int _pidToMonitor;
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

        public MyUserControl(CloseableTabItem tabItemTabProc, int pidToMonitor)
        {
            this._pidToMonitor = pidToMonitor;
            if (tabItemTabProc != null)
            {
                tabItemTabProc.TabItemClosed += (o, e) =>
                 {
                     CleanUp();
                 };
            }
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
                    _btnGo.Content = "Stop";
                    AddStatusMsg($"Listening");
                    await DoTracingAsync();
                }
                else
                {
                    _isTracing = false;
                    _btnGo.Content = "Go";
                    AddStatusMsg($"not Listening");
                    _cts.Cancel();
                    CleanUp();
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
            gcAllocStream.Subscribe(new MyObserver<GCAllocationTickTraceData>((d) =>
            {
                if (d.ProcessID == _pidToMonitor)
                {
                    TypeName = d.TypeName;
                    AllocationAmount = d.AllocationAmount;
                }
            }));
            var gcHeapStatsStream = _userSession.Source.Clr.Observe<GCHeapStatsTraceData>();
            gcHeapStatsStream.Subscribe(new MyObserver<GCHeapStatsTraceData>((d) =>
            {
                if (d.ProcessID == _pidToMonitor)
                {
                    //                    AddStatusMsg($"Gen 0 {d.GenerationSize0,12:n0}  {d.GenerationSize1,12:n0} {d.GenerationSize2,12:n0} {d.GenerationSize3,12:n0}  {d.GenerationSize4,12:n0}");
                }
            })); ;
            _userSession.Source.AllEvents += (e) =>
            {
                if (e.ProcessID == _pidToMonitor)
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
