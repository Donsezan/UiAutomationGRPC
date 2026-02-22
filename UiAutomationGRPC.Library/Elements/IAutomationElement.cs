using System.Drawing;
using UiAutomationGRPC.Library.Helpers;

namespace UiAutomationGRPC.Library.Elements
{
    /// <summary>
    /// Interface for automation elements.
    /// All methods are async-only because every operation involves a gRPC network call
    /// to the server. Async prevents thread-blocking during network I/O and avoids
    /// deadlocks when consumed from UI threads or synchronization-context-bound callers.
    /// </summary>
    public interface IAutomationElement
    {
        /// <summary>
        /// Processes a click on an element.
        /// </summary>
        Task ClickAsync();

        /// <summary>
        /// Processes a click at specific screen coordinates (moves cursor to x,y then clicks).
        /// </summary>
        Task ClickAsync(int x, int y);

        /// <summary>
        /// Processes a double click on an element.
        /// </summary>
        Task DoubleClickAsync();

        /// <summary>
        /// Processes a hover on an element.
        /// </summary>
        Task HoverAsync();

        /// <summary>
        /// Returns the Name field of the element.
        /// </summary>
        Task<string> NameAsync();

        /// <summary>
        /// Returns the ClassName field of the element.
        /// </summary>
        Task<string> ClassNameAsync();

        /// <summary>
        /// Returns the AutomationId field of the element.
        /// </summary>
        Task<string> AutomationIdAsync();

        /// <summary>
        /// Waits until the element becomes clickable/interactable.
        /// </summary>
        Task WaitForElementIsClickableAsync();

        /// <summary>
        /// Waits until the element exists.
        /// </summary>
        Task WaitForElementExistAsync();

        /// <summary>
        /// Checks if the element exists.
        /// </summary>
        Task<bool> IsElementExistAsync();

        /// <summary>
        /// Checks if the element is clickable.
        /// </summary>
        Task<bool> IsElementClickableAsync();

        /// <summary>
        /// Checks if the element exists for a period of time.
        /// </summary>
        /// <param name="status">Expected status.</param>
        /// <param name="time">Time in seconds to wait.</param>
        Task<bool> WaitElementExistStatusForTimeAsync(bool status, int time);

        /// <summary>
        /// Checks if the element is clickable for a period of time.
        /// </summary>
        /// <param name="status">Expected status.</param>
        /// <param name="time">Time in seconds to wait.</param>
        Task<bool> WaitElementClickableStatusForTimeAsync(bool status, int time = UsabilityTimeLimits.ApplicationLoadLimit);

        /// <summary>
        /// Returns the rectangle of the element.
        /// </summary>
        Task<Rectangle> GetRectangleAsync();

        /// <summary>
        /// Returns the runtime ID of the element.
        /// </summary>
        Task<string> GetRuntimeIdAsync();
    }
}
