//------------------------------------------------------------------------------
// <copyright file="MouseAutomationService.cs" company="Microsoft">
//  Copyright (c) Microsoft. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
namespace Microsoft.Test.Stress.Input
{
    using Microsoft.Test.Stress;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;

    /// <summary>
    /// Structure to represent a 2 dimensional point.
    /// </summary>
    [Serializable]
    public struct Point
    {
        /// <summary>
        /// Readonly static presenting a point at (0,0).
        /// </summary>
        private static readonly Point Zero = default(Point);

        /// <summary>
        /// X coordinate.
        /// </summary>
        private int x;

        /// <summary>
        /// Y coordinate.
        /// </summary>
        private int y;

        /// <summary>
        /// Initializes a new instance of the <see cref="Point"/> struct.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        public Point(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Point"/> struct.
        /// </summary>
        /// <param name="point">Point to copy x and y coordinate from.</param>
        public Point(Point point)
        {
            this.x = point.X;
            this.y = point.Y;
        }

        /// <summary>
        /// Gets a point initialized at 0,0.
        /// </summary>
        public static Point ZeroPoint
        {
            get
            {
                return Zero;
            }
        }

        /// <summary>
        /// Gets or sets the X coordinate.
        /// </summary>
        public int X
        {
            get
            {
                return this.x;
            }

            set
            {
                this.x = value;
            }
        }

        /// <summary>
        /// Gets or sets the Y coordinate.
        /// </summary>
        public int Y
        {
            get
            {
                return this.y;
            }

            set
            {
                this.y = value;
            }
        }

        /// <summary>
        /// Tests if the specified points have the same x and y coordinate.
        /// </summary>
        /// <param name="point1">First point to test.</param>
        /// <param name="point2">Second point to test.</param>
        /// <returns>True if both points have the same x and y coordinate, otherwise false.</returns>
        public static bool Equals(Point point1, Point point2)
        {
            return point1.X == point2.X && point1.Y == point2.Y;
        }

        /// <summary>
        /// Tests whether obj is a Point that has the same x and y coordinate as this Point.
        /// </summary>
        /// <param name="obj">Object to test.</param>
        /// <returns>True if both points have the same x and y coordinate, otherwise false.</returns>
        public override bool Equals(object obj)
        {
            if (!(obj is Point))
            {
                return false;
            }

            Point point = (Point)obj;
            return Point.Equals(this, point);
        }

        /// <summary>
        /// Returns the hash code for the point.
        /// </summary>
        /// <returns>Hash code.</returns>
        public override int GetHashCode()
        {
            return this.x ^ this.y;
        }

        /// <summary>
        /// Calculates the distance between this point and another point.
        /// </summary>
        /// <param name="point">Point to calculate distance to.</param>
        /// <returns>Distance between points.</returns>
        public double DistanceTo(Point point)
        {
            return Math.Sqrt(Math.Pow(point.X - this.X, 2) + Math.Pow(point.Y - this.Y, 2));
        }

        /// <summary>
        /// Returns a Point from a native point structure.
        /// </summary>
        /// <param name="point">Native point.</param>
        /// <returns>Managed point structure.</returns>
        internal static Point FromNativePoint(NativeMethods.POINT point)
        {
            return new Point(point.X, point.Y);
        }
    }
    /// <summary>
    /// Service for sending mouse input.
    /// </summary>
    internal sealed class MouseAutomationService 
    {
        /// <summary>
        /// The maximum distance between two points for which to calculate an animation time less than MaximumDefaultMouseMoveAnimationTime.
        /// </summary>
        private const int MaximumDistance = 500;

        /// <summary>
        /// A dictionary mapping Mousebutton to MOUSEINPUT structures.
        /// </summary>
        private static readonly Dictionary<MouseButton, MOUSEINPUT[]> MouseClickInputMap = new Dictionary<MouseButton, MOUSEINPUT[]>();

        /// <summary>
        /// The maximum duration possible for a mouse move animation.
        /// </summary>
        private static readonly TimeSpan MaximumDefaultMouseMoveAnimationTime = TimeSpan.FromSeconds(0.4);

