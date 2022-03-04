//Desc: Sample code to show how to initiate a targeted trace

//Include: ..\Util\MyCodeBaseClass.cs

//Ref: %VSRoot%\Common7\IDE\PrivateAssemblies\Newtonsoft.Json.13.0.1.0\Newtonsoft.Json.dll
//Ref: %VSRoot%\Common7\IDE\PrivateAssemblies\Microsoft.VisualStudio.Telemetry.dll


using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Microsoft.VisualStudio.Telemetry;
using Microsoft.VisualStudio.RemoteSettings;

namespace MyCodeToExecute
{
    // Most of the codemarker stuff in this file is from https://devdiv.visualstudio.com/DevDiv/_git/VS?path=%2Fsrc%2Fvscommon%2FCodeMarkers%2FManagedCodeMarkers.cs

    public class MyClass : MyCodeBaseClass
    {
        public static async Task DoMain(object[] args)
        {
            var ox = new MyClass(args);
            await ox.DoItAsync();
        }
        MyClass(object[] args) : base(args) { }
        async Task DoItAsync()
        {
#if false
reg add hkcu\Software\Microsoft\VisualStudio\16.0_Remote\PerfWatson\InternalUser /v SatProcRuleId /d myruleid
reg add hkcu\Software\Microsoft\VisualStudio\16.0_Remote\PerfWatson\InternalUser /v SatProcFriendlyName /d MyFriendlyName
reg add hkcu\Software\Microsoft\VisualStudio\16.0_Remote\PerfWatson\InternalUser /v SatProcSatProcToMonitor /d perfwatson2.exe
reg add hkcu\Software\Microsoft\VisualStudio\16.0_Remote\PerfWatson\InternalUser /v SatProcMemoryThresholdMBytes /d 10000
reg add hkcu\Software\Microsoft\VisualStudio\16.0_Remote\PerfWatson\InternalUser /v SatProcMonitorPeriodSecs /d 10
reg add hkcu\Software\Microsoft\VisualStudio\16.0_Remote\PerfWatson\InternalUser /v SatProcSatProcsToDump /d perfwatson2.exe
reg add hkcu\Software\Microsoft\VisualStudio\16.0_Remote\PerfWatson\InternalUser /v SatProcTest /d 1
#endif
            // reg add hkcu\Software\Microsoft\VisualStudio\16.0_Remote\PerfWatson\InternalUser /v SatProcRuleId /d myruleid
            // writing from within the VS process writes to mounted hive
            //using (var hKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\VisualStudio\16.0_Remote\PerfWatson\InternalUser", writable: true))
            //{
            //    hKey.SetValue("SatProcRuleId", "Myruleid");
            //    hKey.SetValue("SatProcFriendlyName", "MyFriendlyName");
            //    hKey.SetValue("SatProcSatProcToMonitor", "perfwatson2.exe");
            //    hKey.SetValue("SatProcMemoryThresholdMBytes", "10000");
            //    hKey.SetValue("SatProcMonitorPeriodSecs", "10");
            //    hKey.SetValue("SatProcSatProcsToDump", "perfwatson2.exe");
            //    hKey.SetValue("SatProcTest", "1");

            //    //                var x = hKey.GetValue("SatProcRuleId");
            //}
            //using (var hKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\VisualStudio", writable: true))
            //{
            //    hKey.DeleteSubKeyTree(@"16.0_Remote");
            //}
            //return;
            while (!_CancellationTokenExecuteCode.IsCancellationRequested)
            {
                // Signal Perfwatson to start the tracing
                // Tracing may not start because e.g. not enough disk space, sampling (rule might say collect trace 10% of time), tracing already happening for another rule, etc
                // we blindly send these markers hoping for a trace to start.
                // Because we don't know we can't unsubscribe the stop, abandon events when the start condition fires

                var tracingAction = new DynamicTracingAction()
                {
                    FriendlyName = "TargetedTracingTest",
                    Description = "Testing Targeted tracing",
                    Test = "1",
                    TraceDurationSecs = 10,
                    MinTraceDurationSecs = 10,
                    //                    TracingActions = "DevenvProcessDumpFull;SatProcDump",
                    TracingActions = "EtlTrace,DevenvProcessDumpFull,SatProcDump",
                    //SatProcsToDump = "perfwaTson2.exe"
                    SatProcsToDump = "perfwaTson2.exe;Microsoft.ServiceHub.controller.exe;Microsoft.ServiceHub.SettingsHost.exe;Microsoft.ServiceHub.Identityhost.exe;msbuild.exe;ServiceHub.DataWarehouseHost.exe;ServiceHub.TestWindowStoreHost.exe;Microsoft.Alm.Shared.Remoting.RemoteContainer.dll"
                    // each has a Conhost.exe child proc https://www.howtogeek.com/howto/4996/what-is-conhost.exe-and-why-is-it-running/
                    // Microsoft.ServiceHub.controller.exe,ServiceHub.DataWarehouseHost.exe, ServiceHub.TestWindowStoreHost.exe are 64 bit, but not started by default
                    // Microsoft.Alm.Shared.Remoting.RemoteContainer.dll;ServiceHub.VSDetouredHost.exe;ServiceHub.Host.CLR.x86.exe;ServiceHub.ThreadedWaitDialog.exe
                };

                var actionWrapper = new ActionWrapper<DynamicTracingAction>
                {
                    RuleId = "TestingRule",
                    Action = tracingAction
                };

                _logger.LogMessage($"Sending targetedTraceBegin CodeMarker {(int)CodeMarkerProviderEventIds.perfTargetedTraceBegin}");

                CodeMarkers.Instance.CodeMarkerEx((int)CodeMarkerProviderEventIds.perfTargetedTraceBegin,
                    CreateDynamicTracingCodeMarkerData(actionWrapper, CodeMarkerAction.StopAndUpload));



                await Task.Delay(TimeSpan.FromSeconds(1));
                break;

            }
        }
        // from https://devdiv.visualstudio.com/DevDiv/_git/VS?path=%2Fsrc%2Fenv%2Fshell%2FConnected%2FImpl%2FPackages%2FFeedback%2FTargetedNotification%2FTargetedNotificationProvider.cs&version=GBmaster&_a=contents
        /// <summary>
        /// Creates a CodeMarkerEx byte array parameter for a given trace action.
        /// </summary>
        /// <param name="traceAction">Action to use.</param>
        /// <returns>Byte array.</returns>
        private static byte[] CreateDynamicTracingCodeMarkerData(ActionWrapper<DynamicTracingAction> traceAction, CodeMarkerAction action)
        {
            var dynamicTracingActionContainer = new DynamicTracingActionContainer(traceAction.Action)
            {
                RuleId = traceAction.RuleId,
                CodeMarkerAction = (int)action,
            };
            var dynamicTracingActionContainerAsJsonString = dynamicTracingActionContainer.Serialize(indent: false);
            byte[] dynamicTracingActionContainerData = UnicodeEncoding.Unicode.GetBytes(dynamicTracingActionContainerAsJsonString);
            byte[] databuffer = new byte[dynamicTracingActionContainerData.Length + 2]; // null term
            Buffer.BlockCopy(src: dynamicTracingActionContainerData, srcOffset: 0, dst: databuffer, dstOffset: 0, count: dynamicTracingActionContainerData.Length);
            return databuffer;
        }
        // from https://devdiv.visualstudio.com/DevDiv/_git/VS?path=/src/vscommon/TestTools/PerfWatson2/Responsiveness/Listener/Microsoft.Performance.ResponseTime/EventProviders.cs
        public enum CodeMarkerProviderEventIds : ushort
        {
            ModalDialogBegin = 512,
            PerfVSFinsihedBooting = 7103, // end of startup
            PerfScenarioStop = 7491,      // end of startup based on diagnostic scenario provider
            InputDelay = 9445,                          // perfVSInputDelay RaiseInputDelayMarker  env\msenv\core\main.cpp
            ShellUIActiveViewSwitchEnd = 18116,         // perfShellUI_ActiveViewSwitchEnd  env\shell\viewmanager\viewmanager.cs
            PerfWatsonHangDumpCollected = 18712,        // Indicates that a hang dump was collected in the VS process, and has the watson report id as the payload.
            perfTargetedTraceBegin = 18987,
            perfTargetedTraceEnd = 18988,
            perfTargetedTraceAbandon = 18989
        }
    }
    // https://devdiv.visualstudio.com/DevDiv/_git/VSTelemetryAPI?path=%2Fsrc%2FMicrosoft.VisualStudio.Telemetry%2FRemoteSettings%2FTargetedNotifications%2FActionWrapper.cs&version=GBmaster&_a=contents
    /// <summary>
    /// An action of type T that is defined on the TargetedNotifications backend.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class ActionWrapper<T>
    {
        /// <summary>
        /// Gets the path under which this action lives.
        /// </summary>
        public string ActionPath { get; internal set; }

        /// <summary>
        /// Gets the typed action.
        /// </summary>
        public T Action { get; internal set; }

        /// <summary>
        /// Gets the precedence of actions of within the same ActionPath. Higher indicates higher precedence.
        /// </summary>
        public int Precedence { get; internal set; }

        /// <summary>
        /// Gets a unique identifier for the rule.
        /// </summary>
        public string RuleId { get; internal set; }

        /// <summary>
        /// Gets a name for the type of action.  Useful if consumer wants to handle processing their own actions.
        /// </summary>
        public string ActionType { get; internal set; }

        /// <summary>
        /// Gets an experimentation flight that needs to be enabled in order for this action to have been returned.
        /// </summary>
        public string FlightName { get; internal set; }

        /// <summary>
        /// Gets any subscription details for this action
        /// </summary>
        public ActionSubscriptionDetails Subscription { get; internal set; }

        internal string ActionJson { get; set; }
    }


