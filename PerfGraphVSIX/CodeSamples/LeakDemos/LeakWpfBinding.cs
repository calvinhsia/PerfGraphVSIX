//Desc: Demonstrate a leak with WPF RoutedEventHandlers
// This code will be compiled and run when you hit the ExecCode button. Any error msgs will be shown in the status log control.
// This allows you to create a stress test by repeating some code, while taking measurements between each iteration.

//  Macro substitution: %PerfGraphVSIX% will be changed to the fullpath to PerfGraphVSIX
//                      %VSRoot% will be changed to the fullpath to VS: e.g. "C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview"

//Include: ..\Util\LeakBaseClass.cs

//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.ComponentModelHost.dll



//Ref: %PerfGraphVSIX%
//Pragma: GenerateInMemory = False
//Pragma: UseCSC = true
//Pragma: showwarnings = true
//Pragma: verbose = false

////Ref: c:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll


//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.ComponentModel.Composition.dll


using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.Test.Stress;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.ComponentModelHost;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Reflection;
using System.Xml;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using System.IO;

namespace MyCodeToExecute
{
    public class MyWpfBindingLeakClass : LeakBaseClass
    {
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyWpfBindingLeakClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 37, Sensitivity: 2.5, delayBetweenIterationsMsec: 0);
            }
        }
        public MyWpfBindingLeakClass(object[] args) : base(args)
        {
            //ShowUI = false;
            //NumIterationsBeforeTotalToTakeBaselineSnapshot = 0;
            SecsBetweenIterations = 0.1;
        }
        public override async Task DoInitializeAsync()
        {
            await Task.Yield();
        }
        List<MyPersonObject> _lstPersonObjects = new();
        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            await Task.Yield();
            var curperson = new MyPersonObject() { Name = $"Name {iteration}" };
            _lstPersonObjects.Add(curperson);
            // to test if your code leaks, put it here. Repeat a lot to magnify the effect
            var MyWindow = new MyLeakyWindow(this, curperson);

            MyWindow.Show();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), _CancellationTokenExecuteCode);
            }
            catch (Exception)
            {
            }
            MyWindow.Close();

        }
        public override async Task DoCleanupAsync()
        {
            await Task.Yield();
            _lstPersonObjects.Clear();
        }
        /// <summary>
        /// Get list of event handlers for Wpf RoutedEvents
        /// e.g.  var eventHandlerList = GetRoutedEventHandlerList<CheckBox>(_pdfViewerWindow.chkInk0, CheckBox.CheckedEvent);
        ///      var cntEvHandlers = eventHandlerList.Length;
        ///     foreach (var evHandler in eventHandlerList)
        ///     {
        ///         var targ = evHandler.Target;
        ///         var meth = evHandler.Method;
        ///     }
        /// </summary>
        /// <typeparamref name="TEventPublisher">The type of the event publisher: e.g. Button </typeparamref>
        /// <returns>Array of delegates or null</returns>
        internal static Delegate[] GetRoutedEventHandlerList<TEventPublisher>(TEventPublisher instance, RoutedEvent routedEvent)
        {
            var lstDelegates = new List<Delegate>();
            try
            {
                var evHandlersStore = typeof(TEventPublisher)
                    .GetProperty("EventHandlersStore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(instance, index: null);
                var miGetEvHandlers = evHandlersStore.GetType().GetMethod("GetRoutedEventHandlers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                var lstRoutedEvents = miGetEvHandlers.Invoke(evHandlersStore, new object[] { routedEvent }) as RoutedEventHandlerInfo[];
                foreach (var handler in lstRoutedEvents)
                {
                    lstDelegates.Add(handler.Handler);
                }
            }
            catch (Exception)
            {
            }
            return lstDelegates.ToArray();
        }
        public class MyPersonObject
        {
            public string Name { get; set; }
        }

        public class MyLeakyWindow : Window
        {
            public MyWpfBindingLeakClass _MyClass;
            byte[] arr = new byte[1024 * 1024 * 10];
            //            public string txtBoxText { get; set; } = "some text";
            public MyLeakyWindow(MyWpfBindingLeakClass MyClass, MyPersonObject curPerson)
            {
                this._MyClass = MyClass;
                var strxaml =
    $@"<Grid
xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
xmlns:l=""clr-namespace:{this.GetType().Namespace};assembly={
        System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location)}"" 
        Margin=""5,5,5,5"">
        <Grid.RowDefinitions>
            <RowDefinition Height=""auto""/>
            <RowDefinition Height=""*""/>
        </Grid.RowDefinitions>
        <StackPanel x:Name=""_sp"" Grid.Row=""0"" HorizontalAlignment=""Left"" Height=""30"" VerticalAlignment=""Top"" Orientation=""Horizontal"">
            <TextBox Text = ""{{Binding Name, Mode = TwoWay}}"" Width = ""200""/>
            <TextBox Text = ""{{Binding ElementName=_sp, Path=Children.Count, Mode = OneWay}}"" Width = ""200""/>
            <Button x:Name = ""btnGo"" Content = ""_Go""/>
        </StackPanel>
       
    </Grid>
";
                //             <TextBox Text = ""{{Binding {txtBoxText}, Mode = TwoWay}}"" Width = ""200""/>

                Width = 900;
                Height = 600;
                var strReader = new System.IO.StringReader(strxaml);
                var xamlreader = XmlReader.Create(strReader);

                var grid = (Grid)(XamlReader.Load(xamlreader));
                grid.DataContext = curPerson;
                this.Content = grid;
                var sp = (StackPanel)grid.FindName("_sp");
                var btnGo = (Button)grid.FindName("btnGo");
                btnGo.Click += (o, e) =>
                {
                    try
                    {
                        this.Title = curPerson.Name;
                    }
                    catch (Exception ex)
                    {
                        this.Content = ex.ToString();
                    }
                };

            }
        }

    }
}

