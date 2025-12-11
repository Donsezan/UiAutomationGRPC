using UiAutomationGRPC.Library.Helpers;
using System.Drawing;

namespace UiAutomationGRPC.Library.Elements
{
    /// <summary>
    /// Interface for automation elements.
    /// </summary>
    public interface IAutomationElement
    {
        /// <summary>
        /// Processes a click on an element.
        /// </summary>
        void Click();

        /// <summary>
        /// Processes a click on an element relative to Top Left coordinates.
        /// </summary>
        void Click(int x, int y);

        /// <summary>
        /// Processes a double click on an element.
        /// </summary>
        void DoubleClick();

        /// <summary>
        /// Processes a hover on an element.
        /// </summary>
        void Hover();

        /// <summary>
        /// Returns the Name field of the element.
        /// </summary>
        string Name();

        /// <summary>
        /// Returns the ClassName field of the element.
        /// </summary>
        string ClassName();

        /// <summary>
        /// Returns the AutomationId field of the element.
        /// </summary>
        string AutomationId();

        /// <summary>
        /// Waits until the element becomes clickable/interactable.
        /// </summary>
        void WaitForElementIsClickable();

        /// <summary>
        /// Waits until the element exists.
        /// </summary>
        void WaitForElementExist();

        /// <summary>
        /// Checks if the element exists.
        /// </summary>
        bool IsElementExist();

        /// <summary>
        /// Checks if the element is clickable.
        /// </summary>
        bool IsElementClicable();

        /// <summary>
        /// Checks if the element exists for a period of time.
        /// </summary>
        /// <param name="status">Expected status.</param>
        /// <param name="time">Time in seconds to wait.</param>
        bool WaitElementExistStatusForTime(bool status, int time);

        /// <summary>
        /// Checks if the element is clickable for a period of time.
        /// </summary>
        /// <param name="status">Expected status.</param>
        /// <param name="time">Time in seconds to wait.</param>
        bool WaitElementClickableStatusForTime(bool status, int time = UsabilityTimeLimits.ApplicationLoadLimit);

        /// <summary>
        /// Returns the rectangle of the element.
        /// </summary>
        Rectangle GetRectangle();

        /// <summary>
        /// Returns the runtime ID of the element.
        /// </summary>
        string GetRuntimeId();
    }
}