    // from https://devdiv.visualstudio.com/DevDiv/_git/VS?path=%2Fsrc%2Fenv%2Fshell%2FConnected%2FImpl%2FPackages%2FFeedback%2FTargetedNotification%2FDynamicTracing.cs&version=GBmaster&_a=contents
    // This file is used to define the TelemetryEventNotification base class for DynamicTracingAction,
    // which is used in both PerfWatson2.exe and Feedback, so DynamicTracingAction.cs lives in VSCommon\Inc
    // PerfWatson has its own version of the same class (using an alias for the namespace "Microsoft.VisualStudio.Telemetry" which means something else there.
    internal class TelemetryEventNotification
    {
        /// <summary>
        /// The telemetry condition for which to start the execution.
        /// </summary>
        public ITelemetryEventMatch StartCondition { get; set; }

        /// <summary>
        ///  the telemetry condition for which to stop the execution. If it's a trace, and TraceDurationSecs!=0, then this is ignored.
        /// </summary>
        public ITelemetryEventMatch StopCondition { get; set; }

        /// <summary>
        ///  the telemetry event for which the action is abandoned (abandon the trace).
        /// </summary>
        public ITelemetryEventMatch AbandonCondition { get; set; }
    }


    // from https://devdiv.visualstudio.com/DevDiv/_git/VS?path=%2Fsrc%2Fvscommon%2Finc%2FDynamicTracingAction.cs&version=GBmaster&_a=contents
    // Copyright (c) Microsoft. All rights reserved.
    /*
    TraceNotifications are enabled for Telemetry Events from both PerfWatson and devenv processes.

    This code is in 2 processes
    1. Microsoft.VisualStudio.PerfWatson.dll for PerfWatson2.exe
    2. Microsoft.VisualStudio.Shell.Connected.dll for devenv.exe
    This gets complicated because PerfWatson has a Microsoft.VisualStudio.Telemetry namespace from years ago, before the AppInsights version.
    So TelemetryEventNotification is defined in both processes slightly differently: the PerfWatson one uses extern alias
      */


    public enum CodeMarkerAction // match src\env\shell\Connected\Impl\Packages\Feedback\TargetedNotification\TargetedNotificationProvider.cs
    {
        None = 0,
        StopAndUpload = 1,
        Abandon = 2,
    }

    /// <summary>
    /// Specifies the action to take when triggered by telemetry event
    /// e.g.
    ///     VSActivity Log,
    ///     dumps of sat processes, e.g. servicehub
    ///     servicehub logs in %temp%\servicehub\logs.
    /// </summary>
    [Flags]
    public enum TraceProfileAction
    {
        /// <summary>
        /// none determined yet
        /// </summary>
        None = 0,

