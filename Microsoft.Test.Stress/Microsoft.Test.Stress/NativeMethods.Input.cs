﻿//-----------------------------------------------------------------------
// <auto-generated>
//  Do not style cop native and interop methods and interfaces
// </auto-generated>
//-----------------------------------------------------------------------
namespace Microsoft.Test.Stress
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Linq;

    //Native methods used by MouseInput class
    public static partial class MouseInput
    {
        //ref bing:"GetSystemMetrics Function(Windows)"
        //Virtual screen = bounding area of all monitors, or 'Desktop' area
        const int SM_XVIRTUALSCREEN = 76; //X of 'virtual screen'
        const int SM_YVIRTUALSCREEN = 77; //Y of 'virtual screen'
        const int SM_CXVIRTUALSCREEN = 78; //Width of 'virtual screen'
        const int SM_CYVIRTUALSCREEN = 79; //Height of 'virtual screen'

        //ref bing:"INPUT Structure site:msdn.microsoft.com"
        const int INPUT_MOUSE = 0;
        const int INPUT_KEYBOARD = 1;

        //ref bing:"INPUT Structure site:msdn.microsoft.com"
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public int type; //INPUT_MOUSE or INPUT_KEYBOARD
            public INPUTUNION union;
        };

        //ref bing:"INPUT Structure site:msdn.microsoft.com"
        [StructLayout(LayoutKind.Explicit)]
        struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mouseInput;
            [FieldOffset(0)]
            public KEYBDINPUT keyboardInput;
        };

        [StructLayout(LayoutKind.Sequential)]
        //ref bing:"KEYBDINPUT Structure site:msdn.microsoft.com"
        struct KEYBDINPUT
        {
            public short wVk;
            public short wScan;
            public int dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        };

        [StructLayout(LayoutKind.Sequential)]
        //ref bing:"MOUSEINPUT Structure site:msdn.microsoft.com"
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public int dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        };

        //ref bing:"MOUSEINPUT Structure site:msdn.microsoft.com"
        const int XBUTTON1 = 0x0001;
        const int XBUTTON2 = 0x0002;
        const int WHEEL_DELTA = 120;

        [Flags]
        //ref bing:"MOUSEINPUT Structure site:msdn.microsoft.com"
        enum MOUSEEVENTF
        {
            MOVE = 0x0001,
            LEFTDOWN = 0x0002,
            LEFTUP = 0x0004,
            RIGHTDOWN = 0x0008,
            RIGHTUP = 0x0010,
            MIDDLEDOWN = 0x0020,
            MIDDLEUP = 0x0040,
            XDOWN = 0x0080, ///Requires Win2k and up
            XUP = 0x0100, ///Requires Win2k and up
            WHEEL = 0x0800, ///Requires WinNT and up
            HORIZONTALWHEEL = 0x1000, ///Requires Vista and up
            MOVE_NOCOALESCE = 0x2000, ///Requires Vista and up
            VIRTUALDESK = 0x4000, ///MUST be used in combination with Absolute
            ABSOLUTE = 0x8000,
        };

        //ref bing:"GetSystemMetrics Function(Windows)"
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        static extern int GetSystemMetrics(int nIndex);

        //ref bing:"SendInput Function(Windows)"
        [DllImport("user32.dll", SetLastError = true)]
        static extern int SendInput(int nInputs, ref INPUT mi, int cbSize);
    }



    #region Structures
    /// <summary>
    /// The type of the input event. 
    /// </summary>
    internal enum InputType : uint
    {
        Mouse = 0,
        Keyboard = 1,
        Hardware = 2,
    }

    /// <summary>
    /// Used by SendInput to store information for synthesizing input events such as keystrokes, mouse movement, and mouse clicks.
    /// </summary>
    internal struct INPUT
    {
        public UInt32 Type;
        public MOUSEKEYBDHARDWAREINPUT Data;

        public INPUT(MOUSEINPUT mouseInput)
        {
            this.Data = new MOUSEKEYBDHARDWAREINPUT();
            this.Data.Mouse = mouseInput;
            this.Type = (uint)InputType.Mouse;
        }

        public INPUT(KEYBDINPUT keyboardInput)
        {
            this.Data = new MOUSEKEYBDHARDWAREINPUT();
            this.Data.Keyboard = keyboardInput;
            this.Type = (uint)InputType.Keyboard;
        }
    }

    /// <summary>
    /// Aggregate of the different input types. (Wrapper).
    /// </summary>
    /// <remarks>Input.Data can be set to either MOUSEINPUT, KEYBDINPUT or HARDWAREINPUT each starting at the same memory location.</remarks>
    [StructLayout(LayoutKind.Explicit)]
    internal struct MOUSEKEYBDHARDWAREINPUT
    {
        [FieldOffset(0)]
        public MOUSEINPUT Mouse;

        [FieldOffset(0)]
        public KEYBDINPUT Keyboard;

        [FieldOffset(0)]
        public HARDWAREINPUT Hardware;
    }

    /// <summary>
    /// Contains information about a simulated keyboard event.
    /// </summary>
    internal struct KEYBDINPUT
    {
#pragma warning disable 649
        public ushort KeyCode;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
#pragma warning restore 649
    }

    /// <summary>
    /// Contains information about a simulated message generated by an input device other than a keyboard or mouse.
    /// </summary>
    internal struct HARDWAREINPUT
    {
#pragma warning disable 649
        public uint Msg;
        public ushort ParamL;
        public ushort ParamH;
#pragma warning restore 649
    }

    /// <summary>
    /// Keyboard input flags. 
    /// </summary>
    [Flags]
    internal enum KeyboardFlag : uint
    {
        None = 0x0000,
        ExtendedKey = 0x0001,
        KeyUp = 0x0002,
        Unicode = 0x0004,
        ScanCode = 0x0008,
    }

    /// <summary>
    /// Contains information about a simulated mouse event.
    /// </summary>
    internal struct MOUSEINPUT
    {
        public static MOUSEINPUT FromFlags(uint flags)
        {
            return new MOUSEINPUT()
            {
                Flags = flags
            };
        }
#pragma warning disable 649
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
#pragma warning restore 649
    }
    #endregion

    internal static partial class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern uint GetDoubleClickTime();

        [DllImport("user32.dll")]
        internal static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        internal static void SendInput(MOUSEINPUT mouseInput)
        {
            SendInput(new INPUT(mouseInput));
        }

        internal static void SendInput(KEYBDINPUT keyboardInput)
        {
            SendInput(new INPUT(keyboardInput));
        }

        internal static void SendInput(KEYBDINPUT[] keyboardInput)
        {
            SendInput(keyboardInput.Select(keyInput => new INPUT(keyInput)).ToArray());
        }

        internal static void SendInput(INPUT input)
        {
            SendInput(new INPUT[] { input });
        }

        internal static void SendInput(INPUT[] input)
        {
            uint result = 0;
            if ((result = NativeMethods.SendInput((uint)input.Length, input, Marshal.SizeOf(input[0]))) != input.Length)
            {
                throw new Win32Exception();
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
        }

        internal const uint XBUTTON1 = 0x0001;
        internal const uint XBUTTON2 = 0x0002;
        internal const uint MOUSEEVENTF_MOVE = 0x0001;
        internal const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        internal const uint MOUSEEVENTF_LEFTUP = 0x0004;
        internal const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        internal const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        internal const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        internal const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        internal const uint MOUSEEVENTF_XDOWN = 0x0080;
        internal const uint MOUSEEVENTF_XUP = 0x0100;
        internal const uint MOUSEEVENTF_WHEEL = 0x0800;
        internal const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
        internal const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        [DllImport("user32.dll")]
        internal static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetMessageExtraInfo();

    }
}
