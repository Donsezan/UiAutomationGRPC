using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Provides virtual mouse operations for UI automation.
    /// Consolidates all mouse-related P/Invoke calls and operations.
    /// </summary>
    public static class VirtualMouse
    {
        #region P/Invoke Declarations

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        private const int MOUSEEVENTF_MOVE = 0x0001;
        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const int MOUSEEVENTF_RIGHTUP = 0x0010;
        private const int MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const int MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const int MOUSEEVENTF_WHEEL = 0x0800;
        private const int MOUSEEVENTF_ABSOLUTE = 0x8000;

        #endregion

        #region Cursor Movement

        /// <summary>
        /// Moves the cursor by a relative delta.
        /// </summary>
        public static void Move(int xDelta, int yDelta)
        {
            mouse_event(MOUSEEVENTF_MOVE, xDelta, yDelta, 0, 0);
        }

        /// <summary>
        /// Moves the cursor to an absolute screen position using P/Invoke.
        /// </summary>
        public static void MoveTo(int x, int y)
        {
            SetCursorPos(x, y);
        }

        #endregion

        #region Left Click Operations

        /// <summary>
        /// Performs a left click at the current cursor position.
        /// </summary>
        public static void LeftClick()
        {
            LeftDown();
            LeftUp();
        }

        /// <summary>
        /// Presses the left mouse button down.
        /// </summary>
        public static void LeftDown()
        {
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        }

        /// <summary>
        /// Releases the left mouse button.
        /// </summary>
        public static void LeftUp()
        {
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        }

        /// <summary>
        /// Moves the cursor to the specified position and performs a left click.
        /// </summary>
        public static void LeftClickAt(int x, int y)
        {
            SetCursorPos(x, y);
            LeftClick();
        }

        /// <summary>
        /// Moves the cursor and performs a left click (legacy alias).
        /// </summary>
        public static void Click(int x, int y)
        {
            LeftClickAt(x, y);
        }

        #endregion

        #region Right Click Operations

        /// <summary>
        /// Performs a right click at the current cursor position.
        /// </summary>
        public static void RightClick()
        {
            RightDown();
            RightUp();
        }

        /// <summary>
        /// Presses the right mouse button down.
        /// </summary>
        public static void RightDown()
        {
            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
        }

        /// <summary>
        /// Releases the right mouse button.
        /// </summary>
        public static void RightUp()
        {
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
        }

        /// <summary>
        /// Moves the cursor to the specified position and performs a right click.
        /// </summary>
        public static void RightClickAt(int x, int y)
        {
            SetCursorPos(x, y);
            RightClick();
        }

        #endregion

        #region Double Click Operations

        /// <summary>
        /// Performs a double left click at the current cursor position.
        /// </summary>
        public static void DoubleClick()
        {
            LeftClick();
            Thread.Sleep(50);
            LeftClick();
        }

        /// <summary>
        /// Moves the cursor to the specified position and performs a double click.
        /// </summary>
        public static void DoubleClickAt(int x, int y)
        {
            SetCursorPos(x, y);
            DoubleClick();
        }

        #endregion

        #region Middle Click and Scroll

        /// <summary>
        /// Performs a middle mouse button click at the current cursor position.
        /// </summary>
        public static void MiddleClick()
        {
            mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
        }

        /// <summary>
        /// Legacy alias for MiddleClick.
        /// </summary>
        public static void MouseMiddleClick()
        {
            MiddleClick();
        }

        /// <summary>
        /// Scrolls the mouse wheel.
        /// Positive value = forward (away from user), negative = backward (toward user).
        /// </summary>
        public static void Scroll(int wheelDelta)
        {
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, wheelDelta, 0);
        }

        /// <summary>
        /// Scrolls by number of steps (each step is 120 wheel units).
        /// </summary>
        public static void ScrollSteps(int steps)
        {
            Scroll(steps * 120);
        }

        /// <summary>
        /// Legacy alias for Scroll.
        /// </summary>
        public static void MousWeelScroll(int position)
        {
            Scroll(position);
        }

        #endregion
    }
}
