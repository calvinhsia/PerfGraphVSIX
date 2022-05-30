//Desc: Listens for GC ETW events and shows in a separate process the most common allocations.

//Include: ..\Util\MyCodeBaseClass.cs
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
using System.Windows;
using System.Windows.Markup;
using System.Windows.Interop;
using System.Windows.Controls;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Collections.Generic;

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

        const int WM_MOVE = 3;
        const int WM_Size = 5;
        IntPtr hwndClient = IntPtr.Zero;
        IntPtr hwndOOP = IntPtr.Zero;
        string oopProcName = "";
        async Task DoItAsync()
        {
            try
            {
                await Task.Yield();
                // run it inproc as a tab on PerfGraph toolwindow
                var wtoolwindow = MyStatics.GetAncestor<Window>(_perfGraphToolWindowControl);
                var interop = new WindowInteropHelper(wtoolwindow);
                interop.EnsureHandle();

                //CloseableTabItem tabItemTabProc = GetTabItem();
                //tabItemTabProc.Content = "asdf";
                //tabItemTabProc.TabItemClosed += (o, e) =>
                //{
                //    wtoolwindow.LocationChanged -= UpdateOOPWindow;
                //    tabItemTabProc.LayoutUpdated -= UpdateOOPWindow;
                //};
                //wtoolwindow.LocationChanged += UpdateOOPWindow;
                //tabItemTabProc.LayoutUpdated += UpdateOOPWindow;

                //            var desiredpid = Process.GetProcessesByName("devenv")[0].Id;
                //tabItemTabProc.Content = new MyUserControl(tabItemTabProc, desiredpid);
                // run it out of proc so our memory use doesn't affect the numbers
                // Or we can generate a 64 bit exe and run it
                var vsRoot = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                var pathStressDir = Path.GetDirectoryName(typeof(StressUtil).Assembly.Location);
                var addDir = $"{(Path.Combine(vsRoot, "PublicAssemblies"))};{(Path.Combine(vsRoot, "PrivateAssemblies"))};{pathStressDir}";

                // now we create an assembly, load it in a 64 bit process which will invoke the same method using reflection
                var asmGCMonitor = Path.ChangeExtension(Path.GetTempFileName(), ".exe");
                oopProcName = Path.GetFileNameWithoutExtension(asmGCMonitor);
                var type = new AssemblyCreator().CreateAssembly
                    (
                        asmGCMonitor,
                        PortableExecutableKinds.PE32Plus,
                        ImageFileMachine.AMD64,
                        AdditionalAssemblyPaths: addDir, // Microsoft.VisualStudio.Shell.Interop
                        logOutput: false // for diagnostics
                    );
                var pidToMonitor = Process.GetCurrentProcess().Id;
                var outputLogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyTestAsm.log");
                File.Delete(outputLogFile);
                var args = $@"""{Assembly.GetExecutingAssembly().Location}"" {nameof(MyEtwMainWindow)} {nameof(MyEtwMainWindow.MyMainMethod)} ""{outputLogFile}"" ""{addDir}"" ""{pidToMonitor}"" true";
                var pListener = Process.Start(
                    asmGCMonitor,
                    args);
                //pListener.WaitForExit(30 * 1000);
                //File.Delete(asmGCMonitor);
                //var result = File.ReadAllText(outputLogFile);
                _logger.LogMessage($"Launched GCMonitor {pListener.Id}");

            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogMessage($"Exception {ex.ToString()}");
            }
        }
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);

        void UpdateOOPWindow(object sender, EventArgs e)
        {
            try
            {
                if (hwndOOP == IntPtr.Zero && !string.IsNullOrEmpty(oopProcName))
                {
                    var oopProcs = Process.GetProcessesByName(oopProcName);
                    if (oopProcs.Length == 1)
                    {
                        hwndOOP = oopProcs[0].MainWindowHandle;
                    }
                }
                if (hwndOOP != IntPtr.Zero)
                {
                    SetWindowPos(hwndOOP,
                        hWndInsertAfter: IntPtr.Zero,
                        X: 0,
                        Y: 0,
                        cx: 1000,
                        cy: 500,
                        uFlags: 0
                        );
                }
            }
            catch (Exception)
            {
            }
        }
    }

    internal class MyEtwMainWindow
    {
        // arg1 is a file to write our results, arg2 and arg3 show we can pass simple types. e.g. Pass the name of a named pipe.
        //        [STAThread]
        internal static async Task MyMainMethod(string outLogFile, string addDirs, int pidToMonitor, bool boolarg)
        {
            _additionalDirs = addDirs;
            File.AppendAllText(outLogFile, $"Starting {nameof(MyEtwMainWindow)}  {Process.GetCurrentProcess().Id}  AddDirs={addDirs}");

            var tcs = new TaskCompletionSource<int>();
            var thread = new Thread((s) =>
            {
                try
                {
                    AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                    var ProcToMonitor = Process.GetProcessById(pidToMonitor);
                    var oWindow = new Window()
                    {
                        Title = $"GCMonitor monitoring {pidToMonitor} {ProcToMonitor.MainWindowTitle}",
                    };
                    if (ProcToMonitor.MainWindowHandle != IntPtr.Zero)
                    {
                        var interop = new WindowInteropHelper(oWindow);
                        interop.EnsureHandle();
                        interop.Owner = ProcToMonitor.MainWindowHandle;
                    }
                    oWindow.Content = new MyUserControl(null, pidToMonitor);

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
    class GCSampleData
    {
        public int Count { get; set; }
        public long Size { get; set; }
        public string TypeName { get; set; }
        public override string ToString() => $"{Count,-11:n0}  {Size,-13:n0} {TypeName} ";
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

        int _GCCount;
        public int GCCount { get { return _GCCount; } set { _GCCount = value; RaisePropChanged(); } }

        GCType _GCType;
        public GCType GCType { get { return _GCType; } set { _GCType = value; RaisePropChanged(); } }

        GCReason _GCReason;
        public GCReason GCReason { get { return _GCReason; } set { _GCReason = value; RaisePropChanged(); } }
        public int NumDistinctItems { get { return dictSamples.Count; } set { } }

        public int MaxListSize { get; set; } = 100;

        public long SizeGen0 { get; set; }
        public long SizeGen1 { get; set; }
        public long SizeGen2 { get; set; }
        public long SizeGen3 { get; set; }
        public long Total { get; set; }

        Dictionary<string, GCSampleData> dictSamples = new Dictionary<string, GCSampleData>();
        bool PendingReset = false;
        private TextBox _txtStatus;
        private Button _btnGo;
        DockPanel _dpData;
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
xmlns:l=""clr-namespace:{this.GetType().Namespace};assembly={System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location)}"" 
        Margin=""5,5,5,5"">
        <Grid.RowDefinitions>
            <RowDefinition Height=""Auto""/>
            <RowDefinition Height=""*""/>
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width = ""Auto""/>
            <ColumnDefinition Width = ""*""/>
        </Grid.ColumnDefinitions>
        <StackPanel Grid.Row=""0"" HorizontalAlignment=""Left"" VerticalAlignment=""Top"" Orientation=""Vertical"">
            <StackPanel Orientation=""Horizontal"">
                <Label Content=""MaxListSize""/>
                <TextBox Text = ""{{Binding MaxListSize}}"" Width = ""200""/>
            </StackPanel>
            <Button x:Name=""_btnGo"" Content=""_Go"" Width=""45"" ToolTip=""Start/Stop monitoring events"" HorizontalAlignment = ""Left""/>
            <Button x:Name=""_btnReset"" Content=""Reset"" Width=""45"" ToolTip=""Reset history of types collected on next event"" HorizontalAlignment = ""Left""/>
            <StackPanel Orientation=""Horizontal"">
                <Label Content=""TypeName""/>
                <TextBox Text = ""{{Binding TypeName}}"" Width = ""600""/>
            </StackPanel>
            <StackPanel Orientation=""Horizontal"">
                <Label Content=""AllocationAmount""/>
                <TextBox Text = ""{{Binding AllocationAmount, StringFormat=N0}}"" Width = ""400""/>
            </StackPanel>
            <StackPanel Orientation=""Horizontal"">
                <Label Content=""GCCount""/>
                <TextBox Text = ""{{Binding GCCount, StringFormat=N0}}"" Width = ""400""/>
            </StackPanel>
            <StackPanel Orientation=""Horizontal"">
                <Label Content=""GCType""/>
                <TextBox Text = ""{{Binding GCType}}"" Width = ""400""/>
            </StackPanel>
            <StackPanel Orientation=""Horizontal"">
                <Label Content=""GCReason""/>
                <TextBox Text = ""{{Binding GCReason}}"" Width = ""400""/>
            </StackPanel>
            <StackPanel Orientation=""Horizontal"">
                <Label Content=""NumDistinctItems""/>
                <TextBox Text = ""{{Binding NumDistinctItems}}"" Width = ""400""/>
            </StackPanel>
            <TextBox x:Name=""_txtStatus"" FontFamily=""Consolas"" FontSize=""10""
            IsReadOnly=""True"" VerticalScrollBarVisibility=""Auto"" HorizontalScrollBarVisibility=""Auto"" IsUndoEnabled=""False"" VerticalAlignment=""Top""/>
        </StackPanel>
        <StackPanel Grid.Column = ""1"" Orientation= ""Vertical"">
            <Label Content=""Size of Each Gen (not refreshed until after GC end. Ctrl+Alt+Shift+F12 twice will induce GC in VS)""/>
            <StackPanel Orientation=""Horizontal"">
                <Label Content=""Gen0""/>
                <TextBox Text = ""{{Binding SizeGen0, StringFormat=N0}}"" Width = ""400""/>
            </StackPanel>
            <StackPanel Orientation=""Horizontal"">
                <Label Content=""Gen1 ""/>
                <TextBox Text = ""{{Binding SizeGen1, StringFormat=N0}}"" Width = ""400""/>
            </StackPanel>
            <StackPanel Orientation=""Horizontal"">
                <Label Content=""Gen2 ""/>
                <TextBox Text = ""{{Binding SizeGen2, StringFormat=N0}}"" Width = ""400""/>
            </StackPanel>
            <StackPanel Orientation=""Horizontal"">
                <Label Content=""Gen3 ""/>
                <TextBox Text = ""{{Binding SizeGen3, StringFormat=N0}}"" Width = ""400""/>
            </StackPanel>
            <StackPanel Orientation=""Horizontal"">
                <Label Content=""Total""/>
                <TextBox Text = ""{{Binding Total, StringFormat=N0}}"" Width = ""400""/>
            </StackPanel>
        </StackPanel>
        <Grid Grid.Row=""1"">
            <DockPanel x:Name = ""dpData""/>
        </Grid>
    </Grid>
";
            /*
            <ListView x:Name=""lv"" ItemsSource=""{{Binding DataItems}}"" FontFamily=""Consolas"" FontSize=""10"">
                <ListView.View>
                    <GridView>
                        <GridViewColumn DisplayMemberBinding=""{{Binding Count, StringFormat=N0}}"" Header=""Count"" Width = ""80""/>
                        <GridViewColumn DisplayMemberBinding=""{{Binding Size, StringFormat=N0}}"" Header=""Size"" Width = ""100""/>
                        <GridViewColumn DisplayMemberBinding=""{{Binding TypeName}}"" Header=""TypeName""/>
                    </GridView>
                </ListView.View>
            </ListView>
             */
            var grid = (Grid)(XamlReader.Parse(strxaml));
            grid.DataContext = this;
            this.Content = grid;
            this._txtStatus = (TextBox)grid.FindName("_txtStatus");
            this._btnGo = (Button)grid.FindName("_btnGo");
            this._btnGo.Click += BtnGo_Click;
            var btnRest = (Button)grid.FindName("_btnReset");
            btnRest.Click += (_, __) => { PendingReset = true; };
            _dpData = (DockPanel)grid.FindName("dpData");

            _txtStatus.Dispatcher.BeginInvoke(new Action(() =>
            {
                _btnGo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
        }
        void CleanUp()
        {
            _kernelsession?.Dispose();
            _kernelsession = null;
            _userSession?.Dispose();
            _userSession = null;
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
                throw new InvalidOperationException("Must run as zadmin");
            }
            _userSession = new TraceEventSession($"PerfGraphGCMon"); // only 1 at a time can exist with this name in entire machine

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
                    // GCAllocationTick occurs roughly every 100k allocations
                    TypeName = d.TypeName;
                    AllocationAmount = d.AllocationAmount;
                    if (PendingReset)
                    {
                        dictSamples.Clear();
                        PendingReset = false;
                    }
                    if (!dictSamples.TryGetValue(TypeName, out var data))
                    {
                        dictSamples[TypeName] = new GCSampleData() { TypeName = TypeName, Count = 1, Size = AllocationAmount };
                    }
                    else
                    {
                        data.Count++;
                        data.Size += AllocationAmount;
                        RaisePropChanged(nameof(NumDistinctItems));
                    }
                    var lst = (from item in dictSamples.Values
                               orderby item.Size descending
                               select item).Take(MaxListSize);
                    _txtStatus.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            var br = new BrowsePanel(lst, colWidths: new[] { 80, 100, 500 });
                            _dpData.Children.Clear();
                            _dpData.Children.Add(br);
                        }
                        catch (Exception ex)
                        {
                            AddStatusMsg($"Exception.#items = {dictSamples.Count} \r\n{ex}");
                        }
                    }));
                }
            }));
            var gcHeapStatsStream = _userSession.Source.Clr.Observe<GCHeapStatsTraceData>();
            gcHeapStatsStream.Subscribe(new MyObserver<GCHeapStatsTraceData>((d) =>
                    {
                        if (d.ProcessID == _pidToMonitor)
                        {
                            SizeGen0 = d.GenerationSize0;
                            SizeGen1 = d.GenerationSize1;
                            SizeGen2 = d.GenerationSize2;
                            SizeGen3 = d.GenerationSize3;
                            Total = SizeGen0 + SizeGen1 + SizeGen2 + SizeGen3;
                            RaisePropChanged(nameof(SizeGen0));
                            RaisePropChanged(nameof(SizeGen1));
                            RaisePropChanged(nameof(SizeGen2));
                            RaisePropChanged(nameof(SizeGen3));
                            RaisePropChanged(nameof(Total));
                            //AddStatusMsg($"HeapStats {d.GenerationSize0,12:n0}  {d.GenerationSize1,12:n0} {d.GenerationSize2,12:n0} {d.GenerationSize3,12:n0}  {d.GenerationSize4,12:n0}");
                        }
                    }));
            var gcStartStream = _userSession.Source.Clr.Observe<GCStartTraceData>();
            gcStartStream.Subscribe(new MyObserver<GCStartTraceData>((d) =>
            {
                if (d.ProcessID == _pidToMonitor)
                {
                    GCReason = d.Reason;
                    GCType = d.Type;
                    GCCount = d.Count;
                    //AddStatusMsg($"GCStart");
                }
            }));
            var gcEndStream = _userSession.Source.Clr.Observe<GCEndTraceData>();
            gcEndStream.Subscribe(new MyObserver<GCEndTraceData>((d) =>
            {
                if (d.ProcessID == _pidToMonitor)
                {
                    //AddStatusMsg($"GCEnd");
                }
            }));
            //_userSession.Source.AllEvents += (e) =>
            //{
            //    if (e.ProcessID == _pidToMonitor)
            //    {
            //        //                     AddStatusMsg($"{e.EventName}");
            //    }
            //};
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
