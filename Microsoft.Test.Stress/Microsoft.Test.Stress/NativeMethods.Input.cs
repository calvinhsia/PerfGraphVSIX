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