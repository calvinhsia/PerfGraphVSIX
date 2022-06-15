//Desc: Shows how to fire FaultEvent

//Include: ..\Util\MyCodeBaseClass.cs
//Ref: %VSRoot%\Common7\IDE\PrivateAssemblies\Microsoft.VisualStudio.Telemetry.dll

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
            try
            {
                /*
                 this shows how to send a FaultEvent.
                You can use the TelemetryMonitor.cs code sample with a filter to your event to monitor the event sent
                 */
                while (!_CancellationTokenExecuteCode.IsCancellationRequested)
                {
                    _logger.LogMessage($"Sending Fault Event");

                    var ev = new FaultEvent("PerfGraphVSIX/Telemetry/EventFromCodeSample/FaultEvent","A fault occured");
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
                    await Task.Delay(TimeSpan.FromSeconds(2), _CancellationTokenExecuteCode);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogMessage($"Exception {ex.ToString()}");
            }
        }
    }
}
