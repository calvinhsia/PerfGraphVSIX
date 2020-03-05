using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace PerfGraphVSIX
{
    /// <summary>
    /// Interaction logic for InProcLeakTracker.xaml
    /// </summary>
    public partial class InProcLeakTracker : UserControl
    {
        private readonly DispatcherTimer _timer;
        private readonly FontFamily _fontFamily;
        private readonly JoinableTaskFactory _jtf;
        private readonly IMemoryLeakTrackerService _tracker;
        private readonly HashSet<LivingObjectRecord> _alreadyShownRecords = new HashSet<LivingObjectRecord>();

        public InProcLeakTracker()
        {
            this.InitializeComponent();

            _fontFamily = new FontFamily("Consolas");
            _tracker = PerfGraphToolWindowPackage.ComponentModel.GetService<IMemoryLeakTrackerService>();
            _jtf = PerfGraphToolWindowPackage.ComponentModel.GetService<JoinableTaskContext>().Factory;

            _timer = new DispatcherTimer(DispatcherPriority.Background, this.Dispatcher);
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0);

            this.IsVisibleChanged += OnIsVisibleChanged;
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool visible)
            {
                if (visible)
                {
                    _timer.Tick += OnTick;
                    _timer.Start();
                }
                else
                {
                    _timer.Stop();
                    _timer.Tick -= OnTick;
                }
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            bool doGC = this.GcCheckbox.IsChecked ?? false;
            _jtf.RunAsync(async delegate
            {
                await TaskScheduler.Default;
                if (doGC)
                {
                    for (int i = 0; (i < 2); i++)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        System.Runtime.InteropServices.Marshal.CleanupUnusedObjectsInCurrentContext();
                    }
                    GC.Collect();
                }

                await _jtf.SwitchToMainThreadAsync();
                _tracker.RefreshWeakRefrenceCache();
                this.UpdateDisplay();
            });
        }

        private void UpdateDisplay()
        {
            this.Open.Children.Clear();
            this.Closed.Children.Clear();

            var livingObjects = _tracker.GetLivingObjects();

            this.UpdateCounters(this.Open, livingObjects.Where(obj => obj.HasProbablyLeaked == false).OrderBy(record => record.Identifier));
            this.UpdateCounters(this.Closed, livingObjects.Where(obj => obj.HasProbablyLeaked == true).OrderBy(record => record.Identifier));
        }

        private void UpdateCounters(StackPanel views, IEnumerable<LivingObjectRecord> trackedObjects)
        {
            foreach (var trackedObject in trackedObjects)
            {
                var t = new TextBlock();
                t.FontFamily = _fontFamily;
                t.Margin = new Thickness(2.0);
                t.Text = "Tracked Object: " + trackedObject.Identifier + " ; Description: " + trackedObject.Description;
                t.ToolTip = trackedObject.Preview;
                views.Children.Add(t);
            }
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            for (int i = 0; (i < 2); i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                System.Runtime.InteropServices.Marshal.CleanupUnusedObjectsInCurrentContext();
            }
            GC.Collect();
            _tracker.RefreshWeakRefrenceCache();
            _tracker.ClearCache();
            this.UpdateDisplay();
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            var categoryDelimter = "-------------------------------------------------------------------------------------";
            var recordDelimtiter = "*************************************************************************************";
            var closedContent = new StringBuilder(Environment.NewLine + Environment.NewLine + categoryDelimter + "Closed Objects: " + categoryDelimter + Environment.NewLine + Environment.NewLine + recordDelimtiter);
            var openContent = new StringBuilder(Environment.NewLine + Environment.NewLine + categoryDelimter + "Open Objects: " + categoryDelimter + Environment.NewLine + Environment.NewLine + recordDelimtiter);

            var openIndex = 1;
            var closedIndex = 1;
            foreach (var trackedObject in _tracker.GetLivingObjects().OrderBy(record => record.Identifier))
            {
                if (trackedObject.HasProbablyLeaked)
                {
                    closedContent.AppendLine("\t " + closedIndex + ". Probably Leaked object (Alive even after object claims to be closed): " + trackedObject.Identifier +
                                                Environment.NewLine + "\t   Descritpion: " + trackedObject.Description +
                                                Environment.NewLine + "\t   Preview: " + trackedObject.Preview +
                                                Environment.NewLine + recordDelimtiter);
                    closedIndex++;
                }
                else
                {
                    openContent.AppendLine("\t " + openIndex + ". Alive object (Object claims to still be open): " + trackedObject.Identifier +
                                                Environment.NewLine + "\t   Descritpion: " + trackedObject.Description +
                                                Environment.NewLine + "\t   Preview: " + trackedObject.Preview +
                                                Environment.NewLine + recordDelimtiter);
                    openIndex++;
                }
            }
            SetClipboardText(closedContent.ToString() + Environment.NewLine + Environment.NewLine + Environment.NewLine + Environment.NewLine + openContent.ToString());
        }

        private static void SetClipboardText(string text)
        {
            try
            {
                DataObject dataObject = new DataObject();
                dataObject.SetText(text);
                Clipboard.SetDataObject(dataObject, false);
            }
            catch (Exception) { }
        }

        private void OnGcChecked(object sender, RoutedEventArgs e)
        {
            this.OnTick(null, EventArgs.Empty);
        }
    }

}

