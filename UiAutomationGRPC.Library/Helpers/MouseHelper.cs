using System;
using UiAutomation;

namespace UiAutomationGRPC.Library.Helpers
{
    /// <summary>
    /// Helper for simulating mouse input via gRPC.
    /// </summary>
    public static class MouseHelper
    {
        private static UiAutomationService.UiAutomationServiceClient _client;

        /// <summary>
        /// Initializes the mouse helper with a driver.
        /// </summary>
        /// <param name="driver">The driver instance.</param>
        public static void Init(UiAutomationDriver driver)
        {
            _client = driver.Client;
        }

        /// <summary>
        /// Moves the mouse cursor to the specified coordinates.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        public static void MoveMouseTo(int x, int y)
        {
            PerformAction(ActionType.Move, x.ToString(), y.ToString());
        }

        /// <summary>
        /// Simulates a right mouse button click.
        /// </summary>
        public static void ClickRightButton()
        {
            PerformAction(ActionType.RightClick);
        }

        /// <summary>
        /// Simulates a left mouse button click.
        /// </summary>
        public static void ClickLeftButton()
        {
            PerformAction(ActionType.LeftClick);
        }

        /// <summary>
        /// Simulates a middle mouse button click.
        /// </summary>
        public static void ClickMiddleButton()
        {
            PerformAction(ActionType.MouseMiddleClick);
        }

        /// <summary>
        /// Simulates pressing (holding down) the right mouse button.
        /// </summary>
        public static void PressRightButton()
        {
            PerformAction(ActionType.RightDown);
        }

        /// <summary>
        /// Simulates pressing (holding down) the left mouse button.
        /// </summary>
        public static void PressLeftButton()
        {
            PerformAction(ActionType.LeftDown);
        }

        /// <summary>
        /// Simulates release the right mouse button.
        /// </summary>
        public static void ReleaseRightButton()
        {
            PerformAction(ActionType.RightUp);
        }

        /// <summary>
        /// Simulates release the left mouse button.
        /// </summary>
        public static void ReleaseLeftButton()
        {
             PerformAction(ActionType.LeftUp);
        }

        /// <summary>
        /// Scrolls the mouse wheel.
        /// </summary>
        /// <param name="steps">The number of steps to scroll. Positive for up, negative for down.</param>
        public static void ScrollMouseWheel(int steps)
        {
            PerformAction(ActionType.MousWeelScroll, steps.ToString());
        }

        private static void PerformAction(ActionType action, params string[] args)
        {
            if (_client == null)
            {
                // Fallback or throw? For now, just log or ignore if not initialized, 
                // but ideally it should be initialized.
                throw new InvalidOperationException("MouseHelper not initialized. Call MouseHelper.Init(driver) first.");
            }

            var request = new PerformActionRequest
            {
                Action = action,
                RuntimeId = "" // Empty for global actions
            };
            
            if (args != null)
            {
                request.Arguments.AddRange(args);
            }

            try
            {
                // We use async call but block for simplicity in helper usage, similar to KeyboardHelper approach
                _client.PerformActionAsync(request).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // Improve error handling/logging
                throw new Exception($"Failed to perform mouse action {action}: {ex.Message}", ex);
            }
        }
    }
}