        /// <summary>
        /// Gets an ETL trace. See also AdditionalETWProviders
        /// </summary>
        EtlTrace = 0x1,

        /// <summary>
        /// "C:\Windows\Microsoft.NET\Framework\v4.0.30319\ngen.log"
        /// </summary>
        NGenLog = 0x20,

        /// <summary>
        /// %temp%\servicehub\logs
        /// </summary>
        ServiceHubLogs = 0x40,

        /// <summary>
        /// Gets a triage dump (no memory)
        /// </summary>
        DevenvProcessDumpTriage = 0x80,

        /// <summary>
        /// Gets a full heap dump including memory
        /// </summary>
        DevenvProcessDumpFull = 0x100,

        /// <summary>
        /// Gets a full heap dump including memory of a VS Satellite process, such as ServiceHub
        /// </summary>
        SatProcDump = 0x200,

    }

    /// <summary>
    /// The DynamicTracingAction describes the action(s) to be taken when we receive a trigger.
    /// Both devenv.exe and perfwatson2.exe can subscribe to telemetry events to trigger these actions.
    /// From devenv, the class is serialized as JSON and sent to PW via CodeMarker/ETW event to deserialize and process
    /// Thus this class exists in both exes, so this file is in VSCommon\Inc
    /// DynamicTracingAction Inherits from the same class TelemetryEventNotification, but because of the namespace conflicts, there's one in devenv and one in perfwatson using "extern alias AppInsights"
    ///
    ///   The trigger is either:
    ///        1. a Telemetry Notification with a StartCondition defined.
    ///            e.g.   when a telemetry event called "Vs/perfwatson2/ClrPausePct" with a property "vs.perf.clrpausepct.pausepct10" value less than 90.
    ///        2. StartCondtion is null, then this DynamicTracingAction starts immediately
    /// The action taken once triggered is defined by the TracingProfile member, which is a TraceProfileAction flags enum . E.g. start an ETL trace and/or collect a dump or logs
    ///
    /// The DynamicTracingAction Class is authored at a website in Redmond and serialized from JSON ActionData from Targeted Notifications: , e.g. https://targetednotifications.vsdata.io/edit/rule/86FE1ED9-5E30-49FC-96EC-6660E5EFC3C4
    /// Changes to the rule in Redmond end are visible almost immediately on the front end client
    ///
    /// We need to know when to stop the tracing:
    ///    1. Could be abandoned (by a telemetry event, or out of disk space).
    ///             If The TraceDurationSecs is 0 and the Stop or Abandon condition is expected and they don't occur, we automatically abandon traces if trace duration > TimeoutSecs.
    ///    2. Could be stopped when a StopCondition Telemetry event (if any) occurs
    ///    3. Could be a timed trace: stopped automatically when the max(TraceDurationSecs,MinTraceDurarionSecs) have elapsed
    ///
    /// Snapshotting:
    ///    It's important to start tracing as fast as possible: the anomalous condition we're trying to diagnose via trace has already occurred, and we may be too late to capture anything useful
    ///    It takes time to start a trace, including delays in telemetry notification service, sending the information to PerfWatson, starting a trace using StandardCollector
    ///    If we could trace into a circular buffer, then we could capture for a very long time (600 seconds), discarding anything "old" and keeping only the last e.g. 60 seconds.
    ///    Our circular buffer: If the trace exceeds SnapShotDuration, then:
    ///         the ETL trace is "snapshotted"
    ///         the NumSnapshotsToKeep snapshots are kept, older are discarded
    ///    The effect is a circular buffer (of size = NumSnapshotsToKeep * SnapShotDuration seconds), allowing a trace of hard to capture scenarios
    ///    If we're very interested in capturing what happened just before a StopCondition, specify a TraceDuration of 0: the trace will be NumSnapshotsToKeep * SnapshotDuration preceding the stop event
    ///    Caveat: the trace will include the creation of a snapshot
    ///    This means a Start event starts a trace, which is stopped by a Stop Condition 600 (or TraceDurationSecs) seconds later , and we have the last two 3 minute snapshots in a trace
    ///    The result is a trace consisting of NumSnapshotsToKeep snapshots, with all but the most recent being of length SnapshotDuration, and we could have discarded lots of snapshots
    ///
    /// Results: a Diagsession containing the requested data is uploaded to Watson and can be seen here: https://watson/Bucket?iEventType=685&OrderBy=1&MaxRows=100
    ///  The Bucket parameters :
    ///     P1 perfwatsondiagsesion
    ///     P2 RuleId Guid
    ///     P3 FriendlyName of rule
    ///     P4 Version of VS: e.g. 15.8.27909.3000
    ///     P5 0
    /// If everything is working, an uploaded cab should be visible within a few minutes.
    /// The cabs use the Watson Event type "VisualStudioMemWatson" and the retention policy is currently 30 days
    ///
    /// Testing: You can create a rule for your self at https://targetednotifications.vsdata.io. Look at the example rules
    /// For targeting,look at AppId Packages. For Telemetry events coming from devenv, you can leave this as default.
    /// To target telemetry events coming from PerfWatson, choose AppIdPackage PerfWatson
    /// Make sure the Action Path is "vs\feedback\dynamictracing" (as opposed to remote settings or promotions or infobar or survay)
    ///
    /// Then create a Kusto query and save it as a CSL file:
    ///        cluster('Ddtelvsraw').database('VS').RawEventsVS
    ///        | where UserAlias == "calvinh"
    ///        | summarize by UserId
    /// Then upload that CSL file as your rule's "Targets"
    /// When VS (or PerfWatson) starts, it sends a request for any remote items (such as promotions,
    /// For Domain Specific JSON, use these DynamicTracingAction class members.
    /// </summary>
    internal class DynamicTracingAction : TelemetryEventNotification
    {
        /// <summary>
        /// name used for watson bucket parameter.
        /// </summary>
        public string FriendlyName { get; set; }

        /// <summary>
        /// For clarity. Output to text file accompanying WatsonEvent.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// A string of comma separated case insensitive values of enum TraceProfileAction
        /// e.g. "EtlTrace, ngenlog, servicehublogs, devenvprocessdumpfull"
        /// unknown values are ignored.
        /// </summary>
        public string TracingActions { get; set; }

