using UiAutomationGRPC.Library.Helpers;
using System.Drawing;

namespace UiAutomationGRPC.Library
{
    /// <summary>
    /// Created by Nikita Anakolov  and Viktor Yakushenko :)
    /// </summary>
    public interface IAutomationElement
    {
        /// <summary>
        ///  This method processes a click on an element
        /// </summary>
        void Click();

        /// <summary>
        ///  This method processes a click on an element relotive to Top left coordinates
        /// </summary>
        void Click(int x, int y);

        /// <summary>
        /// This method processes a double click on an element
        /// </summary>
        void DoubleClick();

        /// <summary>
        ///  This method processes a Hover on an element
        /// </summary>
        void Hover();

        /// <summary>
        ///  This method return Name field of element
        /// </summary>
        string Name();

        /// <summary>
        ///  This method return ClassName field of element
        /// </summary>
        string ClassName();

        /// <summary>
        ///  This method return AutomationId field of element
        /// </summary>
        string AutomationId();

        /// <summary>
        ///  This method waits when elements become clickable, that's mean that we can interact with them
        /// </summary>
        void WaitForElementIsClickable();

        /// <summary>
        ///  This method waits when element become status exist
        /// </summary>
        void WaitForElementExist();

        /// <summary>
        ///  This method is check for element exists or not and return bool value
        /// </summary>
        bool IsElementExist();

        /// <summary>
        ///  This method is check is element clickable or not and return bool value
        /// </summary>
        bool IsElementClicable();

        /// <summary>
        ///  This method check is element exist or not for period of time
        /// </summary>
        /// <param name="status"></param>
        /// <param name="time"></param>
        bool WaitElementExistStatusForTime(bool status, int time);

        /// <summary>
        /// This method check is element clickable or not for period of time
        /// </summary>
        /// <param name="status"></param>
        /// <param name="time"></param>
        bool WaitElementClickableStatusForTime(bool status, int time = UsabilityTimeLimits.ApplicationLoadLimit);

        /// <summary>
        ///  This method return Rectangle values of element
        /// </summary>
        Rectangle GetRectangle();

        /// <summary>
        /// Returns the runtime ID of the element.
        /// </summary>
        string GetRuntimeId();
    }
}
