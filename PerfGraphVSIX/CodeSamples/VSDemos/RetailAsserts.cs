//Desc: Shows how to fire RetailAsserts

//Include: ..\Util\MyCodeBaseClass.cs
/*
// Assert ETW provider.
//
// Provider Guid: {EE328C6F-4C94-45F7-ACAF-640C6A447654}
// Events:
//  ID = 0, KeyWord = 0x1, Level = 3 (Warning)
//    A message event that contains an assert in format "Assert_Condition:Assert_Message:File_Path(Line_Number)"
//  ID = 2, KeyWord = 0x2, Level = 3 (Warning)
//    An event that contains detailed assert information with these fields packed together:
//      WCHAR_Z: Assert_Condition
//      WCHAR_Z: Assert_Message
//      WCHAR_Z: File_Path
//      ULONG:   Line_Number
//      USHORT:  Stack Depth. This is the number of the stack frames below.
//      INT64:   Stack Hash.  This is the hash of the stack frames below.
//      USHORT:  Frame Size.  This is the size of one stack frame.
//      PVOID[]: Stack Frames. Each frame is a PVOID to a stack frame.
//
// This class uses ETW events provider which has these requirement:
//   Min supported client: Windows Vista
//   Min supported server: Windows Server 2008



//https://devdiv.visualstudio.com/DevDiv/_git/VS?path=%2Fsrc%2Fvscommon%2Ftesttools%2FPerfWatson2%2FResponsiveness%2FListener%2FMicrosoft.Performance.ResponseTime%2FContextProviders%2FEtwContextProvider.cs&_a=contents&version=GBmain
 */

using System;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PerfGraphVSIX;
using Microsoft.Test.Stress;
using System.Threading.Tasks;

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
                int n = 0;
                while (!_CancellationTokenExecuteCode.IsCancellationRequested)
                {
                    var hDevenv = GetModuleHandle(null);
                    var addrIsAssertEtwEnabled = GetProcAddress(hDevenv, "IsAssertEtwEnabled");
                    var IsAssertEtwEnabled = Marshal.GetDelegateForFunctionPointer<delIsAssertEtwEnabled>(addrIsAssertEtwEnabled);
                    var isEnabled = IsAssertEtwEnabled();
                    _logger.LogMessage($"Sending Retail Assert {hDevenv.ToInt64():x}  {addrIsAssertEtwEnabled.ToInt64():x}  {IsAssertEtwEnabled} {isEnabled}");

                    if (isEnabled)
                    {
                        var addrWriteAssertEtwEventA = GetProcAddress(hDevenv, "WriteAssertEtwEventA");
                        var WriteAssertEtwEventA = Marshal.GetDelegateForFunctionPointer<delWriteAssertEtwEventA>(addrWriteAssertEtwEventA);
                        var addrSetOnAssertCallback = GetProcAddress(hDevenv, "SetOnAssertCallback");
                        var SetOnAssertCallback = Marshal.GetDelegateForFunctionPointer<delSetOnAssertCallback>(addrSetOnAssertCallback);
                        //SetOnAssertCallback(OnAssertCallback);
                        SetOnAssertCallback(delegate (string assert, string msg, string file, int line)
                        {
                            _logger.LogMessage($" in del callback {assert} {msg} {file} {line}");
                        });

                        _logger.LogMessage($" {addrWriteAssertEtwEventA.ToInt64():x}  {WriteAssertEtwEventA} ");
                        WriteAssertEtwEventA("test", "msg", "file", line: n++, skipFrames: 2, "AssertId");
                    }
                    await Task.Delay(TimeSpan.FromSeconds(5), _CancellationTokenExecuteCode);
                }
            }
            catch (OperationCanceledException)
            {
            }

        }
        //void OnAssertCallback(string assert, string msg, string file, int line)
        //{
        //    _logger.LogMessage($" in del callback {assert} {msg} {file} {line}");
        //}
        [System.Runtime.InteropServices.DllImport("kernel32", SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpFileName);
        delegate bool delIsAssertEtwEnabled();
        delegate void delWriteAssertEtwEventA([MarshalAs(UnmanagedType.LPStr)] string strAssert,[MarshalAs(UnmanagedType.LPStr)] string msg, [MarshalAs(UnmanagedType.LPStr)] string file, int line, int skipFrames, [MarshalAs(UnmanagedType.LPStr)] string AssertId);
        delegate void delOnAssertCallback([MarshalAs(UnmanagedType.LPWStr)] string strAssert, [MarshalAs(UnmanagedType.LPWStr)] string msg, [MarshalAs(UnmanagedType.LPWStr)] string file, int line);
        delegate delOnAssertCallback delSetOnAssertCallback(delOnAssertCallback callback);
        [DllImport("kernel32", SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

    }

}
