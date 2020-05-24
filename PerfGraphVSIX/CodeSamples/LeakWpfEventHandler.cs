
// This code will be compiled and run when you hit the ExecCode button. Any error msgs will be shown in the status log control.
// This allows you to create a stress test by repeating some code, while taking measurements between each iteration.

//  Macro substitution: %PerfGraphVSIX% will be changed to the fullpath to PerfGraphVSIX
//                      %VSRoot% will be changed to the fullpath to VS: e.g. "C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview"

//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.8.0.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.10.0.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.11.0.dll
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.12.1.DesignTime.dll"
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.0.DesignTime.dll"
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.8.DesignTime.dll"
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Threading.dll"
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.15.0.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Framework.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.ComponentModelHost.dll

//Ref:"%VSRoot%\Common7\IDE\PublicAssemblies\envdte.dll"


//Ref: %PerfGraphVSIX%
//Pragma: GenerateInMemory = False
//Pragma: UseCSC = true

//Pragma: verbose = false

////Ref: c:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll


//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationFramework.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationCore.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\WindowsBase.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Xaml.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Xml.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.ComponentModel.Composition.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Core.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Windows.Forms.dll


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
//Include: ExecCodeBase.cs

/* This sample allows you to edit/compile/run code inside the VS process from within the same instance of VS
 * You can access VS Services, JTF, etc with the same code as you would from e.g. building a VS component
 * but the Edit/Build/Run cycle is much smaller and faster
 * rIntellisense mostly works. Debugging is via logging or output window pane.
 * */
namespace MyCodeToExecute
{
    public class MyClass : BaseExecCodeClass
    {
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 7, Sensitivity: 2.5, delayBetweenIterationsMsec: 800);
            }
        }
        public MyClass(object[] args) : base(args)
        {
            //ShowUI = false;
            //NumIterationsBeforeTotalToTakeBaselineSnapshot = 0;
            SecsBetweenIterations = 0.1;
        }
        MyWindow _MyWindow;
        public override async Task DoInitializeAsync()
        {
            await Task.Yield();
            _MyWindow = new MyWindow(this);
            //            _MyWindow.ShowDialog();
            _MyWindow.Show();
        }

        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            await Task.Yield();
            // to test if your code leaks, put it here. Repeat a lot to magnify the effect
            for (int i = 0; i < 10; i++)
            {
                _MyWindow.ChkBoxAction(i);
            }
