//Desc: Shows how to Get CallStack

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
using System.Security.Policy;

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
        /* 
https://accu.org/journals/overload/22/120/orr_1897/
https://microsoft.visualstudio.com/OS/_git/os?path=%2Fxbox%2Flnm%2Fntos%2Frtl%2Famd64%2Fstkwalk.c&version=GBofficial%2F19h1_release_svc_kir1&_a=contents

         * From native code:
                        vecFrames _vecFrames; // the stack frames
                        g_NumFramesTocapture = 40;
                        _vecFrames.resize(g_NumFramesTocapture);
                        ULONGLONG pHash = 0;
                        typedef int(WINAPI* pfnGetCallstack64)(PVOID pContext, int nSkipFrames, int numFrames, PVOID pFrames[], PINT nFramesWritten, PULONGLONG pHash);
                        pfnGetCallstack64 g_pfnGetCallstack64;
                        auto hmod = GetModuleHandle(0);
                        if (hmod != 0)
                        {
                            int nFramesWritten = 0;
                            auto addrGetCallStack64 = (pfnGetCallStack64)GetProcAddress(hmod, "GetCallstack64");
                            ULONG64 stackHash;
                            (*g_pfnGetCallstack64)(
                                nullptr, // context
                                NumFramesToSkip + 2, // for 64 bit optimized we skip 2 more non-interesting internal frames
                                g_NumFramesTocapture,
                                &_vecFrames[0],
                                &nFramesWritten,
                                &stackHash
                                );
                        }
         */
        void RecurSomeLevels(int nLevels, Action act)
        {
            if (nLevels == 0)
            {
                act();
            }
            else
            {
                if (nLevels % 2 == 0)// have the stack alternate between 2 addresses
                {
                    RecurSomeLevels(nLevels - 1, act);
                }
                else
                {
                    RecurSomeLevels(nLevels - 1, act);
                }
            }
        }
        async Task DoItAsync()
        {
            try
            {
                var outputpane = await GetOutputPaneAsync();
                var hDevenv = GetModuleHandle(null);
                var addrGetCallstack64 = GetProcAddress(hDevenv, "GetCallstack64");
                if (addrGetCallstack64 == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Couldn't find GetCallstack64 api");
                }
                var GetCallstack64 = Marshal.GetDelegateForFunctionPointer<delGetCallstack64>(addrGetCallstack64);
                _logger.LogMessage($" {addrGetCallstack64.ToInt64():x}  {GetCallstack64} ");
                while (!_CancellationTokenExecuteCode.IsCancellationRequested)
                {
                    outputpane.Clear();
                    RecurSomeLevels(20, () =>
                    {
                        int nFramesCollected = 0;
                        UInt64 hash = 0;
                        int nFrames = 200;
                        var arrFrames = new IntPtr[nFrames];
                        var useRtlCaptureStackBackTrace = false;
                        if (useRtlCaptureStackBackTrace) // WinAPI CaptureStackBackTrace
                        { 
                            nFramesCollected = RtlCaptureStackBackTrace(0, nFrames, arrFrames, ref hash);
                        }
                        else
                        { // my implementation
                            var hr = GetCallstack64(pContext: IntPtr.Zero, nSkipFrames: 1, nFrames: nFrames, frames: arrFrames, nFramesWritten: ref nFramesCollected, pHash: ref hash);
                        }
                        for (int i = 0; i < nFramesCollected; i++)
                        {
                            outputpane.OutputString($"{i,3} {arrFrames[i].ToInt64():x}\n");
                        }
                        outputpane.OutputString($"{DateTime.Now} Collected stack #frames = {nFramesCollected} {hash:x}\n");
                    });
                    await Task.Delay(TimeSpan.FromSeconds(5), _CancellationTokenExecuteCode);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogMessage($"Exception {ex.ToString()}");
            }
        }
        delegate int delGetCallstack64(
            IntPtr pContext,
            int nSkipFrames,
            int nFrames,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] IntPtr[] frames,
            ref int nFramesWritten,
            ref UInt64 pHash
            );
        [DllImport("ntdll.dll")]
        public static extern int RtlCaptureStackBackTrace(
            int FramesToSkip,
            int FramesToCapture,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] IntPtr[] frames,
            ref ulong stackHash);

        [DllImport("kernel32", SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
