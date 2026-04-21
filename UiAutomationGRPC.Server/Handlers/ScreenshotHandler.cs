using Grpc.Core;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Automation;
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
        public Task<ScreenshotResponse> TakeScreenshot(ScreenshotRequest request, ServerCallContext context)
        {
            Bitmap bmp = null;
            try
            {
                System.Drawing.Rectangle captureRect = System.Drawing.Rectangle.Empty;
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
                            throw new RpcException(new Status(StatusCode.NotFound, "Element not found or RuntimeId missing for ELEMENT mode."));
                        }
                    }
                    else
                    {
                        var rect = targetElement.Current.BoundingRectangle;
                        if (rect.Width <= 0 || rect.Height <= 0) throw new Exception("Element has invalid dimensions.");

                        // Validate interaction access before capturing
                        var blocked = InteractionAccessGuard.CheckAccess(_guard, targetElement.Current.ProcessId);
                        if (blocked != null)
                            return Task.FromResult(new ScreenshotResponse { Success = false, Message = blocked });

                        captureRect = new System.Drawing.Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                        bmp = CaptureRegion(captureRect);
                    }
                }
                else if (request.Mode == ScreenshotMode.Window)
                {
                    if (!string.IsNullOrEmpty(request.RuntimeId) && ElementCache.TryGetLive(request.RuntimeId, out targetElement))
                    {
                        // Validate interaction access before capturing
                        var blockedW = InteractionAccessGuard.CheckAccess(_guard, targetElement.Current.ProcessId);
                        if (blockedW != null)
                            return Task.FromResult(new ScreenshotResponse { Success = false, Message = blockedW });

                        // Traverse up to find the window
                        var windowElement = GetTopLevelWindow(targetElement);
                        var wRect = windowElement.Current.BoundingRectangle;
                        captureRect = new System.Drawing.Rectangle((int)wRect.X, (int)wRect.Y, (int)wRect.Width, (int)wRect.Height);
                        bmp = CaptureRegion(captureRect);

                        // Draw highlight
                        if (targetElement != windowElement) 
                        {
                            var elemRect = targetElement.Current.BoundingRectangle;
                            using (var g = Graphics.FromImage(bmp))
                            using (var pen = new Pen(Color.Red, 3))
                            {
                                g.DrawRectangle(pen, (int)(elemRect.X - wRect.X), (int)(elemRect.Y - wRect.Y), (int)elemRect.Width, (int)elemRect.Height);
                            }
                        }
                    }
                    else if (request.ProcessId > 0)
                    {
                        var process = Process.GetProcessById(request.ProcessId);
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            var windowElement = AutomationElement.FromHandle(process.MainWindowHandle);
                            var wRect = windowElement.Current.BoundingRectangle;
                            captureRect = new System.Drawing.Rectangle((int)wRect.X, (int)wRect.Y, (int)wRect.Width, (int)wRect.Height);
                            bmp = CaptureRegion(captureRect);
                        }
                        else
                        {
                            throw new RpcException(new Status(StatusCode.NotFound, "Process main window not found."));
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
                    throw new RpcException(new Status(StatusCode.Internal, "Failed to capture screenshot."));
                }

                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return Task.FromResult(new ScreenshotResponse 
                    { 
                        Success = true, 
                        ImageData = Google.Protobuf.ByteString.CopyFrom(ms.ToArray()), 
                        Message = "Screenshot taken."
                    });
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ScreenshotResponse { Success = false, Message = $"Error taking screenshot: {ex.Message}" });
            }
            finally
            {
                bmp?.Dispose();
            }
        }

        public static Bitmap CaptureRegion(System.Drawing.Rectangle rect)
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
            var walker = TreeWalker.ControlViewWalker;
            var current = element;
            while (current != null)
            {
                if (current == AutomationElement.RootElement) return element;
                var parent = walker.GetParent(current);
                if (parent == AutomationElement.RootElement) return current;
                current = parent;
            }
            return element;
        }
    }
}