        /// <summary>
        /// Time time between mouse events during a mouse move operation.
        /// </summary>
        private static readonly TimeSpan MouseMoveTickLength = TimeSpan.FromMilliseconds(15);

        /// <summary>
        /// Initializes a new instance of the <see cref="MouseAutomationService"/> class.
        /// </summary>
        public MouseAutomationService()
        {
            if (MouseClickInputMap.Count == 0)
            {
                BuildMouseButtonMap();
            }
        }

        /// <summary>
        /// Represents different mouse click event types.
        /// </summary>
        private enum MouseButtonPressType
        {
            /// <summary>
            /// MouseDown event.
            /// </summary>
            MouseDown = 0,

            /// <summary>
            /// Mouse up event.
            /// </summary>
            MouseUp = 1,
        }

        /// <summary>
        /// Gets the current location of the cursor in screen coordinates.
        /// </summary>
        public Point CursorLocation
        {
            get
            {
                NativeMethods.POINT location;
                NativeMethods.GetCursorPos(out location);
                return Point.FromNativePoint(location);
            }
        }

        /// <summary>
        /// Gets the duration between clicks for a double click.
        /// </summary>
        internal TimeSpan DoubleClickDuration
        {
            get
            {
                uint maximumDoubleClickTime = NativeMethods.GetDoubleClickTime();
                return TimeSpan.FromMilliseconds(maximumDoubleClickTime / 3); // One third of the time
            }
        }

        /// <summary>
        /// Gets the duration between mouse down and mouse up for a mouse click event.
        /// </summary>
        internal TimeSpan ClickDuration
        {
            get
            {
                uint maximumDoubleClickTime = NativeMethods.GetDoubleClickTime();
                return TimeSpan.FromMilliseconds(maximumDoubleClickTime / 6); // One sixth of the maximum time
            }
        }

        /// <summary>
        /// Moves the mouse cursor to the specified location.
        /// </summary>
        /// <param name="x">X coordinate in screen space.</param>
        /// <param name="y">Y coordinate in screen space.</param>
        public void MoveTo(int x, int y)
        {
            Point currentLocation = this.CursorLocation;
            MoveMouse(
                currentLocation.X,
                currentLocation.Y,
                x,
                y,
                GetTimeSpanFromDistance(currentLocation.X, currentLocation.Y, x, y));
        }

        /// <summary>
        /// Moves the mouse cursor to the specified location.
        /// </summary>
        /// <param name="point">Location to move to in screen space.</param>
        public void MoveTo(Point point)
        {
            this.MoveTo(point.X, point.Y);
        }

        /// <summary>
        /// Moves the mouse cursor to the specified locations.
        /// </summary>
        /// <param name="points">Locations to move to in screen space.</param>
        public void MoveTo(IEnumerable<Point> points)
        {
            foreach (Point point in points)
            {
                this.MoveTo(point);
            }
        }

        /// <summary>
        /// Sends a mouse button double click event.
        /// </summary>
        /// <param name="button">Mouse button.</param>
        public void DoubleClick(MouseButton button)
        {
            this.DoubleClick(button, this.CursorLocation.X, this.CursorLocation.Y);
        }

        /// <summary>
        /// Sends a left mouse button double click event.
        /// </summary>
        /// <param name="x">X coordinate in screen space.</param>
        /// <param name="y">Y coordinate in screen space.</param>
        public void DoubleClick(int x, int y)
        {
            this.DoubleClick(MouseButton.Left, x, y);
        }

        /// <summary>
        /// Sends a left mouse button double click event.
        /// </summary>
        /// <param name="point">Location to click in screen space.</param>
        public void DoubleClick(Point point)
        {
            this.DoubleClick(point.X, point.Y);
        }