        /// <summary>
        /// 1 => once the event fires, unsubscribe (even if no action occurred due to sampling). Thus, if sampling is lt 100%, then don't set SingleNotification=1
        /// 0 => every event firing in a session could trigger the action (depending on sampling).
        /// </summary>
        public int SingleNotification { get; set; }

        /// <summary>
        /// Specifies additional ETW providers to turn on for a trace
        /// comma separated list of guids (cAse insensitive) without braces
        /// Microsoft-VisualStudio-CpsCore fdc862e2-43a9-5181-288a-7fade55cc9cc
        /// Microsoft-VisualStudio-CpsVs 2bf0e3de-e8a7-5821-ee81-ad9385d138a4
        /// Microsoft-VisualStudio-Threading 589491ba-4f15-53fe-c376-db7f020f5204
        /// Roslyn Provider BF965E67-C7FB-5C5B-D98F-CDF68F8154C2
        /// e.g. "fdc862e2-43a9-5181-288a-7fade55cc9cc"
        /// or   "fdc862e2-43a9-5181-288a-7fade55cc9cc;2bf0e3de-e8a7-5821-ee81-ad9385d138a4"
        /// or "763FD754-7086-4DFE-95EB-C01A46FAF4CA:0x2"  /*Provider = Microsoft-Windows-DotNETRuntimePrivate  Keyword = 2*/  fusion log
        /// Keyword defauls to MAX
        /// We could allow only those provider guids from a white list for security purposes.
        /// </summary>
        public string AdditionalETWProviders { get; set; }

        /// <summary>
        /// Do clr rundown will generate NGen PDBs to include in the diagsession. These are necessary for managed symbols
        /// This takes a long time
        /// and can cause a lot of crashes due to long path names: see Bug 590210: [Watson Failure] caused by FAIL_FAST_INVALID_ARG_c0000409_diasymreader.dll!PDB1::OpenNgenPdb.
        /// </summary>
        public int ClrRunDown { get; set; } = 1;


        /// <summary>
        /// Specify the satellite process name(s) separated by "," or ";".
        /// Must also set the 'SatProcDump' in the tracing actions above.
        /// If there are 0 or > 1 instances of the specified process, then will dump all of them. (e.g. multiple MSBUild)
        /// The child process dumped will be a descendant of VS as an ancestor to be dumped (could be > 1 instances of VS running)
        /// Case insensitive: e.g. "perfwaTson2.exe;ServiceHub.controller.exe;msbuild.exe"
        /// </summary>
        public string SatProcsToDump { get; set; }

        /// <summary>
        /// Specifies the number of snapshots to keep
        /// Often by the time we detect an anomalous condition it's too late to start a trace.
        /// When the start event occurs, we start tracing. If the desired ETL length is long, specified by the TraceDuration (> SnapShotDuration) then
        /// we imitate a circular buffer by taking NumSnapshotsToKeep snapshots
        /// For example, a Start/Stop could be 30 minutes apart, and SnapDuratrion could be 2 minutes, and NumSnapshotsToKeep = 2: the tracing will be on for 30 minutes, but only the last 2 2 minute snaps will be uploaded.
        /// </summary>
        public int NumSnapshotsToKeep { get; set; } = 2;

        /// <summary>
        /// A sustained condition might send more than one identical telemetry event. To get a trace of a sustained condition, set start event, no end event, SustainCount ==1 or more and the TraceDurationSecs to the desired length
        ///   which should be longer than the interval for the sustained events.
        ///   e.g. If you want to get a trace of The ClrPauseEvent if it occurs 3 times in 1 minute, set SustainCount to 2 and the TraceDuration= 90 (somewhat longer than 1 minute). Ensure TimeoutSeconds is long enough too
        /// if non-zero, indicates the number of Start Events required to get a successful trace.
        /// If the number of start events is less than the SustainCount, then the trace is automatically abandoned
        /// if num start events received >= Sustaincount, then the trace is successful.
        /// </summary>
        public int SustainCount { get; set; }

        /// <summary>
        ///  the trace will be abandoned if the timeout is reached before the StopCondition or TraceDurationSecs.
        /// </summary>
        public int TimeoutSecs { get; set; } = 120;

        /// <summary>
        /// Specifies the length of a snapshot in seconds.
        /// </summary>
        public int SnapShotDuration { get; set; } = 120;

        /// <summary>
        /// When non-zero, indicates how long the trace lasts in seconds.
        /// Triggers a Stop Tracing event, just as a Stop Condition would. Trace will end at max(TraceDurationSecs,MinTraceDurarionSecs)
        /// Don't need stop condition
        /// Don't do more than a couple minutes.
        /// </summary>
        public int TraceDurationSecs { get; set; }

        /// <summary>
        /// if we're taking a trace and the Stop condition occurs before TraceDurationSecs, then stop the trace at max(TraceDurationSecs,MinTraceDurationSecs)
        /// If the stop condition occurs, we will stop tracing when the trace length > MinTraceDurationSecs
        /// IOW, a successful trace must be > MinTrceDurationSecs length.
        /// Ignored if == 0
        /// see comments above.
        /// </summary>
        public int MinTraceDurationSecs { get; set; } = 30;

        /// <summary>
        /// the % of time this occurrence of the StartCondition will cause the action to be executed
        /// Does not affect backoff times.
        /// </summary>
        public int SamplingPercent { get; set; } = 100;

        /// <summary>
        /// Maximum # of occurrences of this particular event executed in a VS session.
        /// </summary>
        public int MaxPerSession { get; set; } = 10;

        /// <summary>
        /// The time in secs between any trace events of any kind
        /// if any kind of trace was taken, don't do any tracing actions until BackoffTime has elapsed
        /// So if multiple Start triggers occur, they will be ignored if a prior one ended less than BackoffTime seconds ago.
        /// </summary>
        public int BackoffTimeSecs { get; set; } = 60;

