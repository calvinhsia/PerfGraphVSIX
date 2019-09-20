using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using Task = System.Threading.Tasks.Task;


namespace PerfGraphVSIX
{
    public class PerfGraph : UserControl, INotifyPropertyChanged
    {
        TextBox _txtStatus;
        EditorTracker _editorTracker;
        ObjTracker _objTracker;
        JoinableTask _tskDoPerfMonitoring;
        CancellationTokenSource _ctsPcounter;
        string _LastStatMsg;
        readonly Chart _chart;

        public ObservableCollection<UIElement> _OpenedViews { get; set; } = new ObservableCollection<UIElement>();
        public ObservableCollection<UIElement> _LeakedViews { get; set; } = new ObservableCollection<UIElement>();

        public ObservableCollection<UIElement> _CreatedObjs { get; set; } = new ObservableCollection<UIElement>();
        public ObservableCollection<UIElement> _LeakedObjs { get; set; } = new ObservableCollection<UIElement>();

        /// <summary>
        /// PerfCounters updated periodically. Safe to change without stopping the monitoring
        /// </summary>
        public int UpdateInterval { get; set; } = 1000;
        public int NumDataPoints { get; set; } = 100;

        public bool ScaleByteCounters { get; set; } = false;
        public bool SetMaxGraphTo100 { get; set; } = false;

