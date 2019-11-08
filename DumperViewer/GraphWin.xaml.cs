using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PerfGraphVSIX;

namespace DumperViewer
{
    /// <summary>
    /// Interaction logic for GraphWin.xaml
    /// </summary>
    public partial class GraphWin : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void RaisePropChanged([CallerMemberName] string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public ObservableCollection<string> LstCounters { get; set; } = new ObservableCollection<string>();
        public bool ShowTrendLines { get; set; } = true;

        readonly Chart _chart = new Chart();
        List<RegressionAnalysis> lstRegressionAnalysis;
        MeasurementHolder measurementHolder;
        public GraphWin(MeasurementHolder measurementHolder)
        {
            InitializeComponent();
            this.DataContext = this;
            this.measurementHolder = measurementHolder;
            this.Loaded += GraphWin_Loaded;
        }

        private void GraphWin_Loaded(object sender, RoutedEventArgs e)
        {
            this.wfhost.Child = _chart;
        }

        internal void AddGraph(List<RegressionAnalysis> lstRegressionAnalysis)
        {
            this.lstRegressionAnalysis = lstRegressionAnalysis;
            _chart.Series.Clear();
            _chart.ChartAreas.Clear();
            ChartArea chartArea = new ChartArea("ChartArea");
            chartArea.AxisY.LabelStyle.Format = "{0:n0}";
            chartArea.AxisY.LabelStyle.Font = new System.Drawing.Font("Consolas", 12);
            _chart.ChartAreas.Add(chartArea);

            var lstCtrsToInclude = new List<string>();
            if (this.lbCounters.SelectedItems.Count == 0)
            {
                lstCtrsToInclude = null;// include all
            }
            else
            {
                foreach (var item in this.lbCounters.SelectedItems)
                {
                    lstCtrsToInclude.Add(item.ToString());
                }
            }
            foreach (var item in lstRegressionAnalysis)
            {
                if (lstCtrsToInclude != null)
                {
                    if (!lstCtrsToInclude.Contains(item.perfCounterData.PerfCounterName))
                    {
                        continue;
                    }
                }
                if (!LstCounters.Contains(item.perfCounterData.PerfCounterName))
                {
                    LstCounters.Add(item.perfCounterData.PerfCounterName);
                }
                var series = new Series
                {
                    ChartType = SeriesChartType.Line,
                    Name = item.perfCounterData.PerfCounterName,
                    ToolTip = item.perfCounterData.PerfCounterName
                };
                _chart.Series.Add(series);
                for (int i = 0; i < item.lstData.Count; i++)
                {
                    var dp = new DataPoint(i, item.lstData[i].Y);
                    series.Points.Add(dp);
                }
                if (ShowTrendLines)
                {
                    var seriesTrendLine = new Series()
                    {
                        ChartType = SeriesChartType.Line,
                        Name = item.perfCounterData.PerfCounterName + "Trend",
                        ToolTip = item.perfCounterData.PerfCounterName + $"Trend N={item.lstData.Count} RmsErr={item.rmsError}  m={item.m:n1} b= {item.b:n1} IsRegression={item.IsRegression}"
                    };
                    _chart.Series.Add(seriesTrendLine);
                    var dp0 = new DataPoint(0, item.b);
                    seriesTrendLine.Points.Add(dp0);
                    var dp1 = new DataPoint(item.lstData.Count, item.lstData.Count * item.m + item.b);
                    seriesTrendLine.Points.Add(dp1);
                }
            }
            _chart.DataBind();
        }

        private void LbCounters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.lbCounters.SelectedItems.Count > 0)
            {
                var lstCtrsToInclude = new List<string>();
                foreach (var item in this.lbCounters.SelectedItems)
                {
                    lstCtrsToInclude.Add(item.ToString());
                }
                AddGraph(this.lstRegressionAnalysis);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AddGraph(this.lstRegressionAnalysis);
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AddGraph(this.lstRegressionAnalysis);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.measurementHolder.DumpOutMeasurementsToTempFile(StartExcel: true);
        }
    }
}
