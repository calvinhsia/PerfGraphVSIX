//Include: ..\Util\LeakBaseClass.cs
//Desc: Demonstrate leak detection of handles

using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell;
using EnvDTE;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using HANDLE = System.IntPtr;


// https://devdiv.visualstudio.com/DevDiv/_wiki/wikis/DevDiv.wiki/3803/CancellationToken-and-CancellationTokenSource-Leaks


/*
 * Diagnosing kernel handle leaks
From: Lee Culver <leculver@microsoft.com> 
Sent: Monday, August 16, 2021 6:04 PM
To: Calvin Hsia <calvinh@microsoft.com>
Cc: Scott Wadsworth <bwadswor@microsoft.com>; Ashok Kamath <ashokka@microsoft.com>
Subject: RE: Possible handle leak solution

> Were you using PerfGraphVSIX to repeat Open/Close solution to find this? (how many iterations?)
I used PerfGraphVSIX to reproduce the problem.  This was the process:

1.	I turned on: “gflags /i devenv.exe +ust”.
2.	I loaded up VS.
3.	I attached dbgeng to VS after startup.
4.	I used the Open/Close solution demo project, iterations = 79.
5.	After a couple iterations I ran “!htrace -enable” in windbg.
6.	After the 79 iterations were completed I ran “!htrace -diff”.

I then post-processed debug output of “!htrace -diff”, filtered out some frames, and came up with a sorted list of handles created but not destroyed based on a blamed stack frame, here’s partial output of that:
mscordacwks!UTSemReadWrite::Init        452
msenv!CSolution::GetGuidProperty        170
clr!Thread::AllocHandles        52
dbgcore!Win32LiveSystemProvider::GetImageVersionInfo    51
CorperfmonExt!IPCReaderInterface::OpenBlockTableOnPid   48
Microsoft_VisualStudio_Utilities_ni!Microsoft.VisualStudio.LogHub.ServiceLogTraceListener.GetFileStreamFromLogPath(System.String)       29
clr!Thread::CreateNewOSThread   13
win32u!ZwUserMsgWaitForMultipleObjectsEx        8
wintrust!WintrustDllMain        5
mscorlib_ni!System.Threading.EventWaitHandle..ctor(Boolean, System.Threading.EventResetMode, System.String)     5
CoreMessaging!Microsoft::CoreUI::Dispatch::RegisteredWait::_Construct   5
RPCRT4!THREAD::THREAD   3
RPCRT4!EVENT::EVENT     3
dbgeng!OneTimeInitialization    3

Most of these are false positives.  Mscordacwks* is ClrMD related (might be a leak in the dac), which comes from PerfGraphVSIX using ClrMD.  Dbgcore/dbgeng is due to having windbg attached and aren’t leaks.  Clr thread creation creates a few handles, which are expected and will be cleaned up later. The CorperfmonExt one looks interesting, but I’m not sure where the code backing it lives.  And so on.

The next step is finding ways to automate this process in a way that can be captured by your stress runs automatically, but we’ll chat about that in our next sync.

Thanks!
-Lee

Bug 1374567: Handle leak in CSolution::GetGuidProperty VSSlnpst.cpp VSRegOpenOptionRoot
 */
namespace MyCodeToExecute
{

    public class MyClass : LeakBaseClass
    {
        class BigStuffWithLongNameSoICanSeeItBetter
        {
            public BigStuffWithLongNameSoICanSeeItBetter(int i)
            {
                var myevent = CreateEvent(IntPtr.Zero, false, false, "aa" + i.ToString()); // leaks kernel handles, this is used internally in CTS
            }
            [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Auto)]
            public static extern HANDLE CreateEvent(HANDLE lpEventAttributes, [In, MarshalAs(UnmanagedType.Bool)] bool bManualReset, [In, MarshalAs(UnmanagedType.Bool)] bool bIntialState, [In, MarshalAs(UnmanagedType.BStr)] string lpName);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern bool CloseHandle(IntPtr hHandle);
        }

        public static async Task DoMain(object[] args)
        {
            using (var oMyClass = new MyClass(args))
            {
                await oMyClass.DoTheTest(numIterations: 19, Sensitivity: 2.5);
            }
        }
        string somestring = "somestring1";
        string somestring2 = "somestring2";
        string somestring3 = "somestring3";
        CancellationTokenSource cts = new CancellationTokenSource();
        List<BigStuffWithLongNameSoICanSeeItBetter> _lst = new List<BigStuffWithLongNameSoICanSeeItBetter>();
        public MyClass(object[] args) : base(args)
        {
        }

        public override async Task DoInitializeAsync()
        {
        }

        public override async Task DoIterationBodyAsync(int iteration, CancellationToken cts)
        {
            // to test if your code leaks, put it here. Repeat a lot to magnify the effect
            for (int i = 0; i < 1; i++)
            {
                _lst.Add(new BigStuffWithLongNameSoICanSeeItBetter(i));
                //                var mre = new ManualResetEvent(initialState: false);// leaks mem and handles. Not a CTS leak (used internally by CTS)

                //var tk = cts.Token;
                //var cancellationTokenRegistration = tk.Register(() =>
                //{

                //});
                //cancellationTokenRegistration.Dispose(); // must dispose else leaks. CTS Leak Type No. 2



                //var newcts = new CancellationTokenSource();
                //var handle = newcts.Token.WaitHandle; // this internally lazily instantiates a ManualResetEvent
                //                newcts.Dispose(); // must dispose, else leaks mem and handles. CTS Leak Type No. 4
            }
        }
        public override async Task DoCleanupAsync()
        {
            cts.Dispose();
        }

    }
}
