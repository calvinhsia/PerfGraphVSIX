using Microsoft.Test.Stress;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tests;

namespace TestStress
{
    [TestClass]
    public class Test64 : BaseTestClass
    {

        [TestMethod]
        [Ignore]
        public async Task TestEventHandlerDumpAnalysisAsync()
        {
            var dumpfileBaseline = @"C:\Users\calvinh\Downloads\Debugger_StartStop_25_0.dmp";
            var dumpfileCurrent = @"C:\Users\calvinh\Downloads\Debugger_StartStop_29_0 (1).dmp";
            LogMessage($"Start {nameof(TestEventHandlerDumpAnalysisAsync)}   {dumpfileCurrent}");
            /*
SizeCount AnalysisData         AdditionalInfo                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               MemoryAnalysisType MemoryKind
0   36    MultipleEventHandlersSystem.EventHandler Microsoft.CodeAnalysis.WorkspaceRegistration.WorkspaceChanged += Microsoft.CodeAnalysis.Editor.Shared.Tagging.TaggerEventSources+OptionChangedEventSource                                                                                                                                                                                                                                                                                                                                                                                                eventhandlers      managed_heap
0   158   MultipleEventHandlersSystem.EventHandler System.ComponentModel.EventHandlerList+ListEntry.handler += Microsoft.Internal.VisualStudio.PlatformUI.PolicyBasedDataSource                                                                                                                                                                                                                                                                                                                                                                                                                             eventhandlers      managed_heap
0   42    MultipleEventHandlersSystem.EventHandler System.ComponentModel.EventHandlerList+ListEntry.handler += Microsoft.Internal.VisualStudio.PlatformUI.PolicyBasedDataSourceCollection                                                                                                                                                                                                                                                                                                                                                                                                                   eventhandlers      managed_heap
0   45    MultipleEventHandlersSystem.ComponentModel.PropertyChangedEventHandler Microsoft.VisualStudio.PlatformUI.UIContextRules.SelectionMonitor.PropertyChanged += Microsoft.VisualStudio.PlatformUI.UIContextRules.ActiveProjectFlavorTerm                                                                                                                                                                                                                                                                                                                                                              eventhandlers      managed_heap
0   33    MultipleEventHandlersSystem.ComponentModel.PropertyChangedEventHandler Microsoft.VisualStudio.PlatformUI.UIContextRules.SelectionMonitor.PropertyChanged += Microsoft.VisualStudio.PlatformUI.UIContextRules.ActiveEditorContentTypeTerm                                                                                                                                                                                                                                                                                                                                                          eventhandlers      managed_heap
0   32    MultipleEventHandlersSystem.ComponentModel.PropertyChangedEventHandler Microsoft.VisualStudio.PlatformUI.UIContextRules.SelectionMonitor.PropertyChanged += Microsoft.VisualStudio.PlatformUI.UIContextRules.ActiveProjectCapabilityTerm                                                                                                                                                                                                                                                                                                                                                          eventhandlers      managed_heap
0   43    MultipleEventHandlersSystem.ComponentModel.RefreshEventHandler  += Microsoft.VisualStudio.Shell.OleMenuCommandService                                                                                                                                                                                                                                                                                                                                                                                                                                                                             eventhandlers      managed_heap
0   51    MultipleEventHandlersSystem.EventHandler<System.ValueTuple<System.Collections.Immutable.ImmutableDictionary<Microsoft.VisualStudio.Utilities.ServiceBroker.ServiceSource, System.Collections.Immutable.ImmutableDictionary<Microsoft.ServiceHub.Framework.ServiceMoniker, Microsoft.VisualStudio.Utilities.ServiceBroker.GlobalBrokeredServiceContainer+IProffered>>, System.Collections.Immutable.ImmutableHashSet<Microsoft.ServiceHub.Framework.ServiceMoniker>>> Microsoft.VisualStudio.Services.VSGlobalBrokeredServiceContainer.AvailabilityChanged += Microsoft.VisualStudio.Utilities.ServiceBroker.GlobalBrokeredServiceContainer+Vieweventhandlers      managed_heap
0   116   MultipleEventHandlersSystem.EventHandler<System.Exception> Microsoft.VisualStudio.ScriptedHost.WebView2Sandbox.SandboxThreadException += Microsoft.VisualStudio.ScriptedHost.ScriptedControl                                                                                                                                                                                                                                                                                                                                                                                                      eventhandlers      managed_heap
0   116   MultipleEventHandlersSystem.EventHandler<Microsoft.VisualStudio.ScriptedHost.SandboxManager+ISandbox> Microsoft.VisualStudio.ScriptedHost.WebView2Sandbox.shutdownListeners += Microsoft.VisualStudio.ScriptedHost.WebView2Browser                                                                                                                                                                                                                                                                                                                                                                eventhandlers      managed_heap
0   32    MultipleEventHandlersSystem.EventHandler<Microsoft.VisualStudio.Shell.Interop.IVsHierarchy> Microsoft.VisualStudio.PlatformUI.UIContextRules.HierarchyMonitor<Microsoft.VisualStudio.PlatformUI.UIContextRules.ProjectCapabilitiesChangeListener>.HierarchyChanged += Microsoft.VisualStudio.PlatformUI.UIContextRules.ActiveProjectCapabilityTerm                                                                                                                                                                                                                                                eventhandlers      managed_heap
             */
            await Task.Yield();
            var TotNumIterations = 29;
            var NumIterationsBeforeTotalToTakeBaselineSnapshot = 4;
            var analyzer = new DumpAnalyzer(this);
            var sb = new StringBuilder();
            analyzer.GetDiff(
                sb,
                dumpfileBaseline,
                dumpfileCurrent,
                TotNumIterations,
                NumIterationsBeforeTotalToTakeBaselineSnapshot, null, out _, out _
                );
            LogMessage("{0}", sb.ToString());
            //            var results = analyzer.AnalyzeDump(dumpfileCurrent, typesToReportStatisticsOn: null);

            //        foreach (var entryCurrent in results.dictEventHandlers
            //.Where(e => e.Value >= TotNumIterations)// there must be at least NumIterations
            //.OrderBy(e => e.Value))
            //        {
            //            var msg = string.Format("{0,5} {1,5} {2}", entryCurrent.Value, 0,entryCurrent.Key); // can't use "$" because can contain embedded "{"
            //            LogMessage(msg);
            //            //if (dictBase.TryGetValue(entryCurrent.Key, out var baseCnt)) // if it's also in the basedump
            //            //{
            //            //    if (baseCnt >= TotNumIterations - NumIterationsBeforeTotalToTakeBaselineSnapshot) // base must have grown at least 1 per iteration
            //            //    {
            //            //        if (baseCnt + NumIterationsBeforeTotalToTakeBaselineSnapshot <= entryCurrent.Value) // value has increased by at least 1 per iteration
            //            //        {
            //            //            actionDiff(entryCurrent.Key, baseCnt, entryCurrent.Value);
            //            //        }
            //            //    }
            //            //}
            //        }


        }

    }
}
