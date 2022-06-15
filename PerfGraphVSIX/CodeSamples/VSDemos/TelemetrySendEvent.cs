//Desc: Sample to show sending telemetry in the same session as VS

//Ref: %VSRoot%\Common7\IDE\PrivateAssemblies\Microsoft.VisualStudio.Telemetry.dll

//Include: ..\Util\MyCodeBaseClass.cs

using System;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PerfGraphVSIX;
using Microsoft.Test.Stress;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Telemetry;

namespace MyCodeToExecute
{
    // except for MyClass, most of this file is from https://devdiv.visualstudio.com/DevDiv/_git/VS?path=%2Fsrc%2Fvscommon%2FCodeMarkers%2FManagedCodeMarkers.cs

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
            /*
             this shows how to send a TelemetryEvent.
            You can use the TelemetryMonitor.cs code sample with a filter to your event to monitor the event sent
             */
            while (!_CancellationTokenExecuteCode.IsCancellationRequested)
            {
                var ev = new TelemetryEvent("PerfGraphVSIX/Telemetry/EventFromCodeSample");
                ev.Properties["localtime"] = DateTime.Now.ToString(); // to test what time zones
                var testStr = new string('a', 1020) + "123456";
                ev.Properties["longstring"] = testStr;// the 1 will show, followed by "..." = 1024
                var telc = new TelemetryComplexProperty(new // complex	{"Strvalue":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                {
                    Strvalue = testStr + testStr + testStr + testStr + "hi"
                });
                ev.Properties["complex"] = telc; // limit 60k: https://wiki.vsdata.io/event_telemetry_properties#limits
                _logger.LogMessage($"Sending TelemetryEvent '{ev}'");
                //
                TelemetryService.DefaultSession.PostEvent(ev);
                await Task.Delay(TimeSpan.FromSeconds(1));
                // verified
                /*
                 * 
                 * Query:
 cluster("https://DDTelVSRaw.kusto.windows.net").database("VS").RawEventsVSUnclassified
| where  EventName startswith "Perfgraph"

            // this really works, as verified by a PerfView trace with CodeMarker provider enabled:
            // additional providers = 641d7f6c-481c-42e8-ab7e-d18dc5e5cb9e:@StacksEnabled=true
Name
+ Process64 devenv (18828) Args:   /rootsuffix exp
 + Thread (43528) CPU=119ms
  + ntdll!?
   + kernel32!?
    + devenv!?
     + msenv!?
      + user32!?
       + ?!?
        + windowsbase.ni!?
         + mscorlib.ni!?
          + windowsbase.ni!?
           + mscorlib.ni!?
            + tmpbdfd!MyNameSpace.MyClass+<DoItAsync>d__3.MoveNext()
             + microsoft.visualstudio.telemetry.ni!?
              + microsoft.visualstudio.telemetry.package.ni!?
               + Microsoft.VisualStudio.Telemetry.Package!VSTelemetryEtwProvider.WriteTelemetryEvent
                + microsoft.visualstudio.utilities.ni!?
                 + mscorlib!EventSource.WriteImpl
                  + mscorlib.ni!?
                   + ntdll!?
                    + wow64!?
                     + wow64cpu!?
                      + wow64!?
                       + ntdll!?
                        + Event Microsoft-VisualStudio-Common/perfgraphvsix_telemetry_eventfromcodesample
                
                 */
            }
        }
    }
}
