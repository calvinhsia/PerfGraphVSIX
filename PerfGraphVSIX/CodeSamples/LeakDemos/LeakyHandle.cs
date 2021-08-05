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