        /// <summary>
        /// The min # of secs between collections specifically for this event and no others. e.g. can collect LowMemAvailable dumps at most once per month.
        /// DateTime of last collection stored in registry at HKEY_CURRENT_USER\Software\Microsoft\PerfWatson\{RuleId}
        /// Regkey is version independent: we only want to collect for any version of VS on machine once per month
        /// 30 days = 2592000
        /// Abandoned traces do not affect the time recorded of the last collection action.
        /// </summary>
        public int BackoffTimeSecsPerRule { get; set; }

        /// <summary>
        /// used for testing. e.g. If Test == "1", the diagsession file is copied to %temp%\PWTemp for local analysis
        /// This should not be set for production rules. You can rename the diagsession to .zip file and drill in to see contents.
        /// </summary>
        public string Test { get; set; }

        public override string ToString()
        {
            return $"{FriendlyName} Duration = {TraceDurationSecs}";
        }
    }

    /// <summary>
    /// DynamicTracingActionContainer has all the data and extension methods to control Targeted Tracing via DynamicTracingActions, like take a trace or a dump
    /// DynamicTracingActionContainer adds a few items (CodeMarkerAction, RuleId) to the DynamicTracingAction.
    /// In the devenv process, this gets serialized via a codemarker/ETW event to PerfWatson, which will deserialize.
    /// Composition (Containing), Not inheriting from DynamicTracingAction because we're just adding a few properties for serialization.
    /// </summary>
    internal class DynamicTracingActionContainer
    {
        /// <summary>
        /// cast to CodeMarkerAction enum above.
        /// </summary>
        public int CodeMarkerAction { get; set; }
        public string RuleId { get; set; }
        public DynamicTracingAction DynamicTracingAction { get; set; }

        [JsonIgnore]
        public TraceProfileAction TraceProfileAction
        {
            get
            {
                return this.getTraceProfileAction.Value;
            }
        }

        private readonly Lazy<TraceProfileAction> getTraceProfileAction;

        public DynamicTracingActionContainer(DynamicTracingAction DynamicTracingAction)
        {
            this.DynamicTracingAction = DynamicTracingAction;
            this.getTraceProfileAction = new Lazy<TraceProfileAction>(() =>
            {
                TraceProfileAction action = TraceProfileAction.None;
                if (!string.IsNullOrEmpty(DynamicTracingAction.TracingActions))
                {
                    var actions = DynamicTracingAction.TracingActions.Split(new[] { ',', ';' });
                    foreach (var wrd in actions)
                    {
                        // convert string of multiple flags to enum
                        if (Enum.TryParse<TraceProfileAction>(wrd.Trim(), ignoreCase: true, result: out var result))
                        {
                            action |= result;
                        }
                    }
                }

                return action;
            });
        }

        /// <summary>
        /// serialized to pass to PerfWatson via codemarker etw event.
        /// </summary>
        public string Serialize(bool indent)
        {
            var str = JsonConvert.SerializeObject(
                this,
                indent ? Formatting.Indented : Formatting.None);
            return str;
        }

        public static DynamicTracingActionContainer Deserialize(string str)
        {
            // when we serialize from Devenv to PerfWatson, we don't need the telemetry match information
            // furthermore, an error occurs because the type is in a different name space, assembly and internal
            //      JsonSerializationException: Could not create an instance of type Microsoft.VisualStudio.Telemetry.ITelemetryEventMatch. Type is an interface or abstract class and cannot be instantiated.
            // so we use a custom jsonconverter that skips the ITelemetryEventMatch members (for Stop, Start and Abandon conditions)
            var obj = JsonConvert.DeserializeObject<DynamicTracingActionContainer>(
                str,
                new MyJsonConverter());
            return obj;
        }

        public override string ToString()
        {
            return $"Act={CodeMarkerAction} {RuleId} {DynamicTracingAction} {TraceProfileAction}";
        }
    }

    public class MyJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType.Name == "ITelemetryEventMatch";
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            reader.Skip();
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}




#pragma warning disable 0436
internal sealed class CodeMarkers
{
    // Singleton access
    public static readonly CodeMarkers Instance = new CodeMarkers();

    static class NativeMethods
    {
#if Codemarkers_IncludeAppEnum
            ///// Code markers test function imports
            [DllImport(TestDllName, EntryPoint = "InitPerf")]
            public static extern void TestDllInitPerf(IntPtr iApp);

            [DllImport(TestDllName, EntryPoint = "UnInitPerf")]
            public static extern void TestDllUnInitPerf(IntPtr iApp);
#endif // Codemarkers_IncludeAppEnum

        [DllImport(TestDllName, EntryPoint = "PerfCodeMarker")]
        public static extern void TestDllPerfCodeMarker(IntPtr nTimerID, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] aUserParams, IntPtr cbParams);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [DllImport(TestDllName, EntryPoint = "PerfCodeMarker")]
        public static extern void TestDllPerfCodeMarkerString(IntPtr nTimerID, [MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 2)] string aUserParams, IntPtr cbParams);

#if Codemarkers_IncludeAppEnum
            ///// Code markers product function imports
            [DllImport(ProductDllName, EntryPoint = "InitPerf")]
            public static extern void ProductDllInitPerf(IntPtr iApp);

            [DllImport(ProductDllName, EntryPoint = "UnInitPerf")]
            public static extern void ProductDllUnInitPerf(IntPtr iApp);
#endif // Codemarkers_IncludeAppEnum

        [DllImport(ProductDllName, EntryPoint = "PerfCodeMarker")]
        public static extern void ProductDllPerfCodeMarker(IntPtr nTimerID, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] aUserParams, IntPtr cbParams);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [DllImport(ProductDllName, EntryPoint = "PerfCodeMarker")]
        public static extern void ProductDllPerfCodeMarkerString(IntPtr nTimerID, [MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 2)] string aUserParams, IntPtr cbParams);

        ///// global native method imports
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern System.UInt16 FindAtom([MarshalAs(UnmanagedType.LPWStr)] string lpString);

#if Codemarkers_IncludeAppEnum
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            public static extern System.UInt16 AddAtom([MarshalAs(UnmanagedType.LPWStr)] string lpString);

            [DllImport("kernel32.dll")]
            public static extern System.UInt16 DeleteAtom(System.UInt16 atom);
