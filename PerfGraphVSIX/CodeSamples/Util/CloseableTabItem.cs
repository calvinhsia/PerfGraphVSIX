using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MyCodeToExecute
{
    public class CloseableTabItem : TabItem
    {
        private readonly string tabName;
        private readonly Action<CloseableTabItem, string> OnActivatedFirstTimeCreateContent;
        bool fDidCreateContent = false;
        public event EventHandler TabItemClosed;

        public CloseableTabItem(string TabName, string tabTip, Action<CloseableTabItem, string> onActivatedFirstTimeCreateContent = null)
        {
            this.tabName = TabName;
            this.OnActivatedFirstTimeCreateContent = onActivatedFirstTimeCreateContent;
            var tbHeaderPanel = new StackPanel() { Orientation = Orientation.Horizontal };
            this.Header = tbHeaderPanel;
            tbHeaderPanel.Children.Add(new TextBlock() { Text = TabName, ToolTip = tabTip }); // use textblock and not label so "_" isn't hot key
            var tbHeaderCloseButton = new MyCloseButton(this);
            //var closeButtonStyle = new Style()
            //{
            //    TargetType = typeof(Button)
            //};
            //var trigger = new Trigger()
            //{
            //    Property = IsMouseOverProperty,
            //    Value = true
            //};
            //trigger.Setters.Add(new Setter()
            //{
            //    Property = BackgroundProperty,
            //    Value = Brushes.Pink
            //});
            //closeButtonStyle.Triggers.Add(trigger);
            //tbHeaderCloseButton.Style = closeButtonStyle;
            tbHeaderPanel.Children.Add(tbHeaderCloseButton);
        }
        protected override void OnSelected(RoutedEventArgs e)
        {
            base.OnSelected(e);
            if (!fDidCreateContent)
            {
                fDidCreateContent = true;
                this.OnActivatedFirstTimeCreateContent?.Invoke(this, tabName);
            }
        }
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            var isCtrlKeyDown = e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control);
            if (isCtrlKeyDown)
            {
                if (e.Key == Key.F4)
                {
                    this.CloseTabItem();
                    e.Handled = true;
                }
            }
        }

        private void CloseTabItem()
        {
            DependencyObject obj = this;
            while (true)
            {
                obj = VisualTreeHelper.GetParent(obj);
                if (obj is TabControl tabControl)
                {
                    TabItemClosed?.Invoke(obj, new EventArgs());
                    tabControl.Items.Remove(this);
                    break;
                }
            }
        }

        class MyCloseButton : Button
        {
            readonly TextBlock tbClose;
            private readonly CloseableTabItem _closeableTabItem;

            public MyCloseButton(CloseableTabItem closeableTabItem)
            {
                this._closeableTabItem = closeableTabItem;
                Background = Brushes.Transparent;
                Height = 10;
                VerticalAlignment = VerticalAlignment.Top;
                tbClose = new TextBlock()
                {
                    Text = "r",
                    IsEnabled = false,
                    FontFamily = new FontFamily("Marlett"),
                    FontSize = 8,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = Brushes.Transparent,
                };
                this.Content = tbClose;
            }
            protected override void OnMouseEnter(MouseEventArgs e)
            {
                base.OnMouseEnter(e);
                this.tbClose.Background = Brushes.Pink;
            }
            protected override void OnMouseLeave(MouseEventArgs e)
            {
                base.OnMouseLeave(e);
                this.tbClose.Background = Brushes.Transparent;
            }
            protected override void OnClick()
            {
                base.OnClick();
                this._closeableTabItem.CloseTabItem();
            }
        }
    }
}