//            await Task.Delay(TimeSpan.FromSeconds(20));
            var lstEventHandlers = GetRoutedEventHandlerList<MyCheckBox>(_MyWindow.chkBox, MyCheckBox.CheckedEvent);
           _logger.LogMessage(string.Format("#Ev Handlers = {0}", lstEventHandlers.Length));
        }
        public override async Task DoCleanupAsync()
        {
            await Task.Yield();
            _MyWindow.Close();
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


        public class MyWindow : Window
        {
            public MyClass _MyClass;
            DockPanel dp;
            public MyCheckBox chkBox;
            public int numClicks = 0;
            public MyWindow(MyClass MyClass)
            {
                this._MyClass = MyClass;
                this.Loaded += (ol, el) =>
                 {
                     try
                     {
                         _MyClass._logger.LogMessage("In Form Load");

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
        </StackPanel>
        <DockPanel x:Name=""_dp"" Grid.Row=""1""/>
        
    </Grid>
";

                         Width = 400;
                         Height = 600;
                         var strReader = new System.IO.StringReader(strxaml);
                         var xamlreader = XmlReader.Create(strReader);

                         var grid = (Grid)(XamlReader.Load(xamlreader));
                         grid.DataContext = this;
                         this.Content = grid;
                         var sp = (StackPanel)grid.FindName("_sp");
                         chkBox = new MyCheckBox() { Content = "Check Me" };
                         sp.Children.Add(chkBox);
                         var btnGo = new MyButton() { Content = "_Go" };
                         sp.Children.Add(btnGo);
                         dp = (DockPanel)grid.FindName("_dp");
                         btnGo.Click += (o, e) =>
                         {
                             try
                             {
                                 for (int i = 0; i < 10; i++)
                                 {
                                     ChkBoxAction(i);
                                 }
                                 var lstEventHandlers = GetRoutedEventHandlerList<MyCheckBox>(chkBox, MyCheckBox.CheckedEvent);
                                 _MyClass._logger.LogMessage("# evHandlers = " + lstEventHandlers.Length.ToString());
                             }
                             catch (Exception ex)
                             {
                                 this.Content = ex.ToString();
                             }
                         };
                     }
                     catch (Exception ex)
                     {
                         this.Content = ex.ToString();
                     }
                 };
            }
            public void ChkBoxAction(int i)
            {
                dp.Children.Clear();
                dp.Children.Add(new MyInkCanvas(this, chkBox, i));
            }
        }
        public class MyButton : Button
        {

        }
        public class MyInkCanvas : InkCanvas
        {
            byte[] arr = new byte[1024 * 1024 * 10];
            MyWindow _myWindow;

            public MyInkCanvas(MyWindow myWindow, MyCheckBox chkbox, int cnt)
            {
                this._myWindow = myWindow;
                this.Children.Add(new TextBlock() { Text = cnt.ToString() });
                /* Thousands of CheckBox eventhandlers:
                 Subscribing to the Checked method can cause a leak: the single ChkBox on the form is the Publisher of the Checked Event,
                 and it holds a list of the subscribers in it's System.Windows.EventHandlersStore _listStore
Children of "-- MyCodeToExecute.MyClass+MyCheckBox 0x21591f6c"
-- MyCodeToExecute.MyClass+MyCheckBox 0x21591f6c
 -> _dispatcher = System.Windows.Threading.Dispatcher 0x035feb50
 -> _dType = System.Windows.DependencyObjectType 0x21592064
 -> _effectiveValues = System.Windows.EffectiveValueEntry[] 0x21599554
  -> MyCodeToExecute.MyClass+MyWindow 0x2158d4a0
  -> System.Collections.Generic.List<System.Windows.DependencyObject> 0x215922ec
  -> System.Windows.DeferredThemeResourceReference 0x038a8d20
  -> System.Windows.EventHandlersStore 0x21599548
   -> _entries = MS.Utility.SingleObjectMap 0x21599604
    -> _loneEntry = MS.Utility.FrugalObjectList<System.Windows.RoutedEventHandlerInfo> 0x215995f8
     -> _listStore = MS.Utility.ArrayItemList<System.Windows.RoutedEventHandlerInfo> 0x215a41d4
      -> _entries = System.Windows.RoutedEventHandlerInfo[] 0x3a94c858
       -> System.Windows.RoutedEventHandler 0x21599528
       -> System.Windows.RoutedEventHandler 0x21599614
        -> _target = MyCodeToExecute.MyClass+MyInkCanvas 0x21595aa8
       -> System.Windows.RoutedEventHandler 0x2159cf7c
        -> _target = MyCodeToExecute.MyClass+MyInkCanvas 0x215996ac
       -> System.Windows.RoutedEventHandler 0x215a08a8
        -> _target = MyCodeToExecute.MyClass+MyInkCanvas 0x2159cfd8
       -> System.Windows.RoutedEventHandler 0x215a41e4
        -> _target = MyCodeToExecute.MyClass+MyInkCanvas 0x215a0904                If the event subscriber is an empty lambda, then it doesn't leak: the eventhandler count goes up

                 */

                chkbox.Checked += (o, e) =>
                {
                    var y = arr;
                    // the lambda needs to refer to a mem var else no leak: the # ev handlers goes up, but WPF is smart: they're all the same handler
                    //   _myWindow._MyClass.logger.LogMessage("Click" + (_myWindow.numClicks++).ToString());

                };
//                chkbox.Checked += ChkboxHandler;
            }
            void ChkboxHandler(object sender, RoutedEventArgs e)
            {
                _myWindow._MyClass._logger.LogMessage("Click" + (_myWindow.numClicks++).ToString());
            }
        }
        public class MyCheckBox : CheckBox
        {

        }
    }
}
