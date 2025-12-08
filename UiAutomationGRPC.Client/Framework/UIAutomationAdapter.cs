using UiAutomationGRPC.Client.Framework.Locators;
using CoreTest.Helpers;
using Grpc.Core.Logging;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using UiAutomationGRPC.Client.Framework;
using Point = System.Windows.Point;

namespace UiAutomationGRPC.Client
{
    public class UiAutomationAdapter : IAutomationElement
    {
        private readonly Func<AutomationElement> _automationElement;

        public UiAutomationAdapter(Func<BaseSelector> uiAutomationElement, bool external = true)
        {
            _automationElement = () => uiAutomationElement().Build(external);
        }

        public void Hover()
        {
            WaitForElementIsClickable();
            var point = _automationElement().GetClickablePoint();
            Cursor.Position = new System.Drawing.Point(Convert.ToInt32(point.X), Convert.ToInt32(point.Y));
            Logger.WriteLog(_automationElement(), "Element hovered: ");
            VirtualMouse.RightClick();
        }

        public string Name()
        {
            WaitForElementExist();
            if (_automationElement().Current.Name != null)
            {
                return _automationElement()?.Current.Name;
            }
            return string.Empty;
        }

        public string ClassName()
        {
            WaitForElementExist();
            if (_automationElement().Current.ClassName != null)
            {
                return _automationElement()?.Current.ClassName;
            }
            return string.Empty;
        }

        public string AutomationId()
        {
            WaitForElementExist();
            if (_automationElement().Current.AutomationId != null)
            {
                return _automationElement()?.Current.AutomationId;
            }
            return string.Empty;
        }

        public void Click()
        {
            WaitForElementIsClickable();
            var point = _automationElement().GetClickablePoint();
            Logger.WriteLog(_automationElement(), "Element clicked: ");
            Cursor.Position = new System.Drawing.Point(Convert.ToInt32(point.X), Convert.ToInt32(point.Y));
            VirtualMouse.LeftClick();
        }

        public void Click(int relativeX, int relativeY)
        {
            WaitForElementIsClickable();
            var bottomRightX = _automationElement().Current.BoundingRectangle.TopLeft.X;
            var bottomRightY = _automationElement().Current.BoundingRectangle.TopLeft.Y;
            var x = bottomRightX + relativeX;
            var y = bottomRightY + relativeY;
            Logger.WriteLog(_automationElement(), "Element clicked: ");
            Cursor.Position = new System.Drawing.Point(Convert.ToInt32(x), Convert.ToInt32(y));
            VirtualMouse.LeftClick();
        }

        public void DoubleClick()
        {
            WaitForElementIsClickable();
            var point = _automationElement().GetClickablePoint();
            Logger.WriteLog(_automationElement(), "Element clicked: ");
            Cursor.Position = new System.Drawing.Point(Convert.ToInt32(point.X), Convert.ToInt32(point.Y));
            VirtualMouse.LeftClick();
            Thread.Sleep(100);
            Cursor.Position = new System.Drawing.Point(Convert.ToInt32(point.X), Convert.ToInt32(point.Y));
            VirtualMouse.LeftClick();
        }

        public void WaitForElementExist()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var elementExistMsg = false;
            while (true)
            {
                if (stopWatch.Elapsed.TotalSeconds > UsabilityTimeLimits.ApplicationLoadLimit)
                {
                    stopWatch.Stop();
                    if (_automationElement != null)
                        throw new TimeoutException("Time is out. Element not exist: " + _automationElement.Method.Name);
                }
                try
                {
                    if (_automationElement != null && (_automationElement() != null && _automationElement().Current.IsEnabled))
                    {
                        Logger.WriteLog(_automationElement(), "Element exist: ");
                        stopWatch.Stop();
                        break;
                    }
                }
                catch
                {
                    if (_automationElement != null & !elementExistMsg)
                    {
                        Logger.WriteLog("Element not exist: " + _automationElement.Method.Name);
                        elementExistMsg = true;
                    }
                }
                Thread.Sleep(10);
            }
        }

        public void WaitForElementIsClickable()
        {
            WaitForElementExist();
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var elementClickableMsg = false;
            while (true)
            {
                if (stopWatch.Elapsed.TotalSeconds > UsabilityTimeLimits.ApplicationLoadLimit)
                {
                    stopWatch.Stop();
                    if (_automationElement != null)
                        throw new TimeoutException("Time is out. Element not exist: " + _automationElement.Method.Name);
                }
                try
                {
                    if (_automationElement != null && _automationElement() != null && _automationElement().Current.IsEnabled)
                    {

                        var clickable = _automationElement().TryGetClickablePoint(out Point pt);
                        if (clickable)
                        {
                            Logger.WriteLog(_automationElement(), "Element clickable: ");
                            break;
                        }
                    }
                }
                catch
                {
                    if (_automationElement != null & !elementClickableMsg)
                    {
                        Logger.WriteLog("Element not clickable: " + _automationElement.Method.Name);
                        elementClickableMsg = true;
                    }
                }

                Thread.Sleep(10);
            }
            stopWatch.Stop();
            MeasurementContext.TimeMilliseconds = stopWatch.ElapsedMilliseconds;
        }

        public bool IsElementExist()
        {
            bool state = false;
            try
            {
                if (_automationElement() != null && _automationElement().Current.IsEnabled)
                {
                    Logger.WriteLog(_automationElement(), "Element exist: ");
                    state = true;
                }
            }
            catch (Exception e)
            {
                Logger.WriteLog("Element not exist: " + _automationElement.Method.Name + e);
            }
            return state;
        }

        public bool IsElementClicable()
        {
            var state = false;
            try
            {
                if (_automationElement() != null && _automationElement().TryGetClickablePoint(out Point pt))
                {
                    Logger.WriteLog(_automationElement(), "Element clickable: ");
                    state = true;
                }
            }
            catch (Exception e)
            {
                Logger.WriteLog("Element not clickable: " + _automationElement.Method.Name + e);
            }

            return state;
        }

        public bool WaitElementExistStatusForTime(bool status, int time = UsabilityTimeLimits.ApplicationLoadLimit)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            while (true)
            {
                Thread.Sleep(UsabilityTimeLimits.ApplicationLoadLimit);
                var elementStatus = _automationElement() != null && _automationElement().Current.IsEnabled;
                if (elementStatus == status)
                {
                    return status;
                }
                if (stopWatch.Elapsed.TotalSeconds > time)
                {
                    stopWatch.Stop();
                    return !status;
                }
            }
        }

        public bool WaitElementClickableStatusForTime(bool status, int time = UsabilityTimeLimits.ApplicationLoadLimit)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            while (true)
            {
                Thread.Sleep(UsabilityTimeLimits.ApplicationLoadLimit);
                Point pt;
                var elementStatus = _automationElement() != null && _automationElement().TryGetClickablePoint(out pt);
                if (elementStatus == status)
                {
                    return status;
                }
                if (stopWatch.Elapsed.TotalSeconds > time)
                {
                    stopWatch.Stop();
                    return !status;
                }
            }
        }

        public Rectangle GetRectangle()
        {
            WaitForElementExist();
            var rectangle = _automationElement().Current.BoundingRectangle;
            return new Rectangle() { X = Convert.ToInt32(rectangle.X), Y = Convert.ToInt32(rectangle.Y), Width = Convert.ToInt32(rectangle.Width), Height = Convert.ToInt32(rectangle.Height) };
        }
    }
}