        /// <summary>
        /// Sends a mouse button double click event.
        /// </summary>
        /// <param name="button">Mouse button.</param>
        /// <param name="x">X coordinate in screen space.</param>
        /// <param name="y">Y coordinate in screen space.</param>
        public void DoubleClick(MouseButton button, int x, int y)
        {
            this.MoveTo(x, y);
            this.SendMouseClick(button);
            Thread.Sleep(this.DoubleClickDuration); // TODO: Need to use "sleeper" service.
            this.SendMouseClick(button);
        }

        /// <summary>
        /// Sends a mouse button double click event.
        /// </summary>
        /// <param name="button">Mouse button.</param>
        /// <param name="point">Location to click in screen space.</param>
        public void DoubleClick(MouseButton button, Point point)
        {
            this.DoubleClick(button, point.X, point.Y);
        }

        /// <summary>
        /// Sends a mouse button click event.
        /// </summary>
        /// <param name="button">Mouse button.</param>
        public void Click(MouseButton button)
        {
            this.Click(button, this.CursorLocation.X, this.CursorLocation.Y);
        }

        /// <summary>
        /// Sends a left mouse button click event.
        /// </summary>
        /// <param name="x">X coordinate in screen space.</param>
        /// <param name="y">Y coordinate in screen space.</param>
        public void Click(int x, int y)
        {
            this.Click(MouseButton.Left, x, y);
        }

        /// <summary>
        /// Sends a left mouse button click event.
        /// </summary>
        /// <param name="point">Location to click in screen space.</param>
        public void Click(Point point)
        {
            this.Click(point.X, point.Y);
        }

        /// <summary>
        /// Sends a mouse button click event.
        /// </summary>
        /// <param name="button">Mouse button.</param>
        /// <param name="x">X coordinate in screen space.</param>
        /// <param name="y">Y coordinate in screen space.</param>
        public void Click(MouseButton button, int x, int y)
        {
            this.MoveTo(x, y);
            this.SendMouseClick(button);
        }

        /// <summary>
        /// Sends a mouse button click event.
        /// </summary>
        /// <param name="button">Mouse button.</param>
        /// <param name="point">Location to click in screen space.</param>
        public void Click(MouseButton button, Point point)
        {
            this.Click(button, point.X, point.Y);
        }

        /// <summary>
        /// Sends a left mouse button down event, moves the cursors between the specified
        /// points and releases the mouse button.
        /// </summary>
        /// <param name="fromX">Starting X coordinate in screen space.</param>
        /// <param name="fromY">Starting Y coordinate in screen space.</param>
        /// <param name="toX">Ending X coordinate in screen space.</param>
        /// <param name="toY">Ending Y coordinate in screen space.</param>
        public void ClickDrag(int fromX, int fromY, int toX, int toY)
        {
            this.ClickDrag(MouseButton.Left, fromX, fromY, toX, toY);
        }

        /// <summary>
        /// Sends a left mouse button down event, moves the cursors to the specified
        /// point and releases the mouse button.
        /// </summary>
        /// <param name="toX">Ending X coordinate in screen space.</param>
        /// <param name="toY">Ending Y coordinate in screen space.</param>
        public void ClickDrag(int toX, int toY)
        {
            this.ClickDrag(this.CursorLocation.X, this.CursorLocation.Y, toX, toY);
        }

        /// <summary>
        /// Sends a left mouse button down event, moves the cursors to the specified
        /// point and releases the mouse button.
        /// </summary>
        /// <param name="button">Mouse button to press.</param>
        /// <param name="toX">Ending X coordinate in screen space.</param>
        /// <param name="toY">Ending Y coordinate in screen space.</param>
        public void ClickDrag(MouseButton button, int toX, int toY)
        {
            this.ClickDrag(button, this.CursorLocation.X, this.CursorLocation.Y, toX, toY);
        }

        /// <summary>
        /// Sends a left mouse button down event, moves the cursors between the specified
        /// points and releases the mouse button.
        /// </summary>
        /// <param name="from">Starting location in screen space.</param>
        /// <param name="to">Ending location in screen space.</param>
        public void ClickDrag(Point from, Point to)
        {
            this.ClickDrag(from.X, from.Y, to.X, to.Y);
        }

