using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UiAutomationGRPC.Server.Helpers
{
    public class VirtualMouse
    {
        [DllImport("user32.dll")]
        static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
        private const int MOUSEEVENTF_MOVE = 0x0001;
        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const int MOUSEEVENTF_RIGHTUP = 0x0010;
        private const int MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const int MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const int MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const int MOUSEEVENTF_WHEEL = 0x0800;

        public static void Move(int xDelta, int yDelta)
        {
            mouse_event(MOUSEEVENTF_MOVE, xDelta, yDelta, 0, 0);
        }

        public static void MoveTo(int x, int y)
        {

            mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE, x, y, 0, 0);
        }
        public static void LeftClick()
        {
            LeftDown();
            LeftUp();
        }

        public static void LeftDown()
        {
            mouse_event(MOUSEEVENTF_LEFTDOWN, Control.MousePosition.X, Control.MousePosition.Y, 0, 0);
        }

        public static void LeftUp()
        {
            mouse_event(MOUSEEVENTF_LEFTUP, Control.MousePosition.X, Control.MousePosition.Y, 0, 0);
        }

        public static void RightClick()
        {
            RightDown();
            RightUp();
        }

        public static void RightDown()
        {
            mouse_event(MOUSEEVENTF_RIGHTDOWN, Control.MousePosition.X, Control.MousePosition.Y, 0, 0);
        }

        public static void RightUp()
        {
            mouse_event(MOUSEEVENTF_RIGHTUP, Control.MousePosition.X, Control.MousePosition.Y, 0, 0);
        }

        public static void MousWeelScroll(int position)
        {
            //A positive value indicates that the wheel was rotated forward, away from the user; 
            //A negative value indicates that the wheel was rotated backward, toward the user. 
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, position, 0);
        }

        public static void MouseMiddleClick()
        {
            mouse_event(MOUSEEVENTF_MIDDLEDOWN, Control.MousePosition.X, Control.MousePosition.Y, 0, 0);
            mouse_event(MOUSEEVENTF_MIDDLEUP, Control.MousePosition.X, Control.MousePosition.Y, 0, 0);
        }

        public static void Click(int x, int y)
        {
            var point = new Point(x, y);
            Cursor.Position = new System.Drawing.Point(Convert.ToInt32(point.X), Convert.ToInt32(point.Y));
            LeftClick();
        }
    }
}
