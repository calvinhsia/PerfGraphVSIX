//Include: ExecCodeBase.cs
// this will demonstate leak detection

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

    public class MyClass : BaseExecCodeClass
    {
        public event EventHandler Myevent;
        class BigStuffWithLongNameSoICanSeeItBetter
        {
            byte[] arr = new byte[1024 * 1024 * 20];
            public BigStuffWithLongNameSoICanSeeItBetter(MyClass obj)
            {
                //obj.Myevent += Obj_Myevent; // this form always leaks, even if Obj_Myevent is empty
                var x = 2;
                obj.Myevent += (o, e) =>
                  {
                      var y = arr; // this line causes leak because ref to local member lifted in closure
                  };
            }

            private void Obj_Myevent(object sender, EventArgs e)
            {
                throw new NotImplementedException();
            }
        }
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
        }

        public override async Task DoInitializeAsync()
        {
            await Task.Yield();
        }

        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            await Task.Yield();
            // to test if your code leaks, put it here. Repeat a lot to magnify the effect
            for (int i = 0; i < 1; i++)
            {
                var x = new BigStuffWithLongNameSoICanSeeItBetter(this);
            }
        }
        public override async Task DoCleanupAsync()
        {
            await Task.Yield();
            var eventHandlerList = GetEventHandlerList<MyClass, EventArgs>(this, "Myevent");
            logger.LogMessage(string.Format("Leaked: # Event Handlers =  {0} ", eventHandlerList.Length));
            foreach (var evHandler in eventHandlerList)
            {
                logger.LogMessage(string.Format("   {0} {1}", evHandler.Target, evHandler.Method));
            }
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
            var evHandlersStore = typeof(TEventPublisher)
                .GetProperty("EventHandlersStore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(instance, index: null);
            var miGetEvHandlers = evHandlersStore.GetType().GetMethod("GetRoutedEventHandlers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            var lstRoutedEvents = miGetEvHandlers.Invoke(evHandlersStore, new object[] { routedEvent }) as RoutedEventHandlerInfo[];
            var lstDelegates = new List<Delegate>();
            foreach (var handler in lstRoutedEvents)
            {
                lstDelegates.Add(handler.Handler);
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
            var evFld = (typeof(TEventPublisher)
                .GetField(eventName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
            if (evFld.GetValue(instance) as EventHandler != null)
            {
                var evList = (evFld
                    .GetValue(instance) as EventHandler) // check for null
                    .GetInvocationList();// check for null
                return evList;
            }
            else
            {
                var evList = (evFld
                    .GetValue(instance) as EventHandler<TEventArgs>) // check for null
                    .GetInvocationList();// check for null
                return evList;
            }
        }
    }
}
