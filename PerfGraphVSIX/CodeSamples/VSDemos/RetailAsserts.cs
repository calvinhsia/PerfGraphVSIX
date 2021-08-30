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

in Perfview, set Additional Providers: EE328C6F-4C94-45F7-ACAF-640C6A447654:@StacksEnabled=true
Name
+ Process64 devenv (6800) Args:  
 + Thread (19748) CPU=16ms (VS Main)
  + ntdll!?
   + kernel32!?
    + devenv!__scrt_common_main_seh
     + devenv!WinMain
      + devenv!CDevEnvAppId::Run
       + devenv!util_CallVsMain
        + msenv!?
         + ?!?
          + clr!?
           + microsoft.visualstudio.shell.interop!dynamicClass.IL_STUB_COMtoCLR()
            + microsoft.visualstudio.shell.15.0.ni!?
             + microsoft.visualstudio.threading.ni!?
              + mscorlib.ni!System.Threading.Tasks.SynchronizationContextAwaitTaskContinuation+<>c.<.cctor>b__8_0(System.Object)
               + mscorlib.ni!System.Runtime.CompilerServices.AsyncMethodBuilderCore+MoveNextRunner.Run()
                + mscorlib.ni!ExecutionContext.Run
                 + mscorlib.ni!ExecutionContext.RunInternal
                  + mscorlib.ni!System.Runtime.CompilerServices.AsyncMethodBuilderCore+MoveNextRunner.InvokeMoveNext(System.Object)
                   + tmp7529!MyCodeToExecute.MyClass+<DoItAsync>d__2.MoveNext()
                    + microsoft.visualstudio.commands!dynamicClass.IL_STUB_PInvoke(class System.String,class System.String,class System.String,int32,int32,class System.String)
                     + devenv!WriteAssertEtwEventA
                      + devenv!CAssertsEtwProvider::WriteAssertEtwEvent
                       + devenv!CAssertsEtwProvider::WriteAssertEtwEvent
                        + ntdll!?
                         + wow64!?
                          + wow64cpu!?
                           + wow64!?
                            + ntdll!?
                             + Event Provider(ee328c6f-4c94-45f7-acaf-640c6a447654)/EventID(3)


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
            GCHandle gch = default;
            try
            {
                var hDevenv = GetModuleHandle(null);
                var addrIsAssertEtwEnabled = GetProcAddress(hDevenv, "IsAssertEtwEnabled");
                var IsAssertEtwEnabled = Marshal.GetDelegateForFunctionPointer<delIsAssertEtwEnabled>(addrIsAssertEtwEnabled);
                var addrWriteAssertEtwEventA = GetProcAddress(hDevenv, "WriteAssertEtwEventA");
                var WriteAssertEtwEventA = Marshal.GetDelegateForFunctionPointer<delWriteAssertEtwEventA>(addrWriteAssertEtwEventA);
                _logger.LogMessage($" {addrWriteAssertEtwEventA.ToInt64():x}  {WriteAssertEtwEventA} ");
                var addrSetOnAssertCallback = GetProcAddress(hDevenv, "SetOnAssertCallback");
                var SetOnAssertCallback = Marshal.GetDelegateForFunctionPointer<delSetOnAssertCallback>(addrSetOnAssertCallback);
                var cb = new delOnAssertCallback(delegate (string assert, string msg, string file, int line)
                {
                    _logger.LogMessage($" in del callback {assert} {msg} {file} {line}");
                });
                gch = GCHandle.Alloc(cb);
                SetOnAssertCallback(cb);
                int n = 0;
                var isEnabled = IsAssertEtwEnabled();
                while (!_CancellationTokenExecuteCode.IsCancellationRequested)
                {
                    _logger.LogMessage($"Sending Retail Assert {hDevenv.ToInt64():x}  {addrIsAssertEtwEnabled.ToInt64():x}  {IsAssertEtwEnabled} {isEnabled}");

                    if (isEnabled)
                    {
                        WriteAssertEtwEventA($"Test{n}", $"msg{n}", $"file{n}", line: n, skipFrames: 0, "TestRetailAssert");
                        n++;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(2), _CancellationTokenExecuteCode);
                }
                SetOnAssertCallback(null);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogMessage($"Exception {ex.ToString()}");
            }
            if (gch.IsAllocated)
            {
                gch.Free();
            }
        }
        delegate bool delIsAssertEtwEnabled();
        delegate void delWriteAssertEtwEventA(
            [MarshalAs(UnmanagedType.LPStr)] string strAssert,
            [MarshalAs(UnmanagedType.LPStr)] string msg, // change from LPStr to LPWstr for WriteAssertEtwEventA/W
            [MarshalAs(UnmanagedType.LPStr)] string file,
            int line,
            int skipFrames,
            [MarshalAs(UnmanagedType.LPStr)] string AssertId);
        delegate void delOnAssertCallback(
            [MarshalAs(UnmanagedType.LPWStr)] string strAssert,
            [MarshalAs(UnmanagedType.LPWStr)] string msg,
            [MarshalAs(UnmanagedType.LPWStr)] string file,
            int line);
        delegate delOnAssertCallback delSetOnAssertCallback(delOnAssertCallback callback);

        [DllImport("kernel32", SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