        public string LastStatMsg { get { return _LastStatMsg; } set { _LastStatMsg = value; RaisePropChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        void RaisePropChanged([CallerMemberName] string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public static readonly List<PerfCounterData> _lstPerfCounterDefinitions = new List<PerfCounterData>()
        {
            {new PerfCounterData(PerfCounterType.ProcessorPctTime, "Process","% Processor Time","ID Process" )} ,
            {new PerfCounterData(PerfCounterType.ProcessorPrivateBytes, "Process","Private Bytes","ID Process") },
            {new PerfCounterData(PerfCounterType.ProcessorVirtualBytes, "Process","Virtual Bytes","ID Process") },
            {new PerfCounterData(PerfCounterType.ProcessorWorkingSet, "Process","Working Set","ID Process") },
            {new PerfCounterData(PerfCounterType.GCPctTime, ".NET CLR Memory","% Time in GC","Process ID") },
            {new PerfCounterData(PerfCounterType.GCBytesInAllHeaps, ".NET CLR Memory","# Bytes in all Heaps","Process ID" )},
            {new PerfCounterData(PerfCounterType.GCAllocatedBytesPerSec, ".NET CLR Memory","Allocated Bytes/sec","Process ID") },
            {new PerfCounterData(PerfCounterType.PageFaultsPerSec, "Process","Page Faults/sec","ID Process") },
            {new PerfCounterData(PerfCounterType.ThreadCount, "Process","Thread Count","ID Process") },
            {new PerfCounterData(PerfCounterType.KernelHandleCount, "Process","Handle Count","ID Process") },
            {new PerfCounterData(PerfCounterType.GDIHandleCount, "GetGuiResources","GDIHandles",string.Empty) },
            {new PerfCounterData(PerfCounterType.UserHandleCount, "GetGuiResources","UserHandles",string.Empty) },
        };

        public PerfGraph()
        {
            try
            {
                // Make a namespace referring to our namespace and assembly
                // using the prefix "l:"
                //xmlns:l=""clr-namespace:Fish;assembly=Fish"""
                var asm = Path.GetFileNameWithoutExtension(GetType().Assembly.Location);
                var xmlns = $@"xmlns:l=""clr-namespace:{GetType().Namespace};assembly={asm}""";
                //there are a lot of quotes (and braces) in XAML
                //and the C# string requires quotes to be doubled
                var strxaml =
@"
<TabControl
xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
" + xmlns + // add our xaml namespace
@">
    <TabItem Header=""Main"">
        <StackPanel Orientation=""Vertical"" Name = ""spMain""/>
    </TabItem>
    <TabItem Header = ""EditorTracker"" ToolTip=""Track Editor instances"">
        <StackPanel Orientation=""Vertical"">
            <Label Content=""Opened TextViews"" ToolTip=""Views that are currently opened. (Refreshed by UpdateInterval, which does GC)""/>
            <ListBox ItemsSource=""{Binding Path=_OpenedViews}""/>
            <Label Content=""Leaked TextViews"" ToolTip=""Views that have ITextView.IsClosed==true, but still in memory (Refreshed by UpdateInterval, which does GC). Thanks to David Pugh""/>
            <ListBox ItemsSource=""{Binding Path=_LeakedViews}"" MaxHeight = ""200""/>
        </StackPanel>
    </TabItem>
    <TabItem Header = ""ObjectTracker"" ToolTip=""Track Object instances. Requires ClrProfilerAPI"">
        <StackPanel Orientation=""Vertical"">
            <Label Content=""Created Objects"" ToolTip=""Objs that are currently in memory. (Refreshed by UpdateInterval, which does GC)""/>
            <ListBox ItemsSource=""{Binding Path=_CreatedObjs}""/>
            <Label Content=""Leaked Objects"" ToolTip=""Views that have Objects.IsClosed or *disposed* ==true, but still in memory (Refreshed by UpdateInterval, which does GC).""/>
            <ListBox ItemsSource=""{Binding Path=_LeakedObjs}"" MaxHeight = ""200""/>
        </StackPanel>
    </TabItem>
    <TabItem Header = ""Options"">
        <Grid>
            <Grid.ColumnDefinitions>
            <ColumnDefinition Width = ""300""/>
            <ColumnDefinition/>
            </Grid.ColumnDefinitions>
            <StackPanel Orientation = ""Vertical"" Grid.Column=""0"">
                <StackPanel Orientation=""Horizontal"">
                    <Label Content=""Update Interval""/>
                    <l:MyTextBox Name =""txtUpdateInterval"" Text=""{Binding Path =UpdateInterval}"" ToolTip=""Update graph in MilliSeconds. Every sample does a Tools.ForceGC. Set to 0 for manual sample only""/>
                </StackPanel>
                <StackPanel Orientation=""Horizontal"">
                    <Label Content=""# Data Points in graph""/>
                    <l:MyTextBox Text=""{Binding Path =NumDataPoints}"" ToolTip=""Number of Data points (x axis). Will change on next Reset""/>
                </StackPanel>
                <StackPanel Orientation=""Horizontal"">
                    <CheckBox Content=""Scale 'Byte' counters (but not BytePerSec)"" IsChecked=""{Binding Path =ScaleByteCounters}"" ToolTip=""for Byte counters, eg VirtualBytes, scale to be a percent of 4Gigs""/>
                </StackPanel>
                <StackPanel Orientation=""Horizontal"">
                    <CheckBox Name=""chkShowStatusHistory"" Content=""Show Status History"" IsChecked=""True"" ToolTip=""Show a textbox which accumulates history of samples""/>
                </StackPanel>
            </StackPanel>
            <DockPanel Grid.Column=""1"">
                <ListBox Name=""lbPCounters"" SelectionMode=""Multiple"" MaxHeight = ""300"" VerticalAlignment=""Top"" ToolTip=""Multiselect various counters"" />
            </DockPanel>
        </Grid>
    </TabItem>
</TabControl>
";
                var xmlReader = XmlReader.Create(new StringReader(strxaml));

                var tabControl = (TabControl)XamlReader.Load(xmlReader);
                tabControl.DataContext = this;
                this.Content = tabControl;

                var spMain = (StackPanel)tabControl.FindName("spMain");
                var txtUpdateInterval = (MyTextBox)tabControl.FindName("txtUpdateInterval");
                var chkShowStatusHistory = (CheckBox)tabControl.FindName("chkShowStatusHistory");
                var lbPCounters = (ListBox)tabControl.FindName("lbPCounters");
                _editorTracker = PerfGraphToolWindowPackage.ComponentModel.GetService<EditorTracker>();
                _objTracker = new ObjTracker();

                txtUpdateInterval.LostFocus += (o, e) =>
                  {
                      ResetPerfCounterMonitor();
                  };

                chkShowStatusHistory.Checked += (o, e) =>
                {
                    if (_txtStatus != null)
                    {
                        spMain.Children.Remove(_txtStatus); // remove prior one
                    }
                    _txtStatus = new TextBox()
                    {
                        IsReadOnly = true,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        IsUndoEnabled = false,
                        FontFamily = _fontFamily,
                        FontSize = 10,
                        Height = 200,
                        MaxHeight = 200,
                        HorizontalContentAlignment = HorizontalAlignment.Left
                    };
                    spMain.Children.Add(_txtStatus);
                };
                chkShowStatusHistory.Unchecked += (o, e) =>
                {
                    spMain.Children.Remove(_txtStatus);
                    _txtStatus.Text = string.Empty; // keep around so don't have thread contention
                };

                lbPCounters.ItemsSource = _lstPerfCounterDefinitions.Select(s => s.perfCounterType);
                lbPCounters.SelectedIndex = 1;
                _lstPerfCounterDefinitions.Where(s => s.perfCounterType == PerfCounterType.ProcessorPrivateBytes).Single().IsEnabled = true;
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
                lbPCounters.SelectionChanged += async (ol, el) =>
                {
                    try
                    {
                        lbPCounters.IsEnabled = false;
                        // cancel the perf monitoring
                        _ctsPcounter?.Cancel();
                        // before we wait for cancel to finish we can do some work
                        PerfCounterType pctrEnum = PerfCounterType.None;
                        foreach (var itm in lbPCounters.SelectedItems)
                        {
                            pctrEnum |= (PerfCounterType)Enum.Parse(typeof(PerfCounterType), itm.ToString());
                        }
                        AddStatusMsgAsync($"Setting counters to {pctrEnum.ToString()}").Forget();
                        // wait for it to be done cancelling
                        if (_tskDoPerfMonitoring != null)
                        {
                            await _tskDoPerfMonitoring;
                        }
                        await Task.Run(() =>
                        {
                            // run on threadpool thread
                            lock (_lstPerfCounterDefinitions)
                            {
                                foreach (var itm in _lstPerfCounterDefinitions)
                                {
                                    itm.IsEnabled = pctrEnum.HasFlag(itm.perfCounterType);
                                }
                                ResetPerfCounterMonitor();
                            }
                        });
                        AddStatusMsgAsync($"SelectionChanged done").Forget();
                        lbPCounters.IsEnabled = true;
                        el.Handled = true;
                    }
                    catch (Exception)
                    {
                    }
                };

                _chart = new Chart()
                {
                    //Width = 200,
                    //Height = 400,
                    Dock = System.Windows.Forms.DockStyle.Fill
                };
                var wfh = new WindowsFormsHost()
                {
                    Child = _chart
                };
                spMain.Children.Add(wfh);

                var spControls2 = new StackPanel() { Orientation = Orientation.Horizontal };

                var tipString = $"PerfGraphVSIX https://github.com/calvinhsia/PerfGraphVSIX.git version {this.GetType().Assembly.GetName().Version}    {System.Reflection.Assembly.GetExecutingAssembly().Location}";

                var btnDoSample = new Button()
                {
                    Content = "_DoSample",
                    Height = 20,
                    VerticalAlignment = VerticalAlignment.Top,
                    ToolTip = "Do a Sample, which also does a Tools.ForceGC (Ctrl-Alt-Shift-F12 twice) (automatic on every sample, so click this if your sample time is very long)\r\n" + tipString
                };
                spControls2.Children.Add(btnDoSample);
                btnDoSample.Click += (o, e) =>
                   {
                       ThreadHelper.JoinableTaskFactory.Run(() => DoSampleAsync());
                   };

                var txtLastStatMsg = new TextBox() { Width = 500, Height = 20, VerticalAlignment = VerticalAlignment.Top, ToolTip = "Last Sample", HorizontalAlignment = HorizontalAlignment.Left, FontFamily = _fontFamily };
                txtLastStatMsg.SetBinding(TextBox.TextProperty, nameof(LastStatMsg));
                spControls2.Children.Add(txtLastStatMsg);

                spMain.Children.Add(spControls2);

                var t = Task.Run(() =>
                {
                    ResetPerfCounterMonitor();
                });

                chkShowStatusHistory.RaiseEvent(new RoutedEventArgs(CheckBox.CheckedEvent, this));
            }
            catch (Exception ex)
            {
                this.Content = ex.ToString();
            }
        }


        // use a circular buffer to store samples. 
        // dictionary of sample # (int from 0 to NumDataPoints) =>( List (PerfCtrValues in order)
        public Dictionary<int, List<uint>> _dataPoints = new Dictionary<int, List<uint>>();
        int _bufferIndex = 0;
        List<uint> _lstPCData; // list of samples from each selected counter
        void ResetPerfCounterMonitor()
        {
            _ctsPcounter?.Cancel();
            lock (_lstPerfCounterDefinitions)
            {
                _lstPCData = new List<uint>();
                _dataPoints.Clear();
                _bufferIndex = 0;
            }
            if (UpdateInterval > 0)
            {
                AddStatusMsgAsync($"{nameof(ResetPerfCounterMonitor)}").Forget();
                DoPerfCounterMonitoring();
            }
            else
            {
                AddStatusMsgAsync($"UpdateInterval = 0 auto sampling turned off").Forget();
            }
        }

        void DoPerfCounterMonitoring()
        {
            _ctsPcounter = new CancellationTokenSource();
            _tskDoPerfMonitoring = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    while (!_ctsPcounter.Token.IsCancellationRequested && UpdateInterval > 0)
                    {
                        await DoSampleAsync();
                        await Task.Delay(TimeSpan.FromMilliseconds(UpdateInterval), _ctsPcounter.Token);
                    }
                }
                catch (TaskCanceledException)
                {
                }
                AddStatusMsgAsync($"cancelling {nameof(DoPerfCounterMonitoring)}").Forget();
            });
        }

