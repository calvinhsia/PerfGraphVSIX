//Desc: Monitor CPU via GetProcessTimes

//Include: ..\Util\MyCodeBaseClass.cs
//Include: ..\Util\CloseableTabItem.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using PerfGraphVSIX;
using Microsoft.Test.Stress;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Settings;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Shell.Interop;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Xml;
using System.Windows.Markup;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;

namespace MyCodeToExecute
{
    public class MyClass : MyCodeBaseClass
    {
        public static async Task DoMain(object[] args)
        {
            var oMyClass = new MyClass(args);
            try
            {
                await oMyClass.InitializeAsync();
            }
            catch (Exception ex)
            {
                var _logger = args[1] as ILogger;
                _logger.LogMessage(ex.ToString());
            }
        }


        MyClass(object[] args) : base(args) { }
        async Task InitializeAsync()
        {
            await Task.Yield();
            //CloseableTabItem tabItemTabProc = GetTabItem();

            //tabItemTabProc.TabItemClosed += (o, e) =>
            //{
            //    _perfGraphToolWindowControl.TabControl.SelectedIndex = 0;
            //    _logger.LogMessage($"close event ");
            //};

            //            var hcurproc = Process.GetCurrentProcess().Handle;
            var numprocs = GetActiveProcessorCount(ALL_PROCESSOR_GROUPS);
            if (!GetProcessTimes(Process.GetCurrentProcess().Handle, out _, out _, out var kernel, out var user))
            {
                var hr = Marshal.GetLastWin32Error();
                _logger.LogMessage($"GetProcesssTimes error {hr:x8}");
                return;
            }
            var userTime = FiletimeToTimeSpan(user);
            var kernelTime = FiletimeToTimeSpan(kernel);
            var userStart = GetDeltaUserTime(default(TimeSpan));
            _logger.LogMessage($"In Init: NumProcs = {numprocs} userStart = {userStart} {kernelTime}  {userTime}  {user.dwHighDateTime:x8} {user.dwLowDateTime:x8}");
            var tcs = new TaskCompletionSource<int>();
            await Task.Run(async () =>
                {
                    var tspan = TimeSpan.FromSeconds(10);
                    var sw = Stopwatch.StartNew();
                    var cnt = 0ul;
                    while (!_CancellationTokenExecuteCode.IsCancellationRequested)
                    {
                        //                        await Task.Delay(TimeSpan.FromSeconds(1));
                        //_logger.LogMessage($"delay {GetDeltaUserTime(userStart).TotalSeconds}");
                        cnt++;
                        if (sw.Elapsed > tspan)
                        {
                            _logger.LogMessage($"Break after tspan {cnt:n0}");
                            break;
                        }
                    }
                    if (GetProcessTimes(Process.GetCurrentProcess().Handle, out _, out _, out var kernel2, out var user2))
                    {
                        var deltkernel = FiletimeToTimeSpan(kernel2) - kernelTime;
                        var deltuser = FiletimeToTimeSpan(user2) - userTime;
                        _logger.LogMessage($"final {FiletimeToTimeSpan(user2)} {user2.dwHighDateTime:x8} {user2.dwLowDateTime:x8}");
                        _logger.LogMessage($"Done: NumProcs = {numprocs} {deltkernel.TotalSeconds:n2}  {deltuser.TotalSeconds:n2}");
                    }
                    else
                    {
                        var hr = Marshal.GetLastWin32Error();
                        _logger.LogMessage($"GetProcesssTimes error {hr:x8}");
                        _logger.LogMessage($"deltausertime {GetDeltaUserTime(userStart).TotalSeconds}");

                    }
                    tcs.SetResult(0);
                });
            await tcs.Task;
        }

        TimeSpan GetDeltaUserTime(TimeSpan tsStart)
        {
            if (!GetProcessTimes(Process.GetCurrentProcess().Handle, out _, out _, out var kernel, out var user))
            {
                var hr = Marshal.GetLastWin32Error();
                _logger.LogMessage($"GetProcesssTimes error {hr:x8}");
            }
            else
            {
                _logger.LogMessage($"GetProcesssTimes raw value {user.dwHighDateTime:x0} {user.dwLowDateTime:x0}");
                if (tsStart == default(TimeSpan))
                {
                    return FiletimeToTimeSpan(user);
                }
                return FiletimeToTimeSpan(user) - tsStart;
            }
            return default(TimeSpan);
        }


        public const int ALL_PROCESSOR_GROUPS = 0xffff;
        [DllImport("kernel32.dll")]
        public static extern int GetActiveProcessorCount(int processorgroup);
        //each increment is 100 nanoseconds:  so 10 million per second
        //https://microsoft.visualstudio.com/OS/_git/os.2020?path=/minkernel/kernelbase/process.c&_a=contents&version=GBofficial/main
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetProcessTimes(IntPtr hProcess,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpCreationTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpExitTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

        public static DateTime FiletimeToDateTime(System.Runtime.InteropServices.ComTypes.FILETIME fileTime)
        {
            //NB! uint conversion must be done on both fields before ulong conversion
            ulong hFT2 = unchecked((((ulong)(uint)fileTime.dwHighDateTime) << 32) | (uint)fileTime.dwLowDateTime);
            return DateTime.FromFileTimeUtc((long)hFT2);
        }

        public static TimeSpan FiletimeToTimeSpan(System.Runtime.InteropServices.ComTypes.FILETIME fileTime)
        {
            //NB! uint conversion must be done on both fields before ulong conversion
            ulong hFT2 = unchecked((((ulong)(uint)fileTime.dwHighDateTime) << 32) | (uint)fileTime.dwLowDateTime);
            return TimeSpan.FromTicks((long)hFT2);
        }
    }
}
