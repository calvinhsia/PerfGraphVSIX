using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;


namespace PerfGraphVSIX
{
    public class PerfGraph : UserControl, INotifyPropertyChanged
    {
        public TextBox _txtStatus;
        readonly Chart _chart;

        /// <summary>
        /// PerfCounters updated periodically. Safe to change without stopping the monitoring
        /// </summary>
        public int UpdateInterval { get; set; } = 1000;
        public int NumDataPoints { get; set; } = 100;

        public bool ScaleByteCounters { get; set; } = true;
        public bool SetMaxGraphTo100 { get; set; } = true;

        string _LastStatMsg;
        public string LastStatMsg { get { return _LastStatMsg; } set { _LastStatMsg = value; RaisePropChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        void RaisePropChanged([CallerMemberName] string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        [Flags] // user can select multiple items. (beware scaling: pct => 0-100, Bytes => 0-4G)
        public enum PerfCounterType
        {
            None,
            ProcessorPctTime = 0x1,
            ProcessorPrivateBytes = 0x2,
            ProcessorVirtualBytes = 0x4,
            ProcessorWorkingSet = 0x8,
            GCPctTime = 0x100,
            GCBytesInAllHeaps = 0x200,
            GCAllocatedBytesPerSec = 0x400,
        }
        public class PerfCounterData
        {
            public PerfCounterType perfCounterType;
            public string PerfCounterCategory;
            public string PerfCounterName;
            public string PerfCounterInstanceName;
            public bool IsEnabled = false;
            public Lazy<PerformanceCounter> lazyPerformanceCounter;

            public float LastValue;
            public float ReadNextValue()
            {
                var retVal = lazyPerformanceCounter.Value.NextValue();
                LastValue = retVal;
                return retVal;
            }
            public PerfCounterData(PerfCounterType perfCounterType, string perfCounterCategory, string perfCounterName, string perfCounterInstanceName)
            {
                this.perfCounterType = perfCounterType;
                this.PerfCounterCategory = perfCounterCategory;
                this.PerfCounterName = perfCounterName;
                this.PerfCounterInstanceName = perfCounterInstanceName;
                this.lazyPerformanceCounter = new Lazy<PerformanceCounter>(() =>
                {
                    PerformanceCounter pc = null;
                    var vsPid = Process.GetCurrentProcess().Id;
                    var category = new PerformanceCounterCategory(PerfCounterCategory);

                    foreach (var instanceName in category.GetInstanceNames()) // exception if you're not admin or "Performance Monitor Users" group (must re-login)
                    {
                        using (var cntr = new PerformanceCounter(category.CategoryName, PerfCounterInstanceName, instanceName, readOnly: true))
                        {
                            try
                            {
                                var val = (int)cntr.NextValue();
                                if (val == vsPid)
                                {
                                    pc = new PerformanceCounter(PerfCounterCategory, PerfCounterName, instanceName);
                                    break;
                                }
                            }
                            catch (Exception)
                            {
                                // System.InvalidOperationException: Instance 'IntelliTrace' does not exist in the specified Category.
                            }
                        }
                    }
                    return pc;
                });
            }
            public override string ToString()
            {
                return $"{perfCounterType} {PerfCounterCategory} {PerfCounterName} {PerfCounterInstanceName} Enabled = {IsEnabled}";
            }
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
        };



        Task _tskDoPerfMonitoring;
        CancellationTokenSource _ctsPcounter;
        public static PerfGraph Instance;
        public PerfGraph()
        {
            try
            {
                Instance = this;
                this.DataContext = this;
                //this.Height = 600;
                //this.Width = 1000;
                var sp = new StackPanel() { Orientation = Orientation.Vertical };
                var expander = new Expander()
                {
                    IsExpanded = false,
                    Header = "Expand for options"
                };
                var spControls = new StackPanel() { Orientation = Orientation.Horizontal };
                expander.Content = spControls;
                sp.Children.Add(expander);

                spControls.Children.Add(new Label() { Content = "Update Interval", ToolTip = "Update graph in MilliSeconds" });
                var txtUpdateInterval = new TextBox() { Width = 50, Height = 20, VerticalAlignment = VerticalAlignment.Top };
                txtUpdateInterval.SetBinding(TextBox.TextProperty, nameof(UpdateInterval));
                spControls.Children.Add(txtUpdateInterval);

                spControls.Children.Add(new Label()
                {
                    Content = "NumDataPoints",
                    ToolTip = "Number of Data points (x axis). Will change on next Reset"
                });
                var txtNumDataPoints = new TextBox() { Width = 50, Height = 20, VerticalAlignment = VerticalAlignment.Top };
                txtNumDataPoints.SetBinding(TextBox.TextProperty, nameof(NumDataPoints));
                spControls.Children.Add(txtNumDataPoints);

                var chkSetMaxGraphTo100 = new CheckBox()
                {
                    Content = "SetMaxGraphTo100",
                    ToolTip = "Set Max Y axis to 100. Else will dynamically rescale Y axis",
                    Height = 20,
                    VerticalAlignment = VerticalAlignment.Top,
                };
                chkSetMaxGraphTo100.SetBinding(CheckBox.IsCheckedProperty, nameof(SetMaxGraphTo100));
                spControls.Children.Add(chkSetMaxGraphTo100);

                var chkScaleByteCtrs = new CheckBox()
                {
                    Content = "Scale 'Byte' counters (but not BytePerSec)",
                    ToolTip = "for Byte counters, eg VirtualBytes, scale to be a percent of 4Gigs",
                    Height = 20,
                    VerticalAlignment = VerticalAlignment.Top
                };
                chkScaleByteCtrs.SetBinding(CheckBox.IsCheckedProperty, nameof(ScaleByteCounters));
                spControls.Children.Add(chkScaleByteCtrs);

                var chkShowStatusHistory = new CheckBox()
                {
                    Content = "Show Status History",
                    ToolTip = "Show a textbox which accumulates history of samples",
                    Height = 20,
                    VerticalAlignment = VerticalAlignment.Top
                };
                chkShowStatusHistory.Checked += (o, e) =>
                {
                    if (_txtStatus != null)
                    {
                        sp.Children.Remove(_txtStatus); // remove prior one
                    }
                    _txtStatus = new TextBox()
                    {
                        IsReadOnly = true,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        IsUndoEnabled = false,
                        FontFamily = new FontFamily("Courier New"),
                        FontSize = 10,
                        Height = 200,
                        MaxHeight = 200,
                        HorizontalContentAlignment = HorizontalAlignment.Left
                    };
                    sp.Children.Add(_txtStatus);
                };
                chkShowStatusHistory.Unchecked += (o, e) =>
                {
                    sp.Children.Remove(_txtStatus);
                    _txtStatus.Text = string.Empty; // keep around so don't have thread contention
                };
                spControls.Children.Add(chkShowStatusHistory);

                var lbPCounters = new ListBox()
                {
                    Width = 140,
                    //                    Height = 90,
                    SelectionMode = SelectionMode.Multiple,
                    Margin = new Thickness(10, 0, 0, 0)
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
                    }
                    catch (Exception)
                    {
                    }
                };
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates
                spControls.Children.Add(lbPCounters);


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
                sp.Children.Add(wfh);
                var txtLastStatMsg = new TextBox() { Width = 500, Height = 20, VerticalAlignment = VerticalAlignment.Top, ToolTip = "Last Sample", HorizontalAlignment = HorizontalAlignment.Left };
                txtLastStatMsg.SetBinding(TextBox.TextProperty, nameof(LastStatMsg));
                sp.Children.Add(txtLastStatMsg);

                this.Content = sp;

                var t = Task.Run(() =>
                {
                    ResetPerfCounterMonitor();
                });

                //        btnGo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, this));
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
        void ResetPerfCounterMonitor()
        {
            var pid = Process.GetCurrentProcess().Id;
            AddStatusMsgAsync($"{nameof(ResetPerfCounterMonitor)}").Forget();
            lock (_lstPerfCounterDefinitions)
            {
                _dataPoints.Clear();
                _bufferIndex = 0;

                for (int i = 0; i < NumDataPoints; i++)
                {
                    var emptyList = new List<uint>();
                    _lstPerfCounterDefinitions.Select(s => s.perfCounterType).ToList().ForEach((s) => emptyList.Add(0));
                    _dataPoints[i] = emptyList;
                }
            }
            DoPerfCounterMonitoring();
        }
        async Task AddDataPointsAsync(List<uint> lstNewestSample)
        {
            _dataPoints[_bufferIndex++] = lstNewestSample;
            if (_bufferIndex == _dataPoints.Count)
            {
                _bufferIndex = 0;
            }
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            // this needs to be done on UI thread
            _chart.Series.Clear();
            _chart.ChartAreas.Clear();
            ChartArea chartArea = null;
            if (_chart.ChartAreas.Count == 0)
            {
                chartArea = new ChartArea("ChartArea");
                _chart.ChartAreas.Add(chartArea);
            }
            int ndxSeries = 0;
            foreach (var entry in lstNewestSample)
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
                if (SetMaxGraphTo100)
                {
                    _chart.ChartAreas[0].AxisY.Maximum = 100;
                }
            }
            _chart.DataBind();
        }


        void DoPerfCounterMonitoring()
        {
            _ctsPcounter = new CancellationTokenSource();
            _tskDoPerfMonitoring = Task.Run(async () =>
            {
                try
                {
                    while (!_ctsPcounter.Token.IsCancellationRequested)
                    {
                        var sBuilder = new StringBuilder();
                        var lstPCData = new List<uint>();
                        lock (_lstPerfCounterDefinitions)
                        {
                            foreach (var ctr in _lstPerfCounterDefinitions.Where(pctr => pctr.IsEnabled))
                            {
                                var pcValueAsFloat = ctr.ReadNextValue();
                                uint pcValue = 0;
                                if (ctr.perfCounterType.ToString().Contains("Bytes") && !ctr.perfCounterType.ToString().Contains("PerSec") && this.ScaleByteCounters)
                                {
                                    pcValue = (uint)(pcValueAsFloat * 100 / uint.MaxValue); // '% of 4G
                                    sBuilder.Append($"{ctr.PerfCounterName}= {pcValueAsFloat:n0}  {pcValue:n0}%  ");
                                }
                                else
                                {
                                    pcValue = (uint)pcValueAsFloat;
                                    sBuilder.Append($"{ctr.PerfCounterName}={pcValue:n0}  ");
                                }

                                lstPCData.Add(pcValue);
                            }
                            AddDataPointsAsync(lstPCData).Forget();
                        }
                        AddStatusMsgAsync($"Sampling {sBuilder.ToString()}").Forget();
                        await Task.Delay(UpdateInterval, _ctsPcounter.Token);
                    }
                }
                catch (TaskCanceledException)
                {
                }
                AddStatusMsgAsync($"cancelling {nameof(DoPerfCounterMonitoring)}").Forget();
            });
        }

        async public Task AddStatusMsgAsync(string msg, params object[] args)
        {
            // we want to read the threadid 
            //and time immediately on current thread
            var dt = string.Format("[{0}],{1,2},",
                DateTime.Now.ToString("hh:mm:ss:fff"),
                Thread.CurrentThread.ManagedThreadId);
            var str = string.Format(dt + msg + "\r\n", args);
            this.LastStatMsg = str;
            if (_txtStatus != null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                // this action executes on main thread
                // note: this is a memory leak: can clear periodically
                _txtStatus.AppendText(str);
                _txtStatus.ScrollToEnd();
            }
        }
    }
}