        async Task DoSampleAsync()
        {
            var sBuilder = new StringBuilder();
            lock (_lstPerfCounterDefinitions)
            {
                int idx = 0;
                foreach (var ctr in _lstPerfCounterDefinitions.Where(pctr => pctr.IsEnabled))
                {
                    var pcValueAsFloat = ctr.ReadNextValue();
                    uint pcValue = 0;
                    uint priorValue = 0;
                    if (idx < _lstPCData.Count)
                    {
                        priorValue = _lstPCData[idx];
                    }
                    else
                    {
                        _lstPCData.Add(0);
                    }
                    if (ctr.perfCounterType.ToString().Contains("Bytes") && !ctr.perfCounterType.ToString().Contains("PerSec") && this.ScaleByteCounters)
                    {
                        pcValue = (uint)(pcValueAsFloat * 100 / uint.MaxValue); // '% of 4G
                        int delta = (int)pcValue - (int)priorValue;
                        sBuilder.Append($"{ctr.PerfCounterName}= {pcValueAsFloat:n0}  {pcValue:n0}%  Δ = {delta:n0} ");
                    }
                    else
                    {
                        pcValue = (uint)pcValueAsFloat;
                        int delta = (int)pcValue - (int)priorValue;
                        sBuilder.Append($"{ctr.PerfCounterName}={pcValue:n0}  Δ = {delta:n0} ");
                    }
                    _lstPCData[idx] = pcValue;
                    idx++;
                }
            }
            await AddDataPointsAsync();
            AddStatusMsgAsync($"{sBuilder.ToString()}").Forget();
        }