        /// <summary>
        /// Sends a left mouse button down event, moves the cursors to the specified
        /// points and releases the mouse button at the final point.
        /// </summary>
        /// <param name="from">Starting point.</param>
        /// <param name="path">Drag path.</param>
        public void ClickDrag(Point from, IEnumerable<Point> path)
        {
            this.ClickDrag(MouseButton.Left, from, path);
        }

        /// <summary>
        /// Sends a left mouse button down event, moves the cursors to the specified
        /// point and releases the mouse button.
        /// </summary>
        /// <param name="toPoint">Location to drag click to in screen space.</param>
        public void ClickDrag(Point toPoint)
        {
            this.ClickDrag(toPoint.X, toPoint.Y);
        }

        /// <summary>
        /// Sends a left mouse button down event, moves the cursors to the specified
        /// point and releases the mouse button.
        /// </summary>
        /// <param name="button">Mouse button to press.</param>
        /// <param name="toPoint">Location to drag click to in screen space.</param>
        public void ClickDrag(MouseButton button, Point toPoint)
        {
            this.ClickDrag(button, toPoint.X, toPoint.Y);
        }

        /// <summary>
        /// Sends a mouse button down event, moves the cursors to the specified
        /// points and releases the mouse button at the final point.
        /// </summary>
        /// <param name="button">Mouse button.</param>
        /// <param name="from">Starting point.</param>
        /// <param name="path">Drag path.</param>
        public void ClickDrag(MouseButton button, Point from, IEnumerable<Point> path)
        {
            this.MoveTo(from);
            using (this.GetMouseButtonPressHandle(button))
            {
                this.MoveTo(path);
            }
        }

        /// <summary>
        /// Sends a mouse button down event, moves the cursors between the specified
        /// points and releases the mouse button.
        /// </summary>
        /// <param name="button">Mouse button.</param>
        /// <param name="fromX">Starting X coordinate in screen space.</param>
        /// <param name="fromY">Starting Y coordinate in screen space.</param>
        /// <param name="toX">Ending X coordinate in screen space.</param>
        /// <param name="toY">Ending Y coordinate in screen space.</param>
        public void ClickDrag(MouseButton button, int fromX, int fromY, int toX, int toY)
        {
            this.MoveTo(fromX, fromY);
            using (this.GetMouseButtonPressHandle(button))
            {
                this.MoveTo(toX, toY);
            }
        }

        /// <summary>
        /// Sends a mouse button down event, moves the cursors between the specified
        /// points and releases the mouse button.
        /// </summary>
        /// <param name="button">Mouse button.</param>
        /// <param name="from">Starting location in screen space.</param>
        /// <param name="to">Ending location in screen space.</param>
        public void ClickDrag(MouseButton button, Point from, Point to)
        {
            this.ClickDrag(button, from.X, from.Y, to.X, to.Y);
        }

        /// <summary>
        /// Gets a disposable handle that repsents the lifedown of a mouse down event and
        /// a mouse up event called on disposal.
        /// </summary>
        /// <remarks>
        /// Call this method within a using block.
        /// </remarks>
        /// <param name="button">Mouse button.</param>
        /// <returns>Mouse button press handle.</returns>
        public IDisposable GetMouseButtonPressHandle(MouseButton button)
        {
            return new MouseButtonPressHandle(this, button);
        }

        /// <summary>
        /// Gets a MOUSEINPUT structure for the specified button and press type.
        /// </summary>
        /// <param name="button">Mouse button.</param>
        /// <param name="press">Mouse press type.</param>
        /// <returns>MOUSEINPUT structure.</returns>
        private static MOUSEINPUT GetMouseInputFromButton(MouseButton button, MouseButtonPressType press)
        {
            if (MouseClickInputMap.ContainsKey(button))
            {
                return MouseClickInputMap[button][(int)press];
            }

            string message = string.Format(
                CultureInfo.InvariantCulture,
                "Mouse button '{0}' is not supported",
                button);

            throw new NotSupportedException(message);
        }

