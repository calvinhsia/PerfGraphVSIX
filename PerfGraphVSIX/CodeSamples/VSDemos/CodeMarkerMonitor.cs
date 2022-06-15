//Desc: Listens for CodeMarkers and shows them perhaps in a separate process

//Include: ..\Util\MyCodeBaseClass.cs
//Include: ..\Util\AssemblyCreator.cs

//Ref: %VSRoot%\Common7\IDE\PrivateAssemblies\Microsoft.Diagnostics.Tracing.TraceEvent.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\WindowsFormsIntegration.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Windows.Forms.DataVisualization.dll
//Ref: ..\Util\Microsoft.Internal.Performance.CodeMarkers.DesignTime.dll
//Ref: %VSRoot%\Common7\IDE\PrivateAssemblies\Newtonsoft.Json.13.0.1.0\Newtonsoft.Json.dll


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
using Microsoft.Internal.Performance;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
                        Title = $"MyCodeMarkerMonitor monitoring {ProcToMonitor.Id} {ProcToMonitor.MainWindowTitle}",
                    };
                    oWindow.Content = new MyUserControl(ProcToMonitor.Id);
                    oWindow.Show();
                }
                else
                {
                    var vsRoot = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                    var utildir = Path.GetDirectoryName(typeof(CodeMarkerEvent).Assembly.Location);
                    var jsondir = Path.GetDirectoryName(typeof(JsonConvert).Assembly.Location);

                    var addDirs = $"{(Path.Combine(vsRoot, "PublicAssemblies"))};{(Path.Combine(vsRoot, "PrivateAssemblies"))};{Path.GetDirectoryName(typeof(StressUtil).Assembly.Location)};{utildir};{jsondir}";
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
                    var args = $@"""{Assembly.GetExecutingAssembly().Location}"" {nameof(MyMainWindow)} {nameof(MyMainWindow.MyMainMethod)} ""{outputLogFile}"" ""{addDirs}"" ""{ProcToMonitor.Id}"" true";
                    var pListener = Process.Start(
                        asmGCMonitor,
                        args);
                    _logger.LogMessage($"Launched CodeMarkerMonitor {pListener.Id}");
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogMessage($"Exception {ex.ToString()}");
            }
        }
    }

    internal class MyMainWindow
    {
        // arg1 is a file to write our results, arg2 and arg3 show we can pass simple types. e.g. Pass the name of a named pipe.
        internal static async Task MyMainMethod(string outLogFile, string addDirs, int pidToMonitor, bool boolarg)
        {
            _additionalDirs = addDirs;
            File.AppendAllText(outLogFile, $"Starting {nameof(MyMainWindow)}  {Process.GetCurrentProcess().Id}  AddDirs={addDirs}\r\n");

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
                    File.AppendAllText(outLogFile, $"Exception {nameof(MyMainWindow)}  {Process.GetCurrentProcess().Id}   {ex.ToString()}");
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

        Dictionary<string, int> dictEvents = new Dictionary<string, int>();
        List<Tuple<string, int>> _LstEvents = new List<Tuple<string, int>>();
        public List<Tuple<string, int>> LstEvents { get { return _LstEvents; } set { _LstEvents = value; RaisePropChanged(); } }
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
            <ColumnDefinition Width = ""1200""/>
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
                <ListView ItemsSource=""{{Binding LstEvents}}"" FontFamily=""Consolas"" FontSize=""10"" Height = ""800"">
                    <ListView.View>
                        <GridView>
                            <GridViewColumn DisplayMemberBinding=""{{Binding Item1, StringFormat=N0}}"" Header=""Event"" Width = ""350""/>
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
            dictEvents.Clear();
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
                            lock (dictEvents)
                            {
                                var data = (from kvp in dictEvents
                                            select Tuple.Create(kvp.Key, kvp.Value)).ToList();
                                LstEvents = new List<Tuple<string, int>>(data);
                            }
                        }
                        await taskTracing;
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
            _userSession = new TraceEventSession($"PerfGraphMyCodeMarkerMonitor"); // only 1 at a time can exist with this name in entire machine

            //            _userSession.EnableProvider("*Microsoft-VisualStudio-Common", matchAnyKeywords: 0xFFFFFFDF);
            //            _userSession.EnableProvider("*Microsoft-VisualStudio-Common");
            //            _userSession.EnableProvider(new Guid("25c93eda-40a3-596d-950d-998ab963f367"));
            // Microsoft-VisualStudio-Common {25c93eda-40a3-596d-950d-998ab963f367}
            //< Provider Name = "589491ba-4f15-53fe-c376-db7f020f5204" /> < !--Microsoft-VisualStudio-Threading-- >
            //_userSession.EnableProvider(new Guid("EE328C6F-4C94-45F7-ACAF-640C6A447654")); // Retail Asserts
            //_userSession.EnableProvider(new Guid("143A31DB-0372-40B6-B8F1-B4B16ADB5F54"), TraceEventLevel.Verbose, ulong.MaxValue); //MeasurementBlock
            //_userSession.EnableProvider(new Guid("641D7F6C-481C-42E8-AB7E-D18DC5E5CB9E"), TraceEventLevel.Verbose, ulong.MaxValue); // Codemarker
            //_userSession.EnableProvider(new Guid("BF965E67-C7FB-5C5B-D98F-CDF68F8154C2"), TraceEventLevel.Verbose, ulong.MaxValue); // // RoslynEventSource
            var codemarkerProvider = new Guid("641D7F6C-481C-42E8-AB7E-D18DC5E5CB9E");
            _userSession.EnableProvider(codemarkerProvider, TraceEventLevel.Verbose, ulong.MaxValue);

            var vsCommonProvider = new Guid("25c93eda-40a3-596d-950d-998ab963f367");
            _userSession.EnableProvider(vsCommonProvider, TraceEventLevel.Verbose, ulong.MaxValue);

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
                        foreach (var xx in _userSession.Source.Dynamic.DynamicProviders)
                        {
                            AddStatusMsg($"DynPro = {xx.Name}");
                        }
                        if (e.ProviderGuid == codemarkerProvider)
                        {
                            var codemarkerName = CodeMarkerEvent.GetName((int)e.ID);
//                            AddStatusMsg($" {codemarkerName}");
                            lock (dictEvents)
                            {
                                if (!dictEvents.ContainsKey(codemarkerName))
                                {
                                    dictEvents[codemarkerName] = 1;
                                }
                                else
                                {
                                    dictEvents[codemarkerName]++;
                                }
                            }
                        }
                        else
                        {
                            var dict = VSCommonTraceEventParser.GetVSTelemetryPropertiesToDictionary(e);
                            var sb = new StringBuilder();
                            foreach (var kvp in dict)
                            {
                                sb.Append($" {kvp.Key}={kvp.Value}");
                            }
                            sb.Append(e.Dump());
//                            if (eventName == "EventID(1)")
                            {// ReadUnicodeString
//                                AddStatusMsg($"Got str '{str}'   {e.FormattedMessage}");
                            }
//                            var str = UnicodeEncoding.Unicode.GetString(byts);
                            AddStatusMsg($" {e.EventName} {sb}");
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
    public sealed class VSCommonTraceEventParser : TraceEventParser
    {
        public static Guid ProviderGuid = new Guid("{25c93eda-40a3-596d-950d-998ab963f367}"); //ProviderName="Microsoft-VisualStudio-Common"

        public enum LoadPackageReasonPrivate
        {
            Unknown = -1,   // Direct call to IVsShell::LoadPackage
            Preload = -2,   // Pre-load mechanism as used by vslog (obfuscated and undocumented)
            Autload = -3,   // Autoload through a UI context activation
            QS = -4,   // IServiceProvider::QueryService
            EditFct = -5,   // Creating an editor
            PrjFcty = -6,   // Creating a project system
            TlWnd = -7,   // Creating a tool window
            ExecCmd = -8,   // IOleCommandTarget::ExecCmd
            ExtPnt = -9,   // In order to find an extension point (export)
            UIFcty = -10,  // UI factory
            DtSrcFc = -11,  // Datasource factory
            Toolbox = -12,  // Toolbox
            Autmton = -13,  // Automation (GetAutomationObject)
            HlpAbt = -14,  // Help/About information
            StddPrvr = -15,  // AddStandardPreviewer (browser) support
            CmpntPk = -16,  // Component picker (IVsComponentSelectorProvider)
            SlnPrst = -17,  // IVsSolutionProps
            FntClr = -18,  // QueryService call for IVsTextMarkerTypeProvider.
            CmdLnSw = -19,  // DemandLoad specified on AppCommandLoad
            DatConv = -20,  // UIDataConverter
            TlsOpt = -21,  // A page in Tools/Options
            ImpExpS = -22,  // Import/Export settings
        }

        public VSCommonTraceEventParser(TraceEventSource source, bool dontRegister = false) : base(source, dontRegister)
        {
        }

        protected override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback) => throw new NotImplementedException();

        protected override string GetProviderName() => "Microsoft-VisualStudio-Common";

        public static Dictionary<string, object> GetVSTelemetryUserData(TraceEvent traceEvent)
        {
            var userData = traceEvent.PayloadByName("UserData").ToString();
            var dictProps = JsonConvert.DeserializeObject<Dictionary<string, object>>(userData);
            return dictProps;
        }

        /// <summary>
        /// UserData="{ Duration:51882, Properties:[  ] }" Session="{ Version:"15.0.26801.4003", AppName:"devenv" }" RelatedActivityID="04022ab4-17b8-4412-ac06-ca3a71d4516d" 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="traceEvent"></param>
        /// <param name="userdataName"></param>
        /// <returns></returns>
        public static T GetVSTelemetryNonProperty<T>(TraceEvent traceEvent, string userdataName)
        {
            var result = default(T);
            var dictProps = GetVSTelemetryUserData(traceEvent);
            if (dictProps.TryGetValue(userdataName, out var tempRes))
            {
                result = (T)tempRes;
            }
            return result;
        }


        public static T GetVSTelemetryProperty<T>(TraceEvent traceEvent, string propName)
        {
            var dictProps = GetVSTelemetryUserData(traceEvent);
            var props = (JArray)dictProps["Properties"];
            var x = props.Where(p => p["Key"]?.ToString() == propName)?.FirstOrDefault();
            var result = default(T);
            if (x != null)
            {
                var xxx = x["Value"].ToObject<T>();
                result = xxx;
            }
            //T result2 = (T)props.Where(p => p["Key"]?.ToString() == propName)?.FirstOrDefault()?["Value"]?.ToObject(typeof(T));
            return result;
        }
        /// <summary>
        /// UserData="{ Properties:[ { Key:"2dc9daa9-7f2d-11d2-9bfc-00c04f9901d1_inc", Value:"312.4911" }, { Key:"2dc9daa9-7f2d-11d2-9bfc-00c04f9901d1_exc", Value:"312.4911" }, { Key:"2dc9daa9-7f2d-11d2-9bfc-00c04f9901d1_top", Value:"0" }, { Key:"b466091b-94ad-416f-bb8a-42534d423a67_inc", Value:"484.4333" }, { Key:"b466091b-94ad-416f-bb8a-42534d423a67_exc", Value:"171.9422" }, { Key:"b466091b-94ad-416f-bb8a-42534d423a67_top", Value:"484.4333" }, { Key:"efaef2d3-8bdb-4d78-b3eb-b55e44203e80_inc", Value:"125.0091" }, { Key:"efaef2d3-8bdb-4d78-b3eb-b55e44203e80_exc", Value:"125.0091" }, { Key:"efaef2d3-8bdb-4d78-b3eb-b55e44203e80_top", Value:"125.0091" }, { Key:"7f679d93-2eb6-47c9-85eb-f6ad16902662_inc", Value:"1096.0173" }, { Key:"7f679d93-2eb6-47c9-85eb-f6ad16902662_exc", Value:"1059.8442" }, { Key:"7f679d93-2eb6-47c9-85eb-f6ad16902662_top", Value:"49.1362" }, { Key:"6e87cfad-6c05-4adf-9cd7-3b7943875b7c_inc", Value:"1640.5495" }, { Key:"6e87cfad-6c05-4adf-9cd7-3b7943875b7c_exc", Value:"1640.5495" }, { Key:"6e87cfad-6c05-4adf-9cd7-3b7943875b7c_top", Value:"0" }, { Key:"0b93ccc5-bc52-40e9-91a3-7b8b58c4526b_inc", Value:"15156.3384" }, { Key:"0b93ccc5-bc52-40e9-91a3-7b8b58c4526b_exc", Value:"12468.9078" }, { Key:"0b93ccc5-bc52-40e9-91a3-7b8b58c4526b_top", Value:"46.9446" }, { Key:"b80b010d-188c-4b19-b483-6c20d52071ae_inc", Value:"16249.9913" }, { Key:"b80b010d-188c-4b19-b483-6c20d52071ae_exc", Value:"1140.5975" }, { Key:"b80b010d-188c-4b19-b483-6c20d52071ae_top", Value:"0" }, { Key:"d7bb9305-5804-4f92-9cfe-119f4cb0563b_inc", Value:"726.9083" }, { Key:"d7bb9305-5804-4f92-9cfe-119f4cb0563b_exc", Value:"726.9083" }, { Key:"d7bb9305-5804-4f92-9cfe-119f4cb0563b_top", Value:"0" }, { Key:"7fe30a77-37f9-4cf2-83dd-96b207028e1b_inc", Value:"22663.8343" }, { Key:"7fe30a77-37f9-4cf2-83dd-96b207028e1b_exc", Value:"5686.9347" }, { Key:"7fe30a77-37f9-4cf2-83dd-96b207028e1b_top", Value:"212.2958" }, { Key:"53544c4d-e3f8-4aa0-8195-8a8d16019423_inc", Value:"22451.5385" }, { Key:"53544c4d-e3f8-4aa0-8195-8a8d16019423_exc", Value:"0" }, { Key:"53544c4d-e3f8-4aa0-8195-8a8d16019423_top", Value:"22451.5385" }, { Key:"c194969a-a5b1-4af2-a9df-ecf7ce982a05_inc", Value:"279.4837" }, { Key:"c194969a-a5b1-4af2-a9df-ecf7ce982a05_exc", Value:"279.4837" }, { Key:"c194969a-a5b1-4af2-a9df-ecf7ce982a05_top", Value:"279.4837" }, { Key:"TotalExclusiveCost", Value:"23680.142" } ] }" Session="{ Version:"15.0.26801.4003", AppName:"devenv" }" 
        /// </summary>
        /// <param name="traceEvent"></param>
        /// <returns></returns>
        public static Dictionary<string, string> GetVSTelemetryPropertiesToDictionary(TraceEvent traceEvent)
        {
            var dict = new Dictionary<string, string>();
            var result = new StringBuilder();
            var userData = traceEvent.PayloadByName("UserData");
            if (userData != null)
            {
                try
                {
                    if (traceEvent.EventName == "FileWatcher.Logging")// rather than throwing an exception for each of these numerous events
                    {
                        dict["VsEtwLogging"] = userData.ToString();
                    }
                    else
                    {
                        var dictProps = JsonConvert.DeserializeObject<Dictionary<string, object>>(userData.ToString());
                        if (dictProps != null)
                        {
                            if (dictProps.TryGetValue("Properties", out var props))
                            {
                                foreach (var p in (JArray)props)
                                {
                                    result.AppendLine($"{p["Key"]}={p["Value"]}");
                                    dict[(p["Key"]).ToString()] = p["Value"].ToString();
                                }
                            }
                            else
                            {
                                foreach (var entry in dictProps)
                                {
                                    dict[entry.Key] = entry.Value.ToString();
                                }
                            }
                        }
                    }
                }
                catch (Newtonsoft.Json.JsonReaderException)
                {
                    // FileWatcher.Logging, Shell_CostDiagnostics, etc use VsEtwLogging without using telemetry
                    dict["VsEtwLogging"] = userData.ToString();
                }
            }
            return dict;
        }
    }
}