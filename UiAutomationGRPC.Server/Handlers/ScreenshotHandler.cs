using Grpc.Core;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using FlaUI.Core.AutomationElements;
using UiAutomation;
using UiAutomationGRPC.Server.Helpers;

namespace UiAutomationGRPC.Server.Handlers
{
    /// <summary>
    /// Handles screenshot capture operations.
    /// </summary>
    public class ScreenshotHandler
    {
        private readonly InteractionAccessGuard? _guard;

        public ScreenshotHandler(InteractionAccessGuard? guard = null)
        {
            _guard = guard;
        }
        public ScreenshotResponse TakeScreenshot(ScreenshotRequest request, ServerCallContext context)
        {
            Bitmap bmp = null;
            try
            {
                Rectangle captureRect = Rectangle.Empty;
                AutomationElement targetElement = null;

                if (request.Mode == ScreenshotMode.Element)
                {
                    if (string.IsNullOrEmpty(request.RuntimeId) || !ElementCache.TryGetLive(request.RuntimeId, out targetElement))
                    {
                        if (string.IsNullOrEmpty(request.RuntimeId))
                        {
                            // Fallback to primary screen
                            captureRect = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                            bmp = CaptureRegion(captureRect);
                        }
                        else
                        {
                            return new ScreenshotResponse { Success = false, Message = "Element not found or RuntimeId missing for ELEMENT mode." };
                        }
                    }
                    else
                    {
                        var rect = targetElement.BoundingRectangle;
                        if (rect.Width <= 0 || rect.Height <= 0) throw new Exception("Element has invalid dimensions.");

                        // Validate interaction access before capturing
                        var blocked = InteractionAccessGuard.CheckAccess(_guard, targetElement.Properties.ProcessId.ValueOrDefault);
                        if (blocked != null)
                            return new ScreenshotResponse { Success = false, Message = blocked };

                        captureRect = rect;
                        bmp = CaptureRegion(captureRect);
                    }
                }
                else if (request.Mode == ScreenshotMode.Window)
                {
                    if (!string.IsNullOrEmpty(request.RuntimeId) && ElementCache.TryGetLive(request.RuntimeId, out targetElement))
                    {
                        // Validate interaction access before capturing
                        var blockedW = InteractionAccessGuard.CheckAccess(_guard, targetElement.Properties.ProcessId.ValueOrDefault);
                        if (blockedW != null)
                            return new ScreenshotResponse { Success = false, Message = blockedW };

                        // Traverse up to find the window
                        var windowElement = GetTopLevelWindow(targetElement);
                        var wRect = windowElement.BoundingRectangle;
                        captureRect = wRect;
                        bmp = CaptureRegion(captureRect);

                        // Draw highlight
                        if (!targetElement.Equals(windowElement))
                        {
                            var elemRect = targetElement.BoundingRectangle;
                            using (var g = Graphics.FromImage(bmp))
                            using (var pen = new Pen(Color.Red, 3))
                            {
                                g.DrawRectangle(pen, elemRect.X - wRect.X, elemRect.Y - wRect.Y, elemRect.Width, elemRect.Height);
                            }
                        }
                    }
                    else if (request.ProcessId > 0)
                    {
                        var process = Process.GetProcessById(request.ProcessId);
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            var windowElement = UiaRuntime.Automation.FromHandle(process.MainWindowHandle);
                            captureRect = windowElement.BoundingRectangle;
                            bmp = CaptureRegion(captureRect);
                        }
                        else
                        {
                            return new ScreenshotResponse { Success = false, Message = "Process main window not found." };
                        }
                    }
                    else
                    {
                        captureRect = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                        bmp = CaptureRegion(captureRect);
                    }
                }

                if (bmp == null)
                {
                    return new ScreenshotResponse { Success = false, Message = "Failed to capture screenshot." };
                }

                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return new ScreenshotResponse
                    {
                        Success = true,
                        ImageData = Google.Protobuf.ByteString.CopyFrom(ms.ToArray()),
                        Message = "Screenshot taken."
                    };
                }
            }
            catch (Exception ex)
            {
                return new ScreenshotResponse { Success = false, Message = $"Error taking screenshot: {ex.Message}" };
            }
            finally
            {
                bmp?.Dispose();
            }
        }

        public static Bitmap CaptureRegion(Rectangle rect)
        {
            var bmp = new Bitmap(rect.Width, rect.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(rect.Location, Point.Empty, rect.Size);
            }
            return bmp;
        }

        public static AutomationElement GetTopLevelWindow(AutomationElement element)
        {
            var desktop = UiaRuntime.Desktop;
            var walker = UiaRuntime.Automation.TreeWalkerFactory.GetControlViewWalker();
            var current = element;
            while (current != null)
            {
                if (current.Equals(desktop)) return element;
                var parent = walker.GetParent(current);
                if (parent == null) return current;
                if (parent.Equals(desktop)) return current;
                current = parent;
            }
            return element;
        }
    }
}