        /// <summary>
        /// Moves the mouse from the specified start position to the specified end position
        /// for the specified amount of time.
        /// </summary>
        /// <param name="fromX">Starting X coordinate in screen space.</param>
        /// <param name="fromY">Starting Y coordinate in screen space.</param>
        /// <param name="toX">Ending X coordinate in screen space.</param>
        /// <param name="toY">Ending Y coordinate in screen space.</param>
        /// <param name="duration">Duration of mouse move.</param>
        private static void MoveMouse(int fromX, int fromY, int toX, int toY, TimeSpan duration)
        {
            // Note: Style cop made me do this.
            Animate(
                duration,
                (double progress) =>
                {
                    int newX = InterpolateInt32(fromX, toX, progress);
                    int newY = InterpolateInt32(fromY, toY, progress);
                    NativeMethods.SetCursorPos(newX, newY);
                });

            // Finally ensure we are on the correct point
            NativeMethods.SetCursorPos(toX, toY);
        }

        /// <summary>
        /// Gets the duration of a mouse move event for the specified points.
        /// </summary>
        /// <param name="fromX">Starting X coordinate in screen space.</param>
        /// <param name="fromY">Starting Y coordinate in screen space.</param>
        /// <param name="toX">Ending X coordinate in screen space.</param>
        /// <param name="toY">Ending Y coordinate in screen space.</param>
        /// <returns>Mouse move duration.</returns>
        private static TimeSpan GetTimeSpanFromDistance(int fromX, int fromY, int toX, int toY)
        {
            double distance = new Point(fromX, fromY).DistanceTo(new Point(toX, toY));

            if (distance == 0)
            {
                return TimeSpan.Zero;
            }

            // If the distance is > MaximumDistance just use MaximumDefaultInputTime otherwise, calculate it as a fraction
            // of the maximum time.
            TimeSpan timeToMove = MaximumDefaultMouseMoveAnimationTime;

            if (distance < MaximumDistance)
            {
                double maximumTimeToMoveMiliseconds = MaximumDefaultMouseMoveAnimationTime.TotalMilliseconds;
                double targetMiliseconds = (maximumTimeToMoveMiliseconds / 500.0) * distance;
                timeToMove = TimeSpan.FromMilliseconds(targetMiliseconds);
            }

            return timeToMove;
        }

        /// <summary>
        /// Calls the specified Action delegate for the specified amount of time with a value
        /// indicating progress from 0 to 1 for the "animation".
        /// </summary>
        /// <param name="duration">Duration of animation.</param>
        /// <param name="callBack">Callback delegate.</param>
        private static void Animate(TimeSpan duration, Action<double> callBack)
        {
            double progress = 0;  // Tick is between 0..1
            double endTicks = (DateTime.UtcNow + duration).Ticks;
            double startTicks = DateTime.UtcNow.Ticks;
            double targetTicks = endTicks - startTicks;
            double nowTicks = 0;

            // While there is still work to be done...
            while (progress < 1.0)
            {
                // Ensure we don't go over the maximum value
                nowTicks = nowTicks < endTicks ? DateTime.UtcNow.Ticks : endTicks;

                // Animates using Sin to have "deceleration" when reaching the target point.
                progress = Math.Sin((Math.PI / 2) * ((nowTicks - startTicks) / targetTicks));

                // Ensure we don't send a value to the callback which is > 1.0
                progress = Math.Min(progress, 1.0);

                // Invoke the call back.
                callBack(progress);

                // Sleep for MouseMoveTickLength
                Thread.Sleep(MouseMoveTickLength);
            }
        }

        /// <summary>
        /// Interpolates between two integers.
        /// </summary>
        /// <param name="from">Original start integer.</param>
        /// <param name="to">Target integer value.</param>
        /// <param name="progress">Progress (between 0 and 1).</param>
        /// <returns>Interoplated value.</returns>
        private static int InterpolateInt32(int from, int to, double progress)
        {
            if (progress == 0.0)
            {
                return from;
            }
            else if (progress == 1.0)
            {
                return to;
            }

            double value = to - from;
            value *= progress;
            value += value > 0.0 ? 0.5 : -0.5;
            return from + (int)value;
        }

