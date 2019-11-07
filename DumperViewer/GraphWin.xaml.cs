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

        Chart _chart = new Chart();
        public GraphWin()
        {
            InitializeComponent();
            this.DataContext = this;
            this.Loaded += GraphWin_Loaded;
            _chart.Series.Clear();
            _chart.ChartAreas.Clear();
            ChartArea chartArea = new ChartArea("ChartArea");
            chartArea.AxisY.LabelStyle.Format = "{0:n0}";
            chartArea.AxisY.LabelStyle.Font = new System.Drawing.Font("Consolas", 12);
            _chart.ChartAreas.Add(chartArea);
        }

        private void GraphWin_Loaded(object sender, RoutedEventArgs e)
        {
            this.wfhost.Child = _chart;
        }

        internal void AddGraph(PerfCounterData ctr, RegressionAnalysis r)
        {
            LstCounters.Add(ctr.PerfCounterName);
            var series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            _chart.Series.Add(series);
            for (int i = 0; i < r.lstData.Count; i++)
            {
                var dp = new DataPoint(i, r.lstData[i].Y);
                series.Points.Add(dp);
            }
            _chart.DataBind();
        }

        private void LbCounters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
