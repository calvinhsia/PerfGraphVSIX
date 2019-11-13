using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LeakTestDatacollector
{
    //see http://msdn.microsoft.com/en-us/library/ms228772.aspx How to: Fix 'Application is Busy' and 'Call was Rejected By Callee' Errors
    [ComImport(), Guid("00000016-0000-0000-C000-000000000046"), InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IOleMessageFilter
    {
        [PreserveSig]
        int HandleInComingCall(int callType, IntPtr taskCallerHandle, int tickCount, /* LPINTERFACEINFO */ IntPtr interfaceInfo);

        [PreserveSig]
        int RetryRejectedCall(IntPtr taskCalleeHandle, int tickCount, int rejectType);

        [PreserveSig]
        int MessagePending(IntPtr taskCalleeHandle, int tickCount, int pendingType);
    }
    /// <summary>
    /// When a request is sent to VS, VS can respond with a RPC_E_REJECTED exception to indicate that it is busy. 
    /// This message causes automation to fail. This class filters messages so that an RPC_E_REJECTED exception will
    /// not cause the test to fail - instead, this class will intercept the message and tell VS to rehandle the 
    /// request that was rejected after a specified timeout. 
    /// </summary>
    /// <history>
    ///     [bebrinck]  4/4/2005    Ported from TAO2. 
    ///     [leish]   6/14/2009     deprecate this class, use Maui built-in message filter instead
    ///     [joboles] 2/23/2010     Class re-instated, to remove Maui dependency
    /// </history>
    public class MessageFilter : IOleMessageFilter
    {
        private const int timeToWaitBetweenRecalls = 500; //300

        /// <summary>
        /// This method is called by VS
        /// </summary>
        /// <param name="callType"></param>
        /// <param name="taskCallerHandle"></param>
        /// <param name="tickCount"></param>
        /// <param name="interfaceInfo"></param>
        /// <returns></returns>
        /// <history>
        ///     [bebrinck]  4/4/2005    Ported from TAO2. 
        /// </history>
        public int HandleInComingCall(int callType, System.IntPtr taskCallerHandle, int tickCount, System.IntPtr interfaceInfo)
        {
            //MemSpectTestBase.LogString((new StackTrace()).GetFrames()[0].GetMethod().Name + " tcount={0}", tickCount); // show the current method name
            return 0; //SERVERCALL_ISHANDLED
        }

        /// <summary>
        /// This method is called by VS
        /// </summary>
        /// <param name="taskCalleeHandle"></param>
        /// <param name="tickCount"></param>
        /// <param name="rejectType"></param>
        /// <returns></returns>
        /// <history>
        ///     [bebrinck]  4/4/2005    Ported from TAO2. 
        /// </history>
        public int RetryRejectedCall(System.IntPtr taskCalleeHandle, int tickCount, int rejectType)
        {
            //            MemSpectTestBase.LogString((new StackTrace()).GetFrames()[0].GetMethod().Name + " RejectType={0} tcount={1}", rejectType, tickCount); // show the current method name
            if (rejectType == 2 //SERVERCALL_RETRYLATER
                || rejectType == 1) //SERVERCALL_REJECTED
            {
                return timeToWaitBetweenRecalls; //timeToWaitBetweenRecalls;
            }

            return -1; //cancel call
        }

        /// <summary>
        /// This method is called by VS
        /// </summary>
        /// <param name="taskCalleeHandle"></param>
        /// <param name="tickCount"></param>
        /// <param name="pendingType"></param>
        /// <returns></returns>
        /// <history>
        ///     [bebrinck]  4/4/2005    Ported from TAO2. 
        /// </history>
        public int MessagePending(System.IntPtr taskCalleeHandle, int tickCount, int pendingType)
        {
            //MemSpectTestBase.LogString((new StackTrace()).GetFrames()[0].GetMethod().Name + " PendingType = {0}  TickCount ={1}", pendingType, tickCount); // show the current method name
            return 2; //PENDINGMSG_WAITDEFPROCESS 
        }

        [System.Runtime.InteropServices.DllImportAttribute("ole32.dll")]
        private static extern int CoRegisterMessageFilter(IOleMessageFilter filter, IOleMessageFilter[] oldFilter);

        /// <summary>
        /// This method registers the message filter with VS. Code that wants to use a MessageFilter
        /// should call this method as soon as VS has been created. 
        /// </summary>
        /// <history>
        ///     [bebrinck]  4/4/2005    Ported from TAO2. 
        /// </history>
        public static void RegisterMessageFilter()
        {
            //MemSpectTestBase.LogString((new StackTrace()).GetFrames()[0].GetMethod().Name); // show the current method name
            System.Threading.Thread.CurrentThread.SetApartmentState(System.Threading.ApartmentState.STA);

            MessageFilter filter = new MessageFilter();
            IOleMessageFilter[] oldFilter = null;
            _ = CoRegisterMessageFilter((IOleMessageFilter)filter, oldFilter);
        }

        /// <summary>
        /// This method revokes the message filter. Code that wants to use a MessageFilter
        /// should call this after VS has been shut down. 
        /// </summary>
        /// <history>
        ///     [bebrinck]  4/4/2005    Ported from TAO2. 
        /// </history>
        public static void RevokeMessageFilter()
        {
            IOleMessageFilter[] oldfilt = null;
            // MemSpectTestBase.LogString((new StackTrace()).GetFrames()[0].GetMethod().Name); // show the current method name

            CoRegisterMessageFilter(null, oldfilt);
        }
    }
}
