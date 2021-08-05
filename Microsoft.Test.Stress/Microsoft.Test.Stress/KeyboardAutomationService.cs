//------------------------------------------------------------------------------
// <copyright file="KeyboardAutomationService.cs" company="Microsoft">
//  Copyright (c) Microsoft. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
namespace Microsoft.Test.Stress
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// Implementation of IKeyboardAutomationService, for simulating keyboard input.
    /// </summary>
    //[Export(typeof(IKeyboardAutomationService))]
    //[Export(typeof(IRemotableService))]
    //[ProvidesOperationsExtension]
    public class KeyboardAutomationService //: MarshallableApexService, IRemotableService, IKeyboardAutomationService
    {
        /// <summary>
        /// Maximum key strokes to dispatch per minute.
        /// </summary>
        private const int KeyStrokesPerMinute = 2000;

        /// <summary>
        /// One minute in miliseconds.
        /// </summary>
        private const int OneMinuteInMilliseconds = 60000;

        /// <summary>
        /// Maximum time to sleep between each key stroke dispatch.
        /// </summary>
        private const int KeyPressSleepTimeInMilliseconds = OneMinuteInMilliseconds / KeyStrokesPerMinute;

        /// <summary>
        /// Tracks the last time keyboard input was dispatched.
        /// </summary>
        private DateTime lastDispatchTime = DateTime.UtcNow;


        /// <summary>
        /// Simulates input that holds the specified key down.
        /// </summary>
        /// <remarks>The key is released (key up event) when the handle is disposed.</remarks>
        /// <param name="key">Key to hold down.</param>
        /// <returns>Disposable handle that will release the key when Dispose is called.</returns>
        public IDisposable HoldKey(KeyboardKey key)
        {
            return new KeyDownHandle(this, new KeyboardKey[] { key });
        }

        /// <summary>
        /// Simulates input that holds the specified keys down.
        /// </summary>
        /// <remarks>The keys are released (key up event) when the handle is disposed.</remarks>
        /// <param name="keys">Keys to hold down.</param>
        /// <returns>Disposable handle that will release the keys when Dispose is called.</returns>
        public IDisposable HoldKeys(params KeyboardKey[] keys)
        {
            return new KeyDownHandle(this, keys);
        }

        /// <summary>
        /// Simulates input that holds the specified keys down.
        /// </summary>
        /// <remarks>The keys are released (key up event) when the handle is disposed.</remarks>
        /// <param name="keys">Keys to hold down.</param>
        /// <returns>Disposable handle that will release the keys when Dispose is called.</returns>
        public IDisposable HoldKeys(IEnumerable<KeyboardKey> keys)
        {
            return new KeyDownHandle(this, keys);
        }

        /// <summary>
        /// Simulates input that types the specified character.
        /// </summary>
        /// <remarks>Note: this is different to TypeKey, TypeCharacter will support any unicode character via sending the input as a scan code.</remarks>
        /// <param name="character">Character to type.</param>
        public void TypeCharacter(char character)
        {
            this.Dispatch(CreateCharacterKeyPressInput(character));
        }

        /// <summary>
        /// Simulates input that types the specified keyboard key.
        /// </summary>
        /// <param name="key">Key to type.</param>
        public void TypeKey(KeyboardKey key)
        {
            this.Dispatch(CreateVirtualKeyPressInput(key));
        }

        /// <summary>
        /// Simulates input that types the specified keyboard key.
        /// </summary>
        /// <param name="modifiers">Key modifiers to hold down whilst typing.</param>
        /// <param name="key">Key to type.</param>
        public void TypeKey(KeyboardModifier modifiers, KeyboardKey key)
        {
            this.TypeKey(modifiers, new KeyboardKey[] { key });
        }

        /// <summary>
        /// Simulates input that types the specified keyboard keys.
        /// </summary>
        /// <param name="modifiers">Key modifiers to hold down whilst typing.</param>
        /// <param name="keys">Keys to type.</param>
        public void TypeKey(KeyboardModifier modifiers, params KeyboardKey[] keys)
        {
            using (this.HoldKeys(KeysFromModifiers(modifiers)))
            {
                foreach (KeyboardKey key in keys)
                {
                    this.TypeKey(key);
                }
            }
        }

        /// <summary>
        /// Simulates input that types the specified characters.
        /// </summary>
        /// <param name="modifiers">Key modifiers to hold down whilst typing.</param>
        /// <param name="characters">Characters to type.</param>
        public void TypeKey(KeyboardModifier modifiers, params char[] characters)
        {
            using (this.HoldKeys(KeysFromModifiers(modifiers)))
            {
                foreach (char character in characters)
                {
                    this.TypeKey(character);
                }
            }
        }

        /// <summary>
        /// Simulates input that types the specified character.
        /// </summary>
        /// <param name="modifiers">Key modifiers to hold down whilst typing.</param>
        /// <param name="character">Character to type.</param>
        public void TypeKey(KeyboardModifier modifiers, char character)
        {
            this.TypeKey(modifiers, new char[] { character });
        }

        /// <summary>
        /// Simulates input that types the specified character.
        /// </summary>
        /// <param name="character">Character to type.</param>
        /// <remarks>Note: this is different to TypeCharacter, input will be sent as keyboard input (e.g. pressing the 'A' key on the keyboard).</remarks>
        public void TypeKey(char character)
        {
            // TODO: This is less than ideal, need better long term solution.
            // Note: Convert lower case a-z characters to thier uppcace counterpark, the uppercase
            // values of A-Z represent the keyboard key that maps to the character.
            character = character > 'Z' && character <= 'z' ? (char)(character - 32) : character;
            this.Dispatch(CreateVirtualKeyPressInput((ushort)character));
        }

        /// <summary>
        /// Simulates input that types the specified string of characters.
        /// </summary>
        /// <param name="text">String to type.</param>
        public void TypeText(string text)
        {
            foreach (var inputPair in text.Select(key => CreateCharacterKeyPressInput(key)))
            {
                this.Dispatch(inputPair);
            }
        }

        /// <summary>
        /// Converts <c>KeyboardModifier</c> values into an enumerable set of <c>KeyboardKey</c> values.
        /// </summary>
        /// <param name="modifier">Keyboard modifiers to convert.</param>
        /// <returns>Enumerable set of KeyboardKeys.</returns>
        private static IEnumerable<KeyboardKey> KeysFromModifiers(KeyboardModifier modifier)
        {
            if (modifier.HasFlag(KeyboardModifier.Alt))
            {
                yield return KeyboardKey.Alt;
            }

            if (modifier.HasFlag(KeyboardModifier.Control))
            {
                yield return KeyboardKey.Control;
            }

            if (modifier.HasFlag(KeyboardModifier.Shift))
            {
                yield return KeyboardKey.Shift;
            }
        }

        /// <summary>
        /// Creates a KEYBDINPUT struct array that represents a key press.
        /// </summary>
        /// <param name="key">Key to input.</param>
        /// <returns>Array of KEYBDINPUT structs.</returns>
        private static KEYBDINPUT[] CreateVirtualKeyPressInput(KeyboardKey key)
        {
            return CreateVirtualKeyPressInput((ushort)key);
        }

        /// <summary>
        /// Creates a KEYBDINPUT struct array that represents a key press.
        /// </summary>
        /// <param name="key">Key to input.</param>
        /// <returns>Array of KEYBDINPUT structs.</returns>
        private static KEYBDINPUT[] CreateVirtualKeyPressInput(ushort key)
        {
            return new KEYBDINPUT[]
            {
                CreateVirtualKeyInput(key, false),
                CreateVirtualKeyInput(key, true),
            };
        }

        /// <summary>
        /// Creates a KEYBDINPUT struct array that represents a key press.
        /// </summary>
        /// <param name="character">Character to input.</param>
        /// <returns>Array of KEYBDINPUT structs.</returns>
        private static KEYBDINPUT[] CreateCharacterKeyPressInput(char character)
        {
            return new KEYBDINPUT[]
            {
                CreateCharacterKeyInput(character, false),
                CreateCharacterKeyInput(character, true),
            };
        }

        /// <summary>
        /// Creates a KEYBDINPUT struct for key code input.
        /// </summary>
        /// <param name="key">Key to input.</param>
        /// <param name="keyUp"><c>true</c> if the input should be a KeyUp event, otherwise <c>false</c>.</param>
        /// <returns>KEYBDINPUT struct.</returns>
        private static KEYBDINPUT CreateVirtualKeyInput(KeyboardKey key, bool keyUp)
        {
            return CreateVirtualKeyInput((ushort)key, keyUp);
        }

        /// <summary>
        /// Creates a KEYBDINPUT struct for key code input.
        /// </summary>
        /// <param name="key">Key to input.</param>
        /// <param name="keyUp"><c>true</c> if the input should be a KeyUp event, otherwise <c>false</c>.</param>
        /// <returns>KEYBDINPUT struct.</returns>
        private static KEYBDINPUT CreateVirtualKeyInput(ushort key, bool keyUp)
        {
            return CreateKeyInput(keyCode: (ushort)key, flags: keyUp ? KeyboardFlag.KeyUp : KeyboardFlag.None);
        }

        /// <summary>
        /// Creates a KEYBDINPUT struct for character input.
        /// </summary>
        /// <param name="character">Character to input.</param>
        /// <param name="keyUp"><c>true</c> if the input should be a KeyUp event, otherwise <c>false</c>.</param>
        /// <returns>KEYBDINPUT struct.</returns>
        private static KEYBDINPUT CreateCharacterKeyInput(char character, bool keyUp)
        {
            const uint ExtendedCharacterMask = 0xFF00;
            const uint ExtendedCharacterHeader = 0xE000;

            KEYBDINPUT input = CreateKeyInput(scan: character, flags: KeyboardFlag.Unicode);

            if (keyUp)
            {
                input.Flags |= (uint)KeyboardFlag.KeyUp;
            }

            // Any character that has a value that starts with 0xE0 (greater than 254) needs to be sent as an extended key.
            if (((uint)character & ExtendedCharacterMask) == ExtendedCharacterHeader)
            {
                input.Flags |= (uint)KeyboardFlag.ExtendedKey;
            }

            return input;
        }

        /// <summary>
        /// Creates a KEYBDINPUT struct.
        /// </summary>
        /// <param name="scan">Scan code for input.</param>
        /// <param name="keyCode">Key code for input.</param>
        /// <param name="flags">Keyboard flags.</param>
        /// <returns>KEYBDINPUT struct.</returns>
        private static KEYBDINPUT CreateKeyInput(ushort scan = 0, ushort keyCode = 0, KeyboardFlag flags = KeyboardFlag.None)
        {
            return new KEYBDINPUT()
            {
                KeyCode = keyCode,
                Scan = scan,
                Time = 0,
                Flags = (uint)flags,
                ExtraInfo = IntPtr.Zero,
            };
        }

        /// <summary>
        /// Dispatches the specified keyboard input with SendInput.
        /// </summary>
        /// <param name="input">Keyboard input to dispatch.</param>
        private void Dispatch(KEYBDINPUT[] input)
        {
            TimeSpan delta = DateTime.UtcNow - this.lastDispatchTime;
            int sleepTime = KeyPressSleepTimeInMilliseconds - (int)delta.TotalMilliseconds;
            if (sleepTime > 0)
            {
                Thread.Sleep(sleepTime);
            }

            NativeMethods.SendInput(input);
            this.lastDispatchTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Disposable handle used by the HoldKey method.
        /// </summary>
        private class KeyDownHandle : IDisposable
        {
            /// <summary>
            /// Keys to hold down.
            /// </summary>
            private readonly IEnumerable<KeyboardKey> keys;

            /// <summary>
            /// KeyboardAutomationService reference.
            /// </summary>
            private readonly KeyboardAutomationService keyboard;

            /// <summary>
            /// Initializes a new instance of the <see cref="KeyDownHandle"/> class.
            /// </summary>
            /// <param name="keyboard">Keyboard automation service.</param>
            /// <param name="keys">Keys to hold down.</param>
            public KeyDownHandle(KeyboardAutomationService keyboard, IEnumerable<KeyboardKey> keys)
            {
                this.keyboard = keyboard;
                this.keys = keys;

                // Hold down keys.
                this.keyboard.Dispatch(this.keys.Select(key => CreateVirtualKeyInput(key, false)).ToArray());
            }

            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            public void Dispose()
            {
                // Release keys.
                this.keyboard.Dispatch(this.keys.Select(key => CreateVirtualKeyInput(key, true)).ToArray());
            }
        }
    }
    /// <summary>
    /// Represents physical keyboard keys. 
    /// </summary>
    public enum KeyboardKey
    {
        Backspace = 0x08,
        Tab = 0x09,
        Clear = 0x0C,
        Return = 0x0D,
        Enter = 0x0D,
        Shift = 0x10,
        Control = 0x11,
        Menu = 0x12,
        Alt = 0x12,
        CapsLock = 0x14,
        Escape = 0x1B,
        Space = 0x20,
        PageUp = 0x21,
        PageDown = 0x22,
        End = 0x23,
        Home = 0x24,
        Left = 0x25,
        Up = 0x26,
        Right = 0x27,
        Down = 0x28,
        Insert = 0x2D,
        Delete = 0x2E,
        Help = 0x2F,
        LeftWindows = 0x5B,
        RightWindows = 0x5C,
        Apps = 0x5D,
        ContextMenu = 0x5D,
        F1 = 0x70,
        F2 = 0x71,
        F3 = 0x72,
        F4 = 0x73,
        F5 = 0x74,
        F6 = 0x75,
        F7 = 0x76,
        F8 = 0x77,
        F9 = 0x78,
        F10 = 0x79,
        F11 = 0x7A,
        F12 = 0x7B,
        F13 = 0x7C,
        F14 = 0x7D,
        F15 = 0x7E,
        F16 = 0x7F,
        F17 = 0x80,
        F18 = 0x81,
        F19 = 0x82,
        F20 = 0x83,
        F21 = 0x84,
        F22 = 0x85,
        F23 = 0x86,
        F24 = 0x87,
        Oem1 = 0xBA,
        OemPlus = 0xBB,
        OemComma = 0xBC,
        OemMinus = 0xBD,
        OemPeriod = 0xBE,
        Oem2 = 0xBF,
        Oem3 = 0xC0,
        OemGrave = 0xC0,
    }
    public enum KeyboardModifier
    {
        /// <summary>
        /// No keyboard modifiers.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Control (CTRL) Key.
        /// </summary>
        Control = 0x01,

        /// <summary>
        /// Alt (Menu) key.
        /// </summary>
        Alt = 0x02,

        /// <summary>
        /// Shift key.
        /// </summary>
        Shift = 0x04,
    }


}