        #region Enum to MOUSEINPUT map building

        /// <summary>
        /// Builds the map dictionary of MouseButton enum to MOUSEINPUT structures.
        /// </summary>
        private static void BuildMouseButtonMap()
        {
            MOUSEINPUT[] leftInputs = new MOUSEINPUT[]
                {
                    MOUSEINPUT.FromFlags(NativeMethods.MOUSEEVENTF_LEFTDOWN),
                    MOUSEINPUT.FromFlags(NativeMethods.MOUSEEVENTF_LEFTUP),
                };

            MouseClickInputMap.Add(MouseButton.Left, leftInputs);

            MOUSEINPUT[] rightInputs = new MOUSEINPUT[]
                {
                    MOUSEINPUT.FromFlags(NativeMethods.MOUSEEVENTF_RIGHTDOWN),
                    MOUSEINPUT.FromFlags(NativeMethods.MOUSEEVENTF_RIGHTUP),
                };

            MouseClickInputMap.Add(MouseButton.Right, rightInputs);

            MOUSEINPUT[] middleInputs = new MOUSEINPUT[]
                {
                    MOUSEINPUT.FromFlags(NativeMethods.MOUSEEVENTF_MIDDLEDOWN),
                    MOUSEINPUT.FromFlags(NativeMethods.MOUSEEVENTF_MIDDLEUP),
                };

            MouseClickInputMap.Add(MouseButton.Middle, middleInputs);
        }
        #endregion

        /// <summary>
        /// Sends a mouse click event for the specified button.
        /// </summary>
        /// <param name="button">Mouse button.</param>
        private void SendMouseClick(MouseButton button)
        {
            this.SendMouseDown(button);
            Thread.Sleep(this.ClickDuration);
            this.SendMouseUp(button);
        }

        /// <summary>
        /// Sends a mouse down event for the specified button.
        /// </summary>
        /// <param name="button">Mouse button.</param>
        private void SendMouseDown(MouseButton button)
        {
            NativeMethods.SendInput(
                GetMouseInputFromButton(button, MouseButtonPressType.MouseDown));
        }

        /// <summary>
        /// Sends a mouse up event for the specified button.
        /// </summary>
        /// <param name="button">Mouse button.</param>
        private void SendMouseUp(MouseButton button)
        {
            NativeMethods.SendInput(
               GetMouseInputFromButton(button, MouseButtonPressType.MouseUp));
        }

        /// <summary>
        /// Wraps Mouse down and mouse up into a disposable type to be used within a using block.
        /// </summary>
        private class MouseButtonPressHandle : IDisposable
        {
            /// <summary>
            /// Mouse automation service.
            /// </summary>
            private MouseAutomationService service = null;

            /// <summary>
            /// Button to press and release.
            /// </summary>
            private MouseButton button = MouseButton.Left;

            /// <summary>
            /// Initializes a new instance of the <see cref="MouseButtonPressHandle"/> class.
            /// </summary>
            /// <param name="service">MouseInputService for sending mouse down / up events.</param>
            /// <param name="button">Button to press.</param>
            internal MouseButtonPressHandle(MouseAutomationService service, MouseButton button)
            {
                this.service = service;
                this.button = button;
                this.service.SendMouseDown(this.button);
            }

            /// <summary>
            /// Disposes of the handle.
            /// </summary>
            public void Dispose()
            {
                this.service.SendMouseUp(this.button);
            }
        }
        /// <summary>
        /// Represents mouse buttons.
        /// </summary>
        public enum MouseButton
        {
            /// <summary>
            /// Left mouse button.
            /// </summary>
            Left,

            /// <summary>
            /// Right mouse button.
            /// </summary>
            Right,

            /// <summary>
            /// Middle mouse button.
            /// </summary>
            Middle,
        }
    }
}
