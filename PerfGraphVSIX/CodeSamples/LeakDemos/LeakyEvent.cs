//Include: ..\Util\LeakBaseClass.cs
//Desc: this will demonstrate EventHandler leaks

using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using Microsoft.VisualStudio.Shell;
using EnvDTE;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using System.Linq;

namespace MyCodeToExecute
{
    class EditorClass
    {
        byte[] arr = new byte[1024 * 1024 * 20]; // big array to make class size bigger and leak more noticeable
        public EditorClass(MyClass myClass)
        {   //Subscribing to event means adding self to event's invocationlist. Can be a lambda or method (leaks differently)
            /* 
            myClass.OptionsChanged += OnOptionsChanged; // leaks entire class
            /*/
            myClass.OptionsChanged += (o, e) =>
            {
                myClass._logger.LogMessage($"In OptionsChanged"); // without a ref to a class member, the closure has no class ref so leak is smaller
                //var y = arr; // ref the arr so the closure has a ref to the class, so will leak the entire class
            };
            //*/
        }
        void OnOptionsChanged(object sender, EventArgs e)
        {
        }
    }
    public class MyClass : LeakBaseClass
    {
        public event EventHandler OptionsChanged; // MyClass publishes OptionsChanged
        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 17, delayBetweenIterationsMsec: 0);
            }
        }
        public MyClass(object[] args) : base(args) { }

        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts) // GC is done between each iteration
        {
            await ThreadHelper.JoinableTaskFactory.RunAsync(async () => // use TP thread so UI thread free
            {
                await Task.Yield();
                for (int i = 0; i < 1; i++)    // to test if your code leaks, repeat a lot to magnify the effect
                {
                    var x = new EditorClass(this);
                    OptionsChanged?.Invoke(this, null); // when raising event, all leaked event handlers get fired too!
                }
            });
        }
        public override async Task DoCleanupAsync()
        {
            await Task.Yield();
            // get the list of subscribers and show them
            var eventHandlerList = GetEventHandlerList<MyClass, EventArgs>(this, "OptionsChanged");
            foreach (var evHandler in eventHandlerList)
            {
                _logger.LogMessage($"   {evHandler.Target} {evHandler.Method}");
            }
            _logger.LogMessage($"Cleanup:  # Event Handlers Leaked =  {eventHandlerList.Length} ");
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
            return lstDelegates.ToArray(); ;
        }


        /// <summary>
        /// Get list of event handlers. These are normal eventhandlers (System.EventHandler and generic) not RoutedEventHandlers (a la WPF)
        /// e.g. var eventHandlerList = GetEventHandlerList<PdfViewerWindow, PdfViewerWindow.PdfExceptionEventAgs>(_pdfViewerWindow, nameof(PdfViewerWindow.PdfExceptionEvent));
        ///      var cntEvHandlers = eventHandlerList.Length;
        ///     foreach (var evHandler in eventHandlerList)
        ///     {
        ///         var targ = evHandler.Target;
        ///         var meth = evHandler.Method;
        ///     }
        /// </summary>
        /// <returns>Array of delegates or null</returns>
        internal static Delegate[] GetEventHandlerList<TEventPublisher, TEventArgs>(TEventPublisher instance, string eventName)
        {
            Delegate[] lstDelegates = null;
            try
            {
                var evFld = (typeof(TEventPublisher)
                    .GetField(eventName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
                if (evFld.GetValue(instance) as EventHandler != null)
                {
                    lstDelegates = (evFld
                        .GetValue(instance) as EventHandler) // check for null
                        .GetInvocationList();// check for null
                }
                else
                {
                    lstDelegates = (evFld
                        .GetValue(instance) as EventHandler<TEventArgs>) // check for null
                        .GetInvocationList();// check for null
                }
            }
            catch (Exception)
            {
            }
            return lstDelegates;
        }
    }
}