/*
This code leaks:

    public partial class MainWindow : Window
    {
        readonly List<MyPersonObject> _myPersonObjects = new();
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                for (int i = 0; i < 53; i++)
                {
                    var curPerson = new MyPersonObject() { Name = $"name{i}" };
                    _myPersonObjects.Add(curPerson);
                    var mywin = new MyLeakyWindow(curPerson);
                    mywin.Show();
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    mywin.Close();
                }
            }
            catch (Exception)
            {

            }

        }
    }
    public class MyPersonObject
    {
        public string Name { get; set; }
        public MyPersonObject()
        {
            Name = String.Empty;
        }
    }
    public class MyLeakyWindow : Window
    {
        byte[] arr = new byte[1024 * 1024 * 10];

        public MyLeakyWindow(MyPersonObject curPerson)
        {
            var strxaml =
$@"<Grid
xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
xmlns:l=""clr-namespace:{this.GetType().Namespace};assembly={
    System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location)}"" 
        Margin=""5,5,5,5"">
        <Grid.RowDefinitions>
            <RowDefinition Height=""auto""/>
            <RowDefinition Height=""*""/>
        </Grid.RowDefinitions>
        <StackPanel x:Name=""_sp"" Grid.Row=""0"" HorizontalAlignment=""Left"" Height=""30"" VerticalAlignment=""Top"" Orientation=""Horizontal"">
            <TextBox Text = ""{{Binding ElementName=_sp, Path=Children.Count, Mode = OneWay}}"" Width = ""200""/>
            <Button x:Name = ""btnGo"" Content = ""_Go""/>
        </StackPanel>
       
    </Grid>
";
            //not quite: weakref
            //             <TextBox Text = ""{{Binding Name, Mode = TwoWay}}"" Width = ""200""/>

            //this leaks too:
            //            <TextBox Text = ""{{Binding ElementName=_sp, Path=Children.Count, Mode = OneWay}}"" Width = ""200""/>
            this.DataContext = curPerson;
            Width = 900;
            Height = 600;
            var strReader = new System.IO.StringReader(strxaml);
            var xamlreader = XmlReader.Create(strReader);

            var grid = (Grid)(XamlReader.Load(xamlreader));
            this.Content = grid;
            var sp = (StackPanel)grid.FindName("_sp");
            var btnGo = (Button)grid.FindName("btnGo");
            btnGo.Click += (o, e) =>
            {
                try
                {
                }
                catch (Exception ex)
                {
                    this.Content = ex.ToString();
                }
            };

        }

    }



 Children of '-- System.Windows.Documents.TextContainerChangedEventHandler 000001ba`bd1a5ec0 System.Windows.Documents.TextEditor System.Windows.Documents.TextEditor.OnTextContainerChanged System.Windows.Documents.TextEditor System.Windows.Documents.TextEditor.OnTextContainerChanged'
-- System.Windows.Documents.TextContainerChangedEventHandler 000001ba`bd1a5ec0 System.Windows.Documents.TextEditor System.Windows.Documents.TextEditor.OnTextContainerChanged System.Windows.Documents.TextEditor System.Windows.Documents.TextEditor.OnTextContainerChanged
 -> _target = System.Windows.Documents.TextEditor 000001ba`bd1a5b78
  -> _cursor = System.Windows.Input.Cursor 000001ba`bcedcde8
  -> _dispatcher = System.Windows.Threading.Dispatcher 000001ba`bce43fb0
  -> _dragDropProcess = System.Windows.Documents.TextEditorDragDrop+_DragDropProcess 000001ba`bd1a5e70
  -> _selection = System.Windows.Documents.TextSelection 000001ba`bd1a5d78
   -> _textEditor = System.Windows.Documents.TextEditor 000001ba`bd1a5b78
   -> _textSegments = System.Collections.Generic.List<System.Windows.Documents.TextSegment> 000001ba`bd1a6be0
   -> Changed = System.EventHandler 000001ba`bd1b2180
    -> _invocationList = System.Object[] 000001ba`bd1b2158
     -> System.EventHandler 000001ba`bd1a5f58 System.Windows.Controls.TextBox System.Windows.Controls.Primitives.TextBoxBase.OnSelectionChangedInternal System.Windows.Controls.TextBox System.Windows.Controls.Primitives.TextBoxBase.OnSelectionChangedInternal
      -> _target = System.Windows.Controls.TextBox 000001ba`bd1a5930
       -> System.Windows.Diagnostics.XamlSourceInfo 000001ba`bd1a5f98
       -> _dispatcher = System.Windows.Threading.Dispatcher 000001ba`bce43fb0
       -> _dType = System.Windows.DependencyObjectType 000001ba`bceb9fe0
       -> _effectiveValues = System.Windows.EffectiveValueEntry[] 000001ba`bd1b21c0
        -> System.Windows.Markup.XmlnsDictionary 000001ba`bd1a4aa8
        -> System.Collections.Hashtable 000001ba`bd1a7230
        -> WpfApp2.MyLeakyWindow 000001ba`bd1a4668
        -> WpfApp2.MyPersonObject 000001ba`bd1a4628
        -> MS.Utility.FrugalMap 000001ba`bd1ac1f8
        -> System.Double 000001ba`bd1a6c28 200 200
        -> System.Collections.Specialized.HybridDictionary[] 000001ba`bd1a6c40
        -> System.Collections.Generic.List<System.Windows.DependencyObject> 000001ba`bd1aafc8
        -> System.Boolean 000001ba`bcf1c3b0 False False
        -> System.Boolean 000001ba`bcf1b408 True True
        -> System.Windows.EventHandlersStore 000001ba`bd1a5f40
         -> _entries._mapStore = MS.Utility.ThreeObjectMap 000001ba`bd1a64d0
          -> _entry0.Value = System.Windows.DependencyPropertyChangedEventHandler 000001ba`bd1a5f00
          -> _entry1.Value = MS.Utility.FrugalObjectList<System.Windows.RoutedEventHandlerInfo> 000001ba`bd1a6490
           -> _listStore = MS.Utility.SingleItemList<System.Windows.RoutedEventHandlerInfo> 000001ba`bd1a64a8
            -> _loneEntry._handler = System.Windows.RoutedEventHandler 000001ba`bd1a6450 System.Windows.LostFocusEventManager System.Windows.LostFocusEventManager.OnLostFocus System.Windows.LostFocusEventManager System.Windows.LostFocusEventManager.OnLostFocus
             -> _target = System.Windows.LostFocusEventManager 000001ba`bcee0508
              -> _dispatcher = System.Windows.Threading.Dispatcher 000001ba`bce43fb0
              -> _table = MS.Internal.WeakEventTable 000001ba`bcedff40
               -> _cleanupHelper = MS.Internal.CleanupHelper 000001ba`bcee0360
               -> _dataTable = System.Collections.Hashtable 000001ba`bcedffd8
                -> _buckets = System.Collections.Hashtable+bucket[] 000001ba`bd28aae0
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1f56c0
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd1f5680 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd1f56e8 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                  -> list = System.Collections.Specialized.ListDictionary 000001ba`bd1f5908
                   -> head = System.Collections.Specialized.ListDictionary+DictionaryNode 000001ba`bd1f5930
                    -> key = System.ComponentModel.ReflectPropertyDescriptor 000001ba`bcf2c298
                    -> value = MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord 000001ba`bd1f56e8
                     -> _eventArgs = MS.Internal.Data.ValueChangedEventArgs 000001ba`bd1f58b0
                     -> _listeners = System.Windows.WeakEventManager+ListenerList<MS.Internal.Data.ValueChangedEventArgs> 000001ba`bd1f5728
                     -> _manager = MS.Internal.Data.ValueChangedEventManager 000001ba`bcf2d378
                     -> _pd = System.ComponentModel.ReflectPropertyDescriptor 000001ba`bcf2c298
                     -> _source = System.Windows.Controls.UIElementCollection 000001ba`bd1f4780
                      -> _visualChildren = System.Windows.Media.VisualCollection 000001ba`bd1f47a8
                       -> _items = System.Windows.Media.Visual[] 000001ba`bd1f60d8
                        -> System.Windows.Controls.TextBox 000001ba`bd1f4828
                        -> System.Windows.Controls.Button 000001ba`bd1f5f60
                       -> _owner = System.Windows.Controls.StackPanel 000001ba`bd1f44b0
                      -> _visualParent = System.Windows.Controls.StackPanel 000001ba`bd1f44b0
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd24c508
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd24c4c8 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd24c530 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                  -> list = System.Collections.Specialized.ListDictionary 000001ba`bd24c750
                   -> head = System.Collections.Specialized.ListDictionary+DictionaryNode 000001ba`bd24c778
                    -> key = System.ComponentModel.ReflectPropertyDescriptor 000001ba`bcf2c298
                    -> value = MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord 000001ba`bd24c530
                     -> _eventArgs = MS.Internal.Data.ValueChangedEventArgs 000001ba`bd24c6f8
                     -> _listeners = System.Windows.WeakEventManager+ListenerList<MS.Internal.Data.ValueChangedEventArgs> 000001ba`bd24c570
                     -> _manager = MS.Internal.Data.ValueChangedEventManager 000001ba`bcf2d378
                     -> _pd = System.ComponentModel.ReflectPropertyDescriptor 000001ba`bcf2c298
                     -> _source = System.Windows.Controls.UIElementCollection 000001ba`bd24b5c8
                      -> _visualChildren = System.Windows.Media.VisualCollection 000001ba`bd24b5f0
                       -> _items = System.Windows.Media.Visual[] 000001ba`bd24cf20
                       -> _owner = System.Windows.Controls.StackPanel 000001ba`bd24b310
                      -> _visualParent = System.Windows.Controls.StackPanel 000001ba`bd24b310
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd0cb778
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd0cb738 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd0cb7a0 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1a67c8
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd1a6788 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd1a67f0 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1737b8
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd173618
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd139a58
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd1398b8
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd11f298
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd11f258 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd11f2c0 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd0e5720
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd0e5580
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd28aab8
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd28a918
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd139df8
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd139db8 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd139e20 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd30c270
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd30c230 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd30c298 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd0c9930
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd0c9748
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd218330
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd2182f0 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd218358 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd28c0e8
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd28c0a8 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd28c110 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1c7260
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd1c7220 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd1c7288 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd29f210
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd29f070
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd32a2c0
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd32a280 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd32a2e8 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd148cf0
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd148b50
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd23d7e0
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd23d640
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd0fe040
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd0fdea0
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1a6428
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd1a6288
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd200c58
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd200ab8
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd0e5b58
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd0e5b18 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd0e5b80 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd24c168
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd24bfc8
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd37f8f8
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd37f8b8 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd37f920 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd14fa60
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd14fa20 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd14fa88 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1f5320
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd1f5180
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd4165a8
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd416568 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd4165d0 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd39d348
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd39d308 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd39d370 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bcf2d4d8
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bcf2d498 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bcf2d500 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1d2b78
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd1d2b38 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd1d2ba0 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd11ee38
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd11ec98
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd3369e8
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd3369a8 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd336a10 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd25e8e0
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd25e8a0 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd25e908 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bcf2a548
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bcf2a3a8
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd329af0
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd329950
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd37f128
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd37ef88
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd29f5b0
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd29f570 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd29f5d8 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd0c1ec8
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd0c1d28
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2d08a8
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd2d0708
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd60e578
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd60e3d8
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bcee07e8
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bcee0648
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd0aa758
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd0aa5b8
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd166460
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd1662c0
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd149258
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd149218 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd149280 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd3c9740
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd3c95a0
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2dc560
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd2dc520 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd2dc588 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd0c2298
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd0c2258 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd0c22c0 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd120470
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd1202d0
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd20c928
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd20c8e8 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd20c950 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd230ec8
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd230d28
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1ba110
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd1b9f70
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd6a3748
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd6a3708 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd6a3770 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd223d98
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd223d58 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd223dc0 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd173b58
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd173b18 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd173b80 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2ab4a8
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd2ab308
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2f39d0
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd2f3990 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd2f39f8 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd0b9340
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd0b9300 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd0b9368 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2239f8
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd223858
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd404258
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd4040b8
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd336218
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd336060
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd093ea8
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd093d08
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd231268
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd231228 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd231290 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2c26d8
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd2c2538
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd0fe3e0
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd0fe3a0 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd0fe408 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2ff0b0
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd2fef10
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd6a2f78
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd6a2dd8
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd23dd28
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd23dce8 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd23dd50 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd166800
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd1667c0 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd166828 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2ff450
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd2ff410 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd2ff478 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd60ed48
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd60ed08 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd60ed70 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1c6ec0
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd1c6d20
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd45ed28
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd45ece8 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd45ed50 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2e7bc8
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd2e7a28
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd657e68
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd657cc8
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1e9da8
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd1e9d68 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd1e9dd0 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bcf28e48
                 -> MS.Internal.Data.StaticPropertyChangedEventManager+TypeRecord 000001ba`bcf28db0
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2ab848
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd2ab808 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd2ab870 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd39c458
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd39bd58
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd0b8f60
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd0b8dc0
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd404a28
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd4049e8 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd404a50 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd0f92b8
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd0f9118
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd0aad00
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd0aacc0 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd0aad28 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd20c588
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd20c3e8
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd25e540
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd25e3a0
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2dc1c0
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd2dc020
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd6edf80
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd6edde0
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd45e558
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd45e3b8
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1d27d8
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd1d2638
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1ba4b0
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd1ba470 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd1ba4d8 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2e7f68
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd2e7f28 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd2e7f90 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd6ee768
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd6ee728 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd6ee790 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd3c9f10
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd3c9ed0 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd3c9f38 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd217f90
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd217df0
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1e9a08
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd1e9868
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1208e0
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd1208a0 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd120908 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1de0f0
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd1ddf50
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd415dc0
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd415c20
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd200ff8
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd200fb8 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd201020 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2f3630
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd2f3490
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd0f9658
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd0f9618 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd0f9680 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd1de490
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd1de450 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd1de4b8 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2b7160
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd2b7120 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd2b7188 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2b6dc0
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd2b6c20
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd30bd28
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd30bb88
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd658638
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd6585f8 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd658660 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2c2a78
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd2c2a38 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd2c2aa0 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd14f568
                 -> System.Windows.WeakEventManager+ListenerList<System.Windows.RoutedEventArgs> 000001ba`bd14f3c8
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd094270
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd094230 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd094298 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                 -> MS.Internal.WeakEventTable+EventKey 000001ba`bd2d0c48
                 -> System.Collections.Specialized.HybridDictionary 000001ba`bd2d0c08 000001ba`bcf2c298 System.ComponentModel.ReflectPropertyDescriptor = 000001ba`bd2d0c70 MS.Internal.Data.ValueChangedEventManager+ValueChangedRecord
                -> _keys = System.Collections.Hashtable+KeyCollection 000001ba`bd0615c8
               -> _dispatcher = System.Windows.Threading.Dispatcher 000001ba`bce43fb0
               -> _eventNameTable = System.Collections.Hashtable 000001ba`bcee0020
               -> _lock = MS.Internal.ReaderWriterLockWrapper 000001ba`bcee00c8
               -> _managerTable = System.Collections.Hashtable 000001ba`bcedff90
               -> _toRemove = System.Collections.Generic.List<MS.Internal.WeakEventTable+EventKey> 000001ba`bcee01a8
        -> System.Windows.Automation.Peers.TextBoxAutomationPeer 000001ba`bd1b1fa8
        -> System.Windows.Media.SolidColorBrush 000001ba`bce8c378
        -> System.Windows.Thickness 000001ba`bcf1a0d0
        -> System.Windows.Media.SolidColorBrush 000001ba`bcf26b78
        -> System.Windows.Media.SolidColorBrush 000001ba`bcf28458
        -> System.Windows.HorizontalAlignment 000001ba`bcf1b2e8 0 0
        -> System.Windows.Input.KeyboardNavigationMode 000001ba`bcf1ad48 3 3
        -> System.Boolean 000001ba`bce698f0 True True
        -> System.Windows.Controls.ControlTemplate 000001ba`bcf1c4f0
        -> System.Windows.ModifiedValue 000001ba`bd1a6200
        -> System.Double 000001ba`bd1b1980 28 28
        -> System.Windows.Documents.TextEditor 000001ba`bd1a5b78
        -> System.Windows.Controls.PanningMode 000001ba`bcf1c348 5 5
        -> System.Boolean 000001ba`bce69908 False False
        -> MS.Internal.Documents.UndoManager 000001ba`bd1a5c28
       -> _newTextValue = MS.Internal.NamedObject 000001ba`bce3e750
       -> _parent = System.Windows.Controls.StackPanel 000001ba`bd1a50c8
       -> _renderScope = System.Windows.Controls.TextBoxView 000001ba`bd1ab4b8
       -> _templateCache = System.Windows.Controls.ControlTemplate 000001ba`bcf1c4f0
       -> _templateChild = System.Windows.Controls.Border 000001ba`bd1ab058
       -> _textBoxContentHost = System.Windows.Controls.ScrollViewer 000001ba`bd1ab240
       -> _textContainer = System.Windows.Documents.TextContainer 000001ba`bd1a5ac8
       -> _textEditor = System.Windows.Documents.TextEditor 000001ba`bd1a5b78
       -> _themeStyleCache = System.Windows.Style 000001ba`bcf11e10
     -> System.EventHandler 000001ba`bd1b2118 MS.Internal.Automation.TextAdaptor MS.Internal.Automation.TextAdaptor.OnTextSelectionChanged MS.Internal.Automation.TextAdaptor MS.Internal.Automation.TextAdaptor.OnTextSelectionChanged
  -> _textContainer = System.Windows.Documents.TextContainer 000001ba`bd1a5ac8
  -> _textstore = System.Windows.Documents.TextStore 000001ba`bd1b3280
  -> _textView = System.Windows.Controls.TextBoxView 000001ba`bd1ab4b8
  -> _uiScope = System.Windows.Controls.TextBox 000001ba`bd1a5930
  -> _weakThis = System.Windows.Documents.TextEditor+TextEditorShutDownListener 000001ba`bd1b3340

 
 */