#endif // Codemarkers_IncludeAppEnum

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle([MarshalAs(UnmanagedType.LPWStr)] string lpModuleName);
    }

    // Atom name. This ATOM will be set by the host application when code markers are enabled
    // in the registry.
    const string AtomName = "VSCodeMarkersEnabled";

    // Internal Test CodeMarkers DLL name
    const string TestDllName = "Microsoft.Internal.Performance.CodeMarkers.dll";

    // External Product CodeMarkers DLL name
    const string ProductDllName = "Microsoft.VisualStudio.CodeMarkers.dll";

    enum State
    {
        /// <summary>
        /// The atom is present. CodeMarkers are enabled.
        /// </summary>
        Enabled,

        /// <summary>
        /// The atom is not present, but InitPerformanceDll has not yet been called.
        /// </summary>
        Disabled,

        /// <summary>
        /// Disabled because the CodeMarkers transport DLL could not be found or
        /// an import failed to resolve.
        /// </summary>
        DisabledDueToDllImportException
    }

    private State state;

    /// <summary>
    /// Are CodeMarkers enabled? Note that even if IsEnabled returns false, CodeMarkers
    /// may still be enabled later in another component.
    /// </summary>
    public bool IsEnabled
    {
        get
        {
            return state == State.Enabled;
        }
    }

    // should CodeMarker events be fired to the test or product CodeMarker DLL
    private RegistryView registryView = RegistryView.Default;
    private string regroot = null;
    private bool? shouldUseTestDll;

    // This guid should match vscommon\testtools\PerfWatson2\Responsiveness\Listener\Microsoft.Performance.ResponseTime\ContextProviders\ScenarioContextProvider\ScenarioContextProvider.cs
    // And also match toolsrc\Telescope\Batch\PerfWatsonService.Reducer\SessionProcessors\ScenarioProcessor.cs
    private static readonly byte[] CorrelationMarkBytes = new Guid("AA10EEA0-F6AD-4E21-8865-C427DAE8EDB9").ToByteArray();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
    public bool ShouldUseTestDll
    {
        get
        {
            if (!this.shouldUseTestDll.HasValue)
            {
                try
                {
                    // this code can either be used in an InitPerf (loads CodeMarker DLL) or AttachPerf context (CodeMarker DLL already loaded)
                    // in the InitPerf context we have a regroot and should check for the test DLL registration
                    // in the AttachPerf context we should see which module is already loaded 
                    if (regroot == null)
                    {
                        this.shouldUseTestDll = NativeMethods.GetModuleHandle(ProductDllName) == IntPtr.Zero;
                    }
                    else
                    {
                        // if CodeMarkers are explictly enabled in the registry then try to
                        // use the test DLL, otherwise fall back to trying to use the product DLL
                        this.shouldUseTestDll = UsePrivateCodeMarkers(this.regroot, this.registryView);
                    }
                }
                catch (Exception)
                {
                    this.shouldUseTestDll = true;
                }
            }

            return this.shouldUseTestDll.Value;
        }
    }

    // Constructor. Do not call directly. Use CodeMarkers.Instance to access the singleton
    // Checks to see if code markers are enabled by looking for a named ATOM
    private CodeMarkers()
    {
        // This ATOM will be set by the native Code Markers host
        this.state = (NativeMethods.FindAtom(AtomName) != 0) ? State.Enabled : State.Disabled;
    }

    /// <summary>
    /// Sends a code marker event
    /// </summary>
    /// <param name="nTimerID">The code marker event ID</param>
    /// <returns>true if the code marker was successfully sent, false if code markers are
    /// not enabled or an error occurred.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    public bool CodeMarker(int nTimerID)
    {
        if (!IsEnabled)
            return false;

        try
        {
            if (this.ShouldUseTestDll)
            {
                NativeMethods.TestDllPerfCodeMarker(new IntPtr(nTimerID), null, new IntPtr(0));
            }
            else
            {
                NativeMethods.ProductDllPerfCodeMarker(new IntPtr(nTimerID), null, new IntPtr(0));
            }
        }
        catch (DllNotFoundException)
        {
            // If the DLL doesn't load or the entry point doesn't exist, then
            // abandon all further attempts to send codemarkers.
            this.state = State.DisabledDueToDllImportException;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Sends a code marker event with additional user data
    /// </summary>
    /// <param name="nTimerID">The code marker event ID</param>
    /// <param name="aBuff">User data buffer. May not be null.</param>
    /// <returns>true if the code marker was successfully sent, false if code markers are
    /// not enabled or an error occurred.</returns>
    /// <exception cref="ArgumentNullException">aBuff was null</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    public bool CodeMarkerEx(int nTimerID, byte[] aBuff)
    {
        if (!IsEnabled)
            return false;

        // Check the arguments only after checking whether code markers are enabled
        // This allows the calling code to pass null value and avoid calculation of data if nothing is to be logged
        if (aBuff == null)
            throw new ArgumentNullException("aBuff");

        try
        {
            if (this.ShouldUseTestDll)
            {
                NativeMethods.TestDllPerfCodeMarker(new IntPtr(nTimerID), aBuff, new IntPtr(aBuff.Length));
            }
            else
            {
                NativeMethods.ProductDllPerfCodeMarker(new IntPtr(nTimerID), aBuff, new IntPtr(aBuff.Length));
            }
        }
        catch (DllNotFoundException)
        {
            // If the DLL doesn't load or the entry point doesn't exist, then
            // abandon all further attempts to send codemarkers.
            this.state = State.DisabledDueToDllImportException;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Used by ManagedPerfTrack.cs to report errors accessing the DLL.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    public void SetStateDLLException()
    {
        this.state = State.DisabledDueToDllImportException;
    }


    /// <summary>
    /// Sends a code marker event with additional Guid user data
    /// </summary>
    /// <param name="nTimerID">The code marker event ID</param>
    /// <param name="guidData">The additional Guid to include with the event</param>
    /// <returns>true if the code marker was successfully sent, false if code markers are
    /// not enabled or an error occurred.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    public bool CodeMarkerEx(int nTimerID, Guid guidData)
    {
        return CodeMarkerEx(nTimerID, guidData.ToByteArray());
    }

    /// <summary>
    /// Sends a code marker event with additional String user data
    /// </summary>
    /// <param name="nTimerID">The code marker event ID</param>
    /// <param name="stringData">The additional String to include with the event</param>
    /// <returns>true if the code marker was successfully sent, false if code markers are
    /// not enabled or an error occurred.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    public bool CodeMarkerEx(int nTimerID, string stringData)
    {
        //return CodeMarkerEx(nTimerID, StringToBytesZeroTerminated(stringData));

        if (!IsEnabled)
            return false;

        // Check the arguments only after checking whether code markers are enabled
        // This allows the calling code to pass null value and avoid calculation of data if nothing is to be logged
        if (stringData == null)
            throw new ArgumentNullException("stringData");

        try
        {
            int byteCount = System.Text.Encoding.Unicode.GetByteCount(stringData) + sizeof(char);
            if (this.ShouldUseTestDll)
            {
                NativeMethods.TestDllPerfCodeMarkerString(new IntPtr(nTimerID), stringData, new IntPtr(byteCount));
            }
            else
            {
                NativeMethods.ProductDllPerfCodeMarkerString(new IntPtr(nTimerID), stringData, new IntPtr(byteCount));
            }
        }
        catch (DllNotFoundException)
        {
            // If the DLL doesn't load or the entry point doesn't exist, then
            // abandon all further attempts to send codemarkers.
            this.state = State.DisabledDueToDllImportException;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Converts a string into a byte buffer including a zero terminator (needed for proper ETW message formatting)
    /// </summary>
    /// <param name="stringData">String to be converted to bytes</param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    internal static byte[] StringToBytesZeroTerminated(string stringData)
    {
        var encoding = System.Text.Encoding.Unicode;
        int stringByteLength = encoding.GetByteCount(stringData);
        byte[] data = new byte[stringByteLength + sizeof(char)]; /* string + null termination */
        encoding.GetBytes(stringData, 0, stringData.Length, data, 0); // null terminator is already there, just write string over it
        return data;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    public static byte[] AttachCorrelationId(byte[] buffer, Guid correlationId)
    {
        if (correlationId == Guid.Empty)
        {
            return buffer;
        }

        byte[] correlationIdBytes = correlationId.ToByteArray();
        byte[] bufferWithCorrelation = new byte[CorrelationMarkBytes.Length + correlationIdBytes.Length + (buffer != null ? buffer.Length : 0)];
        CorrelationMarkBytes.CopyTo(bufferWithCorrelation, 0);
        correlationIdBytes.CopyTo(bufferWithCorrelation, CorrelationMarkBytes.Length);
        if (buffer != null)
        {
            buffer.CopyTo(bufferWithCorrelation, CorrelationMarkBytes.Length + correlationIdBytes.Length);
        }

        return bufferWithCorrelation;
    }

    /// <summary>
    /// Sends a code marker event with additional DWORD user data
    /// </summary>
    /// <param name="nTimerID">The code marker event ID</param>
    /// <param name="uintData">The additional DWORD to include with the event</param>
    /// <returns>true if the code marker was successfully sent, false if code markers are
    /// not enabled or an error occurred.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    public bool CodeMarkerEx(int nTimerID, uint uintData)
    {
        return CodeMarkerEx(nTimerID, BitConverter.GetBytes(uintData));
    }

    /// <summary>
    /// Sends a code marker event with additional QWORD user data
    /// </summary>
    /// <param name="nTimerID">The code marker event ID</param>
    /// <param name="ulongData">The additional QWORD to include with the event</param>
    /// <returns>true if the code marker was successfully sent, false if code markers are
    /// not enabled or an error occurred.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    public bool CodeMarkerEx(int nTimerID, ulong ulongData)
    {
        return CodeMarkerEx(nTimerID, BitConverter.GetBytes(ulongData));
    }

    /// <summary>
    /// Checks the registry to see if code markers are enabled
    /// </summary>
    /// <param name="regRoot">The registry root</param>
    /// <param name="registryView">The registry view.</param>
    /// <returns>Whether CodeMarkers are enabled in the registry</returns>
    private static bool UsePrivateCodeMarkers(string regRoot, RegistryView registryView)
    {
        if (regRoot == null)
        {
            throw new ArgumentNullException("regRoot");
        }

        // Reads the Performance subkey from the given registry key
        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView))
        using (RegistryKey key = baseKey.OpenSubKey(regRoot + "\\Performance"))
        {
            if (key != null)
            {
                // Read the default value
                // It doesn't matter what the value is, if it's present and not empty, code markers are enabled
                string defaultValue = key.GetValue(string.Empty).ToString();
                return !string.IsNullOrEmpty(defaultValue);
            }
        }

        return false;
    }

#if Codemarkers_IncludeAppEnum
        /// <summary>
        /// Check the registry and, if appropriate, loads and initializes the code markers dll.
        /// InitPerformanceDll may be called more than once, but only the first successful call will do anything.
        /// Subsequent calls will be ignored.
        /// For 32-bit processes on a 64-bit machine, the 32-bit (Wow6432Node) registry will be used.
        /// For 64-bit processes, the 64-bit registry will be used. If you need to use the Wow6432Node in this case
        /// then use the overload of InitPerformanceDll that takes a RegistryView parameter.
        /// </summary>
        /// <param name="iApp">The application ID value that distinguishes these code marker events from other applications.</param>
        /// <param name="strRegRoot">The registry root of the application. The default value of the "Performance" subkey under this
        /// root will be checked to determine if CodeMarkers should be enabled.</param>
        /// <returns>true if CodeMarkers were initialized successfully, or if InitPerformanceDll has already been called
        /// successfully once.
        /// false indicates that either CodeMarkers are not enabled in the registry, or that the CodeMarkers transport
        /// DLL failed to load.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public bool InitPerformanceDll(int iApp, string strRegRoot)
        {            
            return InitPerformanceDll(iApp, strRegRoot, RegistryView.Default);
        }

        /// <summary>
        /// Check the registry and, if appropriate, loads and initializes the code markers dll.
        /// InitPerformanceDll may be called more than once, but only the first successful call will do anything.
        /// Subsequent calls will be ignored.
        /// </summary>
        /// <param name="iApp">The application ID value that distinguishes these code marker events from other applications.</param>
        /// <param name="strRegRoot">The registry root of the application. The default value of the "Performance" subkey under this
        /// root will be checked to determine if CodeMarkers should be enabled.</param>
        /// <param name="registryView">Specify RegistryView.Registry32 to use the 32-bit registry even if the calling application
        /// is 64-bit</param>
        /// <returns>true if CodeMarkers were initialized successfully, or if InitPerformanceDll has already been called
        /// successfully once.
        /// false indicates that either CodeMarkers are not enabled in the registry, or that the CodeMarkers transport
        /// DLL failed to load.</returns>
        public bool InitPerformanceDll(int iApp, string strRegRoot, RegistryView registryView)
        {           
            // Prevent multiple initializations.
            if (IsEnabled)
            {
                return true;
            }

            if (strRegRoot == null)
            {
                throw new ArgumentNullException("strRegRoot");
            }
            
            this.regroot = strRegRoot;
            this.registryView = registryView;

            try
            {
                if (this.ShouldUseTestDll)
                {
                    NativeMethods.TestDllInitPerf(new IntPtr(iApp));
                }
                else
                {
                    NativeMethods.ProductDllInitPerf(new IntPtr(iApp));
                }
                
                this.state = State.Enabled;
                
                // Add an ATOM so that other CodeMarker enabled code in this process
                // knows that CodeMarkers are enabled 
                NativeMethods.AddAtom(AtomName);
            }
            // catch BadImageFormatException to handle 64-bit process loading 32-bit CodeMarker DLL (e.g., msbuild.exe)
            catch (BadImageFormatException)
            {
                this.state = State.DisabledDueToDllImportException;
            }
            catch (DllNotFoundException)
            {
                this.state = State.DisabledDueToDllImportException;
                return false;
            }

            return true;
        }

        
        // Opposite of InitPerformanceDLL. Call it when your app does not need the code markers dll.
        public void UninitializePerformanceDLL(int iApp)
        {
            bool? usingTestDL = this.shouldUseTestDll; // remember this or we can end up uninitializing the wrong dll.
            this.shouldUseTestDll = null; // reset which DLL we should use (needed for unit testing)
            this.regroot = null;

            if (!IsEnabled)
            {
                return;
            }

            this.state = State.Disabled;

            // Delete the atom created during the initialization if it exists
            System.UInt16 atom = NativeMethods.FindAtom(AtomName);
            if (atom != 0)
            {
                NativeMethods.DeleteAtom(atom);
            }

            try
            {
                if (usingTestDL.HasValue)  // If we don't have a value, then we never initialized the DLL.
                {
                    if (usingTestDL.Value)
                    {
                        NativeMethods.TestDllUnInitPerf(new IntPtr(iApp));
                    }
                    else
                    {
                        NativeMethods.ProductDllUnInitPerf(new IntPtr(iApp));
                    }
                }
            }
            catch (DllNotFoundException)
            {
                // Swallow exception
            }
        }        
#endif //Codemarkers_IncludeAppEnum
}

#if !Codemarkers_NoCodeMarkerStartEnd
/// <summary>
/// Use CodeMarkerStartEnd in a using clause when you need to bracket an
/// operation with a start/end CodeMarker event pair.  If you are using correlated
/// codemarkers and providing your own event manifest, include two GUIDs (the correlation
/// "marker" and the correlation ID itself) as the very first fields.
/// </summary>
internal struct CodeMarkerStartEnd : IDisposable
{
    private int _end;
    private byte[] _buffer;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    internal CodeMarkerStartEnd(int begin, int end, bool correlated = false)
    {
        Debug.Assert(end != default(int));
        this._buffer =
            correlated
            ? CodeMarkers.AttachCorrelationId(null, Guid.NewGuid())
            : null;
        this._end = end;
        this.CodeMarker(begin);
    }

    public void Dispose()
    {
        if (this._end != default(int)) // Protect against multiple Dispose calls
        {
            this.CodeMarker(this._end);
            this._end = default(int);
            this._buffer = null; // allow it to be GC'd
        }
    }

    private void CodeMarker(int id)
    {
        if (this._buffer == null)
        {
            CodeMarkers.Instance.CodeMarker(id);
        }
        else
        {
            CodeMarkers.Instance.CodeMarkerEx(id, this._buffer);
        }
    }
}

/// <summary>
/// Use CodeMarkerExStartEnd in a using clause when you need to bracket an
/// operation with a start/end CodeMarker event pair.  If you are using correlated
/// codemarkers and providing your own event manifest, include two GUIDs (the correlation
/// "marker" and the correlation ID itself) as the very first fields.
/// </summary>
internal struct CodeMarkerExStartEnd : IDisposable
{
    private int _end;
    private byte[] _aBuff;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    internal CodeMarkerExStartEnd(int begin, int end, byte[] aBuff, bool correlated = false)
    {
        Debug.Assert(end != default(int));
        this._aBuff =
            correlated
            ? CodeMarkers.AttachCorrelationId(aBuff, Guid.NewGuid())
            : aBuff;
        this._end = end;
        CodeMarkers.Instance.CodeMarkerEx(begin, this._aBuff);
    }

    // Specialization to use Guids for the code marker data
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    internal CodeMarkerExStartEnd(int begin, int end, Guid guidData, bool correlated = false)
        : this(begin, end, guidData.ToByteArray(), correlated)
    {
    }

    // Specialization for string
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    internal CodeMarkerExStartEnd(int begin, int end, string stringData, bool correlated = false)
        : this(begin, end, CodeMarkers.StringToBytesZeroTerminated(stringData), correlated)
    {
    }

    // Specialization for uint
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    internal CodeMarkerExStartEnd(int begin, int end, uint uintData, bool correlated = false)
        : this(begin, end, BitConverter.GetBytes(uintData), correlated)
    {
    }

    // Specialization for ulong
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    internal CodeMarkerExStartEnd(int begin, int end, ulong ulongData, bool correlated = false)
        : this(begin, end, BitConverter.GetBytes(ulongData), correlated)
    {
    }

    public void Dispose()
    {
        if (this._end != default(int)) // Protect against multiple Dispose calls
        {
            CodeMarkers.Instance.CodeMarkerEx(this._end, this._aBuff);
            this._end = default(int);
            this._aBuff = null;
        }
    }

#endif
}
