//Desc: Creates ETW Events and Listens for them, perhaps in a separate process

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
using System.Diagnostics.Tracing;

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
                var ProcToMonitor = Process.GetCurrentProcess();
                var useExternalProcess = true;
                if (!useExternalProcess)
                {
                    var oWindow = new Window()
                    {
                        Title = $"MyEventSourceMonitor monitoring {ProcToMonitor.Id} {ProcToMonitor.MainWindowTitle}",
                    };
                    oWindow.Content = new MyUserControl(ProcToMonitor.Id);
                    oWindow.Show();
                }
                else
                {
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
                    var outputLogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyTestAsm.log");
                    File.Delete(outputLogFile);
                    var args = $@"""{Assembly.GetExecutingAssembly().Location}"" {nameof(MyEtwMainWindow)} {nameof(MyEtwMainWindow.MyMainMethod)} ""{outputLogFile}"" ""{addDirs}"" ""{ProcToMonitor.Id}"" true";
                    var pListener = Process.Start(
                        asmGCMonitor,
                        args);
                    _logger.LogMessage($"Launched EtwEventSourceMonitor {pListener.Id}");
                }
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), _CancellationTokenExecuteCode);
                    while (!_CancellationTokenExecuteCode.IsCancellationRequested)
                    {
                        _logger.LogMessage($"Raising Event ");
                        MyEventSource.Log.SomeEvent("event msg");
                        MyEventSource.Log.SomeEvent2("event msg2");

                        await Task.Delay(TimeSpan.FromSeconds(2), _CancellationTokenExecuteCode);
                    }
                }
                catch (TaskCanceledException)
                {
                }
                _logger.LogMessage($"Done Raising Events");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogMessage($"Exception {ex.ToString()}");
            }
        }
    }
    [EventSource(Name = "MyEtwEventSource")] // =='MyEtwEventSource'== 'b912a57c-5711-5bbd-1440-05e64115baa3'
    class MyEventSource : EventSource
    {
        public static MyEventSource Log = new MyEventSource();
        [Event(1, Message = "somemsg{0}", Level = EventLevel.Informational)]
        public void SomeEvent(string eventMsg) { WriteEvent(1, eventMsg); }
        [Event(2, Message = "somemsg2")]
        public void SomeEvent2(string eventMsg) { WriteEvent(2, eventMsg); }
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
                        Title = $"MyEventSourceMonitor monitoring {pidToMonitor} {ProcToMonitor.MainWindowTitle}",
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
                <TextBox x:Name=""_txtStatus"" FontFamily=""Consolas"" FontSize=""10"" MaxHeight=""400""
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
            dictJTFEvents["EventID(1)"] = 0;
            dictJTFEvents["EventID(2)"] = 0;
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
            _userSession = new TraceEventSession($"PerfGraphMyEventSourceMonitor"); // only 1 at a time can exist with this name in entire machine

            var gguid = TraceEventProviders.GetEventSourceGuidFromName("MyEtwEventSource");
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
            _userSession.EnableProvider("MyEtwEventSource", TraceEventLevel.Verbose, ulong.MaxValue);
            _userSession.Source.AllEvents += (e) =>
            {
                try
                {
                    if (e.ProcessID == _pidToMonitor)
                    {
                        /*

        /*
<Event MSec=  "3577.5348" PID="21332" PName=  "devenv" TID="13408" EventName="SomeEvent"
  TimeStamp="06/01/22 18:02:49.443018" ID="1" Version="0" Keywords="0x0000F00000000000" TimeStampQPC="7,153,447,263,803" QPCTime="0.100us"
  Level="Informational" ProviderName="MyEtwEventSource" ProviderGuid="b912a57c-5711-5bbd-1440-05e64115baa3" ClassicProvider="False" ProcessorNumber="3"
  Opcode="0" Task="65533" Channel="0" PointerSize="8"
  CPU="3" EventIndex="95580" TemplateType="DynamicTraceEventData">
  <PrettyPrint>
    <Event MSec=  "3577.5348" PID="21332" PName=  "devenv" TID="13408" EventName="SomeEvent" ProviderName="MyEtwEventSource" FormattedMessage="somemsg" eventMsg="event msg"/>
  </PrettyPrint>
  <Payload Length="20">
       0:  65  0 76  0 65  0 6e  0 | 74  0 20  0 6d  0 73  0   e.v.e.n. t. .m.s.
      10:  67  0  0  0             |                           g...
  </Payload>
</Event>
Name
+ Process64 devenv (21332) Args:  
 + Thread (13408) CPU=3048ms (VS Main)
  + ntdll!?
   + kernel32!?
    + devenv!?
     + msenv!?
      + user32!?
       + clr!UMThunkStub
        + windowsbase.ni!?
         + mscorlib.ni!?
          + windowsbase.ni!?
           + microsoft.visualstudio.threading.ni!?
            + mscorlib.ni!?
             + ManagedModule!MyCodeToExecute.MyClass+<DoItAsync>d__2.MoveNext()
              + ManagedModule!MyEventSource.SomeEvent
               + mscorlib.ni!?
                + ntdll!?
                 + Event MyEtwEventSource/SomeEvent
        */
                        var eventName = e.EventName;
                        var eventNdx = 0;
                        var match = Regex.Match(eventName, @"EventID\((\d+)\)");
                        if (match.Success)
                        {
                            eventNdx = int.Parse(match.Groups[1].Value);
                            //                            eventName = ((JoinableTaskFactoryEventParser.EventIds)eventNdx).ToString();
                        }
                        var parm1 = 0;
                        var parm2 = 0;
                        var byts = e.EventData();
                        if (eventName == "EventID(1)")
                        {// ReadUnicodeString
                            var str = UnicodeEncoding.Unicode.GetString(byts);
                            AddStatusMsg($"Got str '{str}'   {e.FormattedMessage}");
                        }
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
}