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
using System.Text;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows.Forms.Integration;
using System.Windows.Forms.DataVisualization.Charting;
using System.Text.RegularExpressions;

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
                var vsRoot = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                var addDirs = $"{(Path.Combine(vsRoot, "PublicAssemblies"))};{(Path.Combine(vsRoot, "PrivateAssemblies"))};{Path.GetDirectoryName(typeof(StressUtil).Assembly.Location)}";

                // now we create an assembly, load it in a 64 bit process which will invoke the same method using reflection
                var asmGCMonitor = Path.ChangeExtension(Path.GetTempFileName(), ".exe");
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
                _logger.LogMessage($"Launched JoinableTaskFactoryMonitor {pListener.Id}");
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
                        Title = $"JoinableTaskFactoryMonitor monitoring {pidToMonitor} {ProcToMonitor.MainWindowTitle}",
                    };
                    if (ProcToMonitor.MainWindowHandle != IntPtr.Zero)
                    {
                        var interop = new WindowInteropHelper(oWindow);
                        interop.EnsureHandle();
                        interop.Owner = ProcToMonitor.MainWindowHandle;
                    }
                    oWindow.Content = new MyUserControl(pidToMonitor);
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

        Dictionary<string, int> dictJTFEvents = new Dictionary<string, int>();
        List<Tuple<string, int>> _LstJTFEvents = new List<Tuple<string, int>>();
        public List<Tuple<string, int>> LstJTFEvents { get { return _LstJTFEvents; } set { _LstJTFEvents = value; RaisePropChanged(); } }
        public int UpdateIntervalSecs { get; set; } = 1;
        int _pidToMonitor;
        private TextBox _txtStatus;
        private Button _btnGo;
        bool PendingReset = false;
        private CancellationTokenSource _cts;
        private bool _isTracing;
        private TraceEventSession _kernelsession;
        private TraceEventSession _userSession;

        public MyUserControl(int pidToMonitor)
        {
            this._pidToMonitor = pidToMonitor;
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
                    <Label Content=""UpdateIntervalSecs""/>
                    <TextBox Text = ""{{Binding UpdateIntervalSecs}}"" Width = ""40"" ToolTip=""How often to update display""/>
                    <Button x:Name=""_btnReset"" Content=""Reset"" Width=""45"" ToolTip=""Reset history of JTF events collected on next update"" HorizontalAlignment = ""Left""/>
                    <Button x:Name=""_btnGo"" Content=""_Go"" Width=""45"" ToolTip=""Start/Stop monitoring events"" HorizontalAlignment = ""Left""/>
                </StackPanel>
                <TextBox x:Name=""_txtStatus"" FontFamily=""Consolas"" FontSize=""10""
                IsReadOnly=""True"" VerticalScrollBarVisibility=""Auto"" HorizontalScrollBarVisibility=""Auto"" IsUndoEnabled=""False"" VerticalAlignment=""Top""/>
                <ListView ItemsSource=""{{Binding LstJTFEvents}}"" FontFamily=""Consolas"" FontSize=""10"" Height = ""800"">
                    <ListView.View>
                        <GridView>
                            <GridViewColumn DisplayMemberBinding=""{{Binding Item1, StringFormat=N0}}"" Header=""Event"" Width = ""250""/>
                            <GridViewColumn DisplayMemberBinding=""{{Binding Item2, StringFormat=N0}}"" Header=""Count"" Width = ""100""/>
                        </GridView>
                    </ListView.View>
                </ListView>
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
            InitDictJTF();
            btnRest.Click += (_, __) => { PendingReset = true; };
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
        void InitDictJTF()
        {
            dictJTFEvents.Clear();
            foreach (var val in Enum.GetValues(typeof(JoinableTaskFactoryEventParser.EventIds)))
            {
                dictJTFEvents[val.ToString()] = 0;
            }
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
                    try
                    {
                        _cts = new CancellationTokenSource();
                        _isTracing = true;
                        _btnGo.Content = "Stop";
                        AddStatusMsg($"Listening");
                        var taskTracing = DoTracingAsync();
                        while (_isTracing && !_cts.IsCancellationRequested)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(UpdateIntervalSecs), _cts.Token);
                            var data = (from kvp in dictJTFEvents
                                        select Tuple.Create(kvp.Key, kvp.Value)).ToList();
                            LstJTFEvents = new List<Tuple<string, int>>(data);
                        }
//                        await taskTracing;
                    }
                    catch (TaskCanceledException)
                    {
                    }
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
            _userSession = new TraceEventSession($"PerfGraphJTFMon"); // only 1 at a time can exist with this name in entire machine

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
            _userSession.EnableProvider(JoinableTaskFactoryEventParser.ProviderGuid, TraceEventLevel.Verbose, ulong.MaxValue);


            //var gcAllocStream = _userSession.Source.Clr.Observe<GCAllocationTickTraceData>();
            //gcAllocStream.Subscribe(new MyObserver<GCAllocationTickTraceData>((d) =>
            //{
            //    if (d.ProcessID == _pidToMonitor)
            //    {
            //        // GCAllocationTick occurs roughly every 100k allocations
            //        TypeName = d.TypeName;
            //        AllocationAmount = d.AllocationAmount;
            //        if (PendingReset)
            //        {
            //            dictSamples.Clear();
            //            PendingReset = false;
            //        }
            //        if (!dictSamples.TryGetValue(TypeName, out var data))
            //        {
            //            dictSamples[TypeName] = new GCSampleData() { TypeName = TypeName, Count = 1, Size = AllocationAmount };
            //        }
            //        else
            //        {
            //            data.Count++;
            //            data.Size += AllocationAmount;
            //            RaisePropChanged(nameof(NumDistinctItems));
            //        }
            //        if (!UpdateTypeListOnGC)
            //        {
            //            UpdateTypeList();
            //        }
            //    }
            //}));
            _userSession.Source.AllEvents += (e) =>
            {
                try
                {
                    if (e.ProcessID == _pidToMonitor)
                    {
                        /*

    <Event MSec=  "3565.4141" PID="48436" PName=  "devenv" TID="52720" EventName="PostExecution/Start"
      TimeStamp="06/01/22 12:02:34.163047" ID="15" Version="0" Keywords="0x0000F00000000000" TimeStampQPC="6,937,294,464,097" QPCTime="0.100us"
      Level="Verbose" ProviderName="Microsoft-VisualStudio-Threading" ProviderGuid="589491ba-4f15-53fe-c376-db7f020f5204" ClassicProvider="False" ProcessorNumber="6"
      Opcode="1" Task="65519" Channel="0" PointerSize="8"
      CPU="6" EventIndex="89539" TemplateType="DynamicTraceEventData">
      <PrettyPrint>
        <Event MSec=  "3565.4141" PID="48436" PName=  "devenv" TID="52720" EventName="PostExecution/Start" ProviderName="Microsoft-VisualStudio-Threading" requestId="52,530,826" mainThreadAffinitized="True"/>
      </PrettyPrint>
      <Payload Length="8">
           0:  8a 8e 21  3  1  0  0  0 |                           ..!..... 
      </Payload>
    </Event>    
    HasStack="True" ThreadID="52,720" ProcessorNumber="6" requestId="52,530,826" mainThreadAffinitized="True" 

                         */
                        var eventName = e.EventName;
                        var eventNdx = 0;
                        var match = Regex.Match(eventName, @"EventID\((\d+)\)");
                        if (match.Success)
                        {
                            eventNdx = int.Parse(match.Groups[1].Value);
                            eventName = ((JoinableTaskFactoryEventParser.EventIds)eventNdx).ToString();
                        }
                        var parm1 = 0;
                        var parm2 = 0;
                        var byts = e.EventData();
                        if (byts.Length >= 4)
                        {
                            parm1 = BitConverter.ToInt32(byts, 0);
                        }
                        if (byts.Length >= 8)
                        {
                            parm2 = BitConverter.ToInt32(byts, 4);
                        }
                        if (PendingReset)
                        {
                            InitDictJTF();
                        }
                        if (!dictJTFEvents.ContainsKey(eventName))
                        {
                            AddStatusMsg($"Key not found {eventName}");
                        }
                        else
                        {
                            dictJTFEvents[eventName]++;
//                            AddStatusMsg($"NDx {eventNdx}  {eventName} {dictJTFEvents[eventName]}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddStatusMsg(ex.ToString());
                    throw ex;
                }
            };

            await Task.Run(() =>
            {
                _userSession.Source.Process();
                AddStatusMsg($"Done Source.Process");
            });
        }
    }
    internal class JoinableTaskFactoryEventParser : TraceEventParser
    {
        public static readonly string ProviderName = "Microsoft-VisualStudio-Threading";
        public static readonly Guid ProviderGuid = new Guid("589491ba-4f15-53fe-c376-db7f020f5204");
        static private volatile TraceEvent[] s_templates;
        protected override string GetProviderName() { return ProviderName; }

        public enum EventIds
        {
            /// <summary>
            /// The event ID for the <see cref="ReaderWriterLockIssued(int, AsyncReaderWriterLock.LockKind, int, int)"/> event.
            /// </summary>
            ReaderWriterLockIssuedLockCountsEvent = 1,

            /// <summary>
            /// The event ID for the <see cref="WaitReaderWriterLockStart(int, AsyncReaderWriterLock.LockKind, int, int, int)"/> event.
            /// </summary>
            WaitReaderWriterLockStartEvent = 2,

            /// <summary>
            /// The event ID for the <see cref="WaitReaderWriterLockStop(int, AsyncReaderWriterLock.LockKind)"/> event.
            /// </summary>
            WaitReaderWriterLockStopEvent = 3,

            /// <summary>
            /// The event ID for the <see cref="CompleteOnCurrentThreadStart(int, bool)"/>.
            /// </summary>
            CompleteOnCurrentThreadStartEvent = 11,

            /// <summary>
            /// The event ID for the <see cref="CompleteOnCurrentThreadStop(int)"/>.
            /// </summary>
            CompleteOnCurrentThreadStopEvent = 12,

            /// <summary>
            /// The event ID for the <see cref="WaitSynchronouslyStart()"/>.
            /// </summary>
            WaitSynchronouslyStartEvent = 13,

            /// <summary>
            /// The event ID for the <see cref="WaitSynchronouslyStop()"/>.
            /// </summary>
            WaitSynchronouslyStopEvent = 14,

            /// <summary>
            /// The event ID for the <see cref="PostExecutionStart(int, bool)"/>.
            /// </summary>
            PostExecutionStartEvent = 15,

            /// <summary>
            /// The event ID for the <see cref="PostExecutionStop(int)"/>.
            /// </summary>
            PostExecutionStopEvent = 16,

            /// <summary>
            /// The event ID for the <see cref="CircularJoinableTaskDependencyDetected(int, int)"/>.
            /// </summary>
            CircularJoinableTaskDependencyDetectedEvent = 17,

        }
        public JoinableTaskFactoryEventParser(TraceEventSource source) : base(source)
        {

            //// Subscribe to the GCBulkType events and remember the TypeID -> TypeName mapping. 
            //ClrTraceEventParserState state = State;
            //AddCallbackForEvents<GCBulkTypeTraceData>(delegate (GCBulkTypeTraceData data)
            //{
            //    for (int i = 0; i < data.Count; i++)
            //    {
            //        GCBulkTypeValues value = data.Values(i);
            //        string typeName = value.TypeName;
            //        // The GCBulkType events are logged after the event that needed it.  It really
            //        // should be before, but we compensate by setting the startTime to 0
            //        // Ideally the CLR logs the types before they are used.  
            //        state.SetTypeIDToName(data.ProcessID, value.TypeID, 0, typeName);
            //    }
            //});

        }

        protected override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            if (s_templates == null)
            {
                var templates = new TraceEvent[1];
                //                templates[0] = new GCStartTraceData(null, 1, 1, "GC", GCTaskGuid, 1, "Start", ProviderGuid, ProviderName);

            }
        }
    }
    public sealed class GCEndTraceData : TraceEvent
    {
        public int Count { get { return GetInt32At(0); } }
        public int Depth { get { if (Version >= 1) { return GetInt32At(4); } return GetInt16At(4); } }
        public int ClrInstanceID { get { if (Version >= 1) { return GetInt16At(8); } return 0; } }

        internal GCEndTraceData(Action<GCEndTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            Action = action;
        }
        protected override void Dispatch()
        {
            Action(this);
        }
        protected override Delegate Target
        {
            get { return Action; }
            set { Action = (Action<GCEndTraceData>)value; }
        }
        protected override void Validate()
        {
            //Debug.Assert(!(Version == 0 && EventDataLength < 6));           // HAND_MODIFIED <
            //Debug.Assert(!(Version == 1 && EventDataLength != 10));
            //Debug.Assert(!(Version > 1 && EventDataLength < 10));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttrib(sb, "Count", Count);
            XmlAttrib(sb, "Depth", Depth);
            XmlAttrib(sb, "ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "Count", "Depth", "ClrInstanceID" };
                }

                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Count;
                case 1:
                    return Depth;
                case 2:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<GCEndTraceData> Action;
    }
}