        async Task AddDataPointsAsync()
        {
            if (_dataPoints.Count == 0) // nothing yet
            {
                for (int i = 0; i < NumDataPoints; i++)
                {
                    _dataPoints[i] = new List<uint>(_lstPCData); // let all init points be equal, so y axis scales IsStartedFromZero
                }
            }
            else
            {
                _dataPoints[_bufferIndex++] = new List<uint>(_lstPCData);
                if (_bufferIndex == _dataPoints.Count) // wraparound?
                {
                    _bufferIndex = 0;
                }
            }
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            DoGC();// do a GC.Collect on main thread for every sample (the graphing uses memory)
            // this needs to be done on UI thread
            _chart.Series.Clear();
            _chart.ChartAreas.Clear();
            ChartArea chartArea = null;
            chartArea = new ChartArea("ChartArea");
            chartArea.AxisY.LabelStyle.Format = "{0:n0}";
            chartArea.AxisY.LabelStyle.Font = new System.Drawing.Font("Consolas", 12);
            _chart.ChartAreas.Add(chartArea);
            int ndxSeries = 0;
            chartArea.AxisY.IsStartedFromZero = false;
            if (SetMaxGraphTo100)
            {
                _chart.ChartAreas[0].AxisY.Maximum = 100;
            }
            foreach (var entry in _lstPCData)
            {
                var series = new Series
                {
                    ChartType = SeriesChartType.Line
                };
                _chart.Series.Add(series);
                for (int i = 0; i < _dataPoints.Count; i++)
                {
                    var ndx = _bufferIndex + i;
                    if (ndx >= _dataPoints.Count)
                    {
                        ndx -= _dataPoints.Count;
                    }
                    var dp = new DataPoint(i + 1, _dataPoints[ndx][ndxSeries]);
                    series.Points.Add(dp);
                }
                ndxSeries++;
            }
            _chart.DataBind();

            if (_editorTracker != null)
            {
                var (openedViews, lstLeakedViews) = _editorTracker.GetCounts();
                _OpenedViews.Clear();
                _LeakedViews.Clear();
                foreach (var dictEntry in openedViews)
                {
                    var sp = new StackPanel() { Orientation = Orientation.Horizontal };
                    sp.Children.Add(new TextBlock() { Text = $"{ dictEntry.Key,-15} {dictEntry.Value,3}", FontFamily = _fontFamily });
                    _OpenedViews.Add(sp);
                }

                foreach (var entry in lstLeakedViews)
                {
                    var sp = new StackPanel() { Orientation = Orientation.Horizontal };
                    sp.Children.Add(new TextBlock() { Text = $"{ entry._contentType,-15} {entry._serialNo,3} {entry._dtCreated.ToString("hh:mm:ss")} {entry._filename}", FontFamily = _fontFamily });
                    _LeakedViews.Add(sp);
                }
            }
            if (_objTracker != null)
            {
                var (createdObjs, lstLeakedObjs) = _objTracker.GetCounts();
                _CreatedObjs.Clear();
                _LeakedViews.Clear();
                foreach (var dictEntry in createdObjs)
                {
                    var sp = new StackPanel() { Orientation = Orientation.Horizontal };
                    sp.Children.Add(new TextBlock() { Text = $"{ dictEntry.Key,-15} {dictEntry.Value,3}", FontFamily = _fontFamily });
                    _OpenedViews.Add(sp);
                }

                foreach (var entry in lstLeakedObjs)
                {
                    var sp = new StackPanel() { Orientation = Orientation.Horizontal };
                    sp.Children.Add(new TextBlock() { Text = $"{ entry.descriptor,-15} {entry._serialNo,3} {entry._dtCreated.ToString("hh:mm:ss")}", FontFamily = _fontFamily });
                    _LeakedViews.Add(sp);
                }
            }
        }

