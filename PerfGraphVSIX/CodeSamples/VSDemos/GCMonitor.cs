//Desc: Listens for GC ETW events and shows in a separate process the most common allocations.

//Include: ..\Util\MyCodeBaseClass.cs
//Include: ..\Util\AssemblyCreator.cs

//Ref: %VSRoot%\Common7\IDE\PrivateAssemblies\Microsoft.Diagnostics.Tracing.TraceEvent.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\WindowsFormsIntegration.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Windows.Forms.DataVisualization.dll
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
using System.Windows.Forms.Integration;
using System.Windows.Forms.DataVisualization.Charting;

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
                var addDirs = $"{(Path.Combine(vsRoot, "PublicAssemblies"))};{(Path.Combine(vsRoot, "PrivateAssemblies"))};{Path.GetDirectoryName(typeof(StressUtil).Assembly.Location)}";

                // now we create an assembly, load it in a 64 bit process which will invoke the same method using reflection
                var asmGCMonitor = Path.ChangeExtension(Path.GetTempFileName(), ".exe");
                oopProcName = Path.GetFileNameWithoutExtension(asmGCMonitor);
                var type = new AssemblyCreator().CreateAssembly
                    (
                        asmGCMonitor,
                        PortableExecutableKinds.PE32Plus,
                        ImageFileMachine.AMD64,
                        AdditionalAssemblyPaths: addDirs, // Microsoft.VisualStudio.Shell.Interop
                        logOutput: false // for diagnostics
                    );
                var pidToMonitor = Process.GetCurrentProcess().Id;
                var outputLogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyTestAsm.log");
                File.Delete(outputLogFile);
                var args = $@"""{Assembly.GetExecutingAssembly().Location}"" {nameof(MyEtwMainWindow)} {nameof(MyEtwMainWindow.MyMainMethod)} ""{outputLogFile}"" ""{addDirs}"" ""{pidToMonitor}"" true";
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
        internal static async Task MyMainMethod(string outLogFile, string addDirs, int pidToMonitor, bool boolarg)
        {
            _additionalDirs = addDirs;
            File.AppendAllText(outLogFile, $"Starting {nameof(MyEtwMainWindow)}  {Process.GetCurrentProcess().Id}  AddDirs={addDirs}\r\n");

            var tcs = new TaskCompletionSource<int>();
            var thread = new Thread((s) => // need to create our own STA thread (can't use threadpool)
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

        public long SizeGen0 { get; set; }
        public long SizeGen1 { get; set; }
        public long SizeGen2 { get; set; }
        public long SizeGen3 { get; set; }
        public long Total { get; set; }

        Chart _chartGen0;
        Chart _chartGen1;
        Chart _chartGen2;
        Chart _chartGen3;
        Chart _chartTot;

        Dictionary<string, GCSampleData> dictSamples = new Dictionary<string, GCSampleData>(); // Type=>GCSampleData

        List<GCSampleData> _LstDataTypes = new List<GCSampleData>();
        public List<GCSampleData> LstDataTypes { get { return _LstDataTypes; } set { _LstDataTypes = value; RaisePropChanged(); } }

        Dictionary<string, int> dictGCTypes = new Dictionary<string, int>(); // GC Type=>Count
        List<Tuple<string, int>> _LstGCTypes = new List<Tuple<string, int>>();
        public List<Tuple<string, int>> LstGCTypes { get { return _LstGCTypes; } set { _LstGCTypes = value; RaisePropChanged(); } }

        Dictionary<string, int> dictGCReasons = new Dictionary<string, int>(); // GC Reason=>Count
        List<Tuple<string, int>> _LstGCReasons = new List<Tuple<string, int>>();
        public List<Tuple<string, int>> LstGCReasons { get { return _LstGCReasons; } set { _LstGCReasons = value; RaisePropChanged(); } }

        public int MaxListSize { get; set; } = 20;
        public int NumDataPoints { get; set; } = 100;
        public bool UpdateTypeListOnGC { get; set; } = false;

        bool PendingReset = false;
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
            var x = new WindowsFormsHost(); //prime the pump
            // xmlns:l="clr-namespace:WpfApp1;assembly=WpfApp1"
            // the C# string requires quotes to be doubled
            var strxaml =
$@"<Grid
xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
xmlns:l=""clr-namespace:{this.GetType().Namespace};assembly={System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location)}"" 
        Margin=""5,5,5,5"">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width = ""900""/>
            <ColumnDefinition Width = ""3""/>
            <ColumnDefinition Width = ""*""/>
        </Grid.ColumnDefinitions>
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height=""Auto""/>
                <RowDefinition Height=""*""/>
            </Grid.RowDefinitions>
            <StackPanel HorizontalAlignment=""Left"" VerticalAlignment=""Top"" Orientation=""Vertical"">
                <StackPanel Orientation=""Horizontal"">
                    <Label Content=""MaxListSize""/>
                    <TextBox Text = ""{{Binding MaxListSize}}"" Width = ""40"" ToolTip=""# datatypes to keep in history""/>
                    <Label Content=""NumDataPoints""/>
                    <TextBox Text = ""{{Binding NumDataPoints}}"" Width = ""40"" ToolTip=""# of points in graphs""/>
                    <CheckBox Content=""UpdateTypeListOnGC"" IsChecked = ""{{Binding UpdateTypeListOnGC}}"" 
ToolTip=""Update the list of types on each AllocationTick (more CPU hit, fresher data) or GC (less CPU hit). Use the ChildProc sample to monitor CPU use""/>
                    <Button x:Name=""_btnReset"" Content=""Reset"" Width=""45"" ToolTip=""Reset history of types collected on next event"" HorizontalAlignment = ""Left""/>
                    <Button x:Name=""_btnGo"" Content=""_Go"" Width=""45"" ToolTip=""Start/Stop monitoring events"" HorizontalAlignment = ""Left""/>
                </StackPanel>
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
                <ListView ItemsSource=""{{Binding LstGCTypes}}"" FontFamily=""Consolas"" FontSize=""10"" Height = ""100"">
                    <ListView.View>
                        <GridView>
                            <GridViewColumn DisplayMemberBinding=""{{Binding Item1, StringFormat=N0}}"" Header=""GCType"" Width = ""100""/>
                            <GridViewColumn DisplayMemberBinding=""{{Binding Item2, StringFormat=N0}}"" Header=""Count"" Width = ""100""/>
                        </GridView>
                    </ListView.View>
                </ListView>
                <ListView ItemsSource=""{{Binding LstGCReasons}}"" FontFamily=""Consolas"" FontSize=""10""  Height = ""100"">
                    <ListView.View>
                        <GridView>
                            <GridViewColumn DisplayMemberBinding=""{{Binding Item1, StringFormat=N0}}"" Header=""GCReason"" Width = ""100""/>
                            <GridViewColumn DisplayMemberBinding=""{{Binding Item2, StringFormat=N0}}"" Header=""Count"" Width = ""100""/>
                        </GridView>
                    </ListView.View>
                </ListView>
                <ListView ItemsSource=""{{Binding LstDataTypes}}"" FontFamily=""Consolas"" FontSize=""10""  Height = ""600"">
                    <ListView.View>
                        <GridView>
                            <GridViewColumn DisplayMemberBinding=""{{Binding Count, StringFormat=N0}}"" Header=""Count"" Width = ""80""/>
                            <GridViewColumn DisplayMemberBinding=""{{Binding Size, StringFormat=N0}}"" Header=""Size"" Width = ""100""/>
                            <GridViewColumn DisplayMemberBinding=""{{Binding TypeName}}"" Header=""TypeName"" Width = ""600""/>
                        </GridView>
                    </ListView.View>
                </ListView>
            </StackPanel>
        </Grid>
        <GridSplitter Grid.Column = ""1"" HorizontalAlignment=""Center"" VerticalAlignment=""Stretch"" Width = ""3"" Background=""LightBlue""/>
        <Grid Grid.Column=""2"">
            <StackPanel Grid.Row = ""1"" Orientation= ""Vertical"">
                <Label Content=""Size of Each Gen (not refreshed until after GC end. Ctrl+Alt+Shift+F12 twice will induce GC in VS)""/>
                <StackPanel Orientation=""Horizontal"">
                    <Label Content=""Gen0""/>
                    <TextBlock Text = ""{{Binding SizeGen0, StringFormat=N0}}"" Width = ""90"" Margin=""0,5,0,0""/>
                    <WindowsFormsHost x:Name=""wfhostGen0"" Height=""200"" Width = ""500""/>
                </StackPanel>
                <StackPanel Orientation=""Horizontal"">
                    <Label Content=""Gen1 ""/>
                    <TextBlock Text = ""{{Binding SizeGen1, StringFormat=N0}}"" Width = ""90"" Margin=""0,5,0,0""/>
                    <WindowsFormsHost x:Name=""wfhostGen1"" Height=""200"" Width = ""500""/>
                </StackPanel>
                <StackPanel Orientation=""Horizontal"">
                    <Label Content=""Gen2 ""/>
                    <TextBlock Text = ""{{Binding SizeGen2, StringFormat=N0}}"" Width = ""90"" Margin=""0,5,0,0""/>
                    <WindowsFormsHost x:Name=""wfhostGen2"" Height=""200"" Width = ""500""/>
                </StackPanel>
                <StackPanel Orientation=""Horizontal"">
                    <Label Content=""Gen3 ""/>
                    <TextBlock Text = ""{{Binding SizeGen3, StringFormat=N0}}"" Width = ""90"" Margin=""0,5,0,0""/>
                    <WindowsFormsHost x:Name=""wfhostGen3"" Height=""200"" Width = ""500""/>
                </StackPanel>
                <StackPanel Orientation=""Horizontal"">
                    <Label Content=""Total""/>
                    <TextBlock Text = ""{{Binding Total, StringFormat=N0}}"" Width = ""90"" Margin=""0,5,0,0""/>
                    <WindowsFormsHost x:Name=""wfhostTot"" Height=""200"" Width = ""500""/>
                </StackPanel>
            </StackPanel>
        </Grid>
    </Grid>
";
            /*
             */
            var grid = (System.Windows.Controls.Grid)(XamlReader.Parse(strxaml));
            grid.DataContext = this;
            this.Content = grid;
            this._txtStatus = (TextBox)grid.FindName("_txtStatus");
            this._btnGo = (Button)grid.FindName("_btnGo");
            this._btnGo.Click += BtnGo_Click;
            var btnRest = (Button)grid.FindName("_btnReset");
            btnRest.Click += (_, __) => { PendingReset = true; };
            var _dpData = (DockPanel)grid.FindName("dpData");
            _chartGen0 = InitChart("wfhostGen0");
            _chartGen1 = InitChart("wfhostGen1");
            _chartGen2 = InitChart("wfhostGen2");
            _chartGen3 = InitChart("wfhostGen3");
            _chartTot = InitChart("wfhostTot");
            Chart InitChart(string wfhostName)
            {
                var wfhost = (WindowsFormsHost)grid.FindName(wfhostName);
                var chart = new Chart();
                wfhost.Child = chart;
                var chartArea = new ChartArea();
                chartArea.AxisY.LabelStyle.Format = "{0:n0}";
                chartArea.AxisY.LabelStyle.Font = new System.Drawing.Font("Consolas", 12);
                chartArea.AxisY.IsStartedFromZero = false;
                chart.ChartAreas.Add(chartArea);
                var series = new Series
                {
                    ChartType = SeriesChartType.Line,
                    Name = "Total"
                };
                chart.Series.Add(series);
                return chart;
            }
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
                throw new InvalidOperationException("Must run as admin");
            }
            _userSession = new TraceEventSession($"PerfGraphGCMon"); // only 1 at a time can exist with this name in entire machine

            var gguid = TraceEventProviders.GetEventSourceGuidFromName("Microsoft-VisualStudio-Threading");
            AddStatusMsg($"Got guid {gguid}");

            //            _userSession.EnableProvider("*Microsoft-VisualStudio-Common", matchAnyKeywords: 0xFFFFFFDF);
            //            _userSession.EnableProvider("*Microsoft-VisualStudio-Common");
            //            _userSession.EnableProvider(new Guid("25c93eda-40a3-596d-950d-998ab963f367"));
            // Microsoft-VisualStudio-Common {25c93eda-40a3-596d-950d-998ab963f367}
            //< Provider Name = "589491ba-4f15-53fe-c376-db7f020f5204" /> < !--Microsoft-VisualStudio-Threading-- >
            //_userSession.EnableProvider(new Guid("EE328C6F-4C94-45F7-ACAF-640C6A447654")); // Retail Asserts
            //_userSession.EnableProvider(new Guid("143A31DB-0372-40B6-B8F1-B4B16ADB5F54"), TraceEventLevel.Verbose, ulong.MaxValue); //MeasurementBlock
            //_userSession.EnableProvider(new Guid("641D7F6C-481C-42E8-AB7E-D18DC5E5CB9E"), TraceEventLevel.Verbose, ulong.MaxValue); // Codemarker
            //_userSession.EnableProvider(new Guid("BF965E67-C7FB-5C5B-D98F-CDF68F8154C2"), TraceEventLevel.Verbose, ulong.MaxValue); // // RoslynEventSource


            _userSession.EnableProvider(ClrTraceEventParser.ProviderGuid, TraceEventLevel.Verbose, (ulong)(ClrTraceEventParser.Keywords.Default));
            var gcAllocStream = _userSession.Source.Clr.Observe<GCAllocationTickTraceData>();
            void UpdateTypeList()
            {
                lock (dictSamples)
                {
                    var lst = (from item in dictSamples.Values
                               orderby item.Size descending
                               select item).Take(MaxListSize);

                    LstDataTypes = new List<GCSampleData>(lst);
                }
                lock (dictGCTypes)
                {
                    LstGCTypes = (from item in dictGCTypes
                                  orderby item.Key
                                  select Tuple.Create(item.Key, item.Value)).ToList();
                }
                lock (dictGCReasons)
                {
                    LstGCReasons = (from item in dictGCReasons
                                    orderby item.Key
                                    select Tuple.Create(item.Key, item.Value)).ToList();
                }
            }
            gcAllocStream.Subscribe(new MyObserver<GCAllocationTickTraceData>((d) =>
            {
                if (d.ProcessID == _pidToMonitor)
                {
                    // GCAllocationTick occurs roughly every 100k allocations
                    TypeName = d.TypeName;
                    AllocationAmount = d.AllocationAmount;
                    lock (dictSamples)
                    {
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
                    }
                    if (!UpdateTypeListOnGC)
                    {
                        UpdateTypeList();
                    }
                    //_txtStatus.Dispatcher.BeginInvoke(new Action(() =>
                    //{
                    //    try
                    //    {

                    //        //    var br = new BrowsePanel(lst, colWidths: new[] { 80, 100, 500 });
                    //        //    _dpData.Children.Clear();
                    //        //    _dpData.Children.Add(br);
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        AddStatusMsg($"Exception.#items = {dictSamples.Count} \r\n{ex}");
                    //    }
                    //}));
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
                    _txtStatus.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (UpdateTypeListOnGC)
                            {
                                UpdateTypeList();
                            }
                            UpdateChart(_chartGen0, SizeGen0);
                            UpdateChart(_chartGen1, SizeGen1);
                            UpdateChart(_chartGen2, SizeGen2);
                            UpdateChart(_chartGen3, SizeGen3);
                            UpdateChart(_chartTot, Total);
                            void UpdateChart(Chart chart, long value)
                            {
                                var series = chart.Series[0];
                                if (series.Points.Count() >= NumDataPoints)
                                {
                                    series.Points.RemoveAt(0);
                                    for (int i = 0; i < series.Points.Count(); i++)
                                    {
                                        series.Points[i].XValue = i;
                                    }
                                }
                                series.Points.Add(new DataPoint(series.Points.Count(), value));
                                chart.ChartAreas[0].RecalculateAxesScale();
                                chart.DataBind();
                            }
                        }
                        catch (Exception ex)
                        {
                            AddStatusMsg(ex.ToString());
                        }
                    }));
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
                    lock (dictGCTypes)
                    {
                        if (!dictGCTypes.ContainsKey(GCType.ToString()))
                        {
                            dictGCTypes[GCType.ToString()] = 1;
                        }
                        else
                        {
                            dictGCTypes[GCType.ToString()]++;
                        }
                    }
                    lock (dictGCReasons)
                    {
                        if (!dictGCReasons.ContainsKey(GCReason.ToString()))
                        {
                            dictGCReasons[GCReason.ToString()] = 1;
                        }
                        else
                        {
                            dictGCReasons[GCReason.ToString()]++;
                        }
                    }
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
            //            _kernelsession = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
            if (_kernelsession != null)
            {
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
            }
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