        void DoGC()
        {
            Dispatcher.VerifyAccess();
            //EnvDTE.DTE dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            PerfGraphToolWindowCommand.Instance.g_dte.ExecuteCommand("Tools.ForceGC");
        }

        const int statusTextLenThresh = 100000;
        int nTruncated = 0;
        FontFamily _fontFamily = new FontFamily("Consolas");

        async public Task AddStatusMsgAsync(string msg, params object[] args)
        {
            // we want to read the threadid 
            //and time immediately on current thread
            var dt = string.Format("[{0}],",
                DateTime.Now.ToString("hh:mm:ss:fff")
                //,Thread.CurrentThread.ManagedThreadId
                );
            var str = string.Format(dt + msg + "\r\n", args);
            this.LastStatMsg = str;
            if (_txtStatus != null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                // this action executes on main thread
                var len = _txtStatus.Text.Length;

                if (len > statusTextLenThresh)
                {
                    _txtStatus.Text = _txtStatus.Text.Substring(statusTextLenThresh - 1000);
                    str += $"   Truncated Status History {++nTruncated} times\r\n";
                }
                _txtStatus.AppendText(str);
                _txtStatus.ScrollToEnd();
            }
        }

    }
    // a textbox that selects all when focused:
    public class MyTextBox : TextBox
    {
        public MyTextBox()
        {
            this.SetResourceReference(TextBox.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            this.SetResourceReference(TextBox.BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
            this.GotFocus += (o, e) =>
            {
                this.SelectAll();
            };
        }
    }

}
