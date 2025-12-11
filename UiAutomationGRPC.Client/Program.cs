using System;
using System.Threading.Tasks;
using UiAutomationGRPC.Client.Calc.Pages;
using UiAutomationGRPC.Library;
using System.IO;

namespace UiAutomationGRPC.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Connecting to server...");
            
            // UiAutomationDriver: The main entry point for interacting with the gRPC server.
            // It manages the connection and provides methods for app lifecycle as well as creating elements.
            using (var driver = new UiAutomationDriver("127.0.0.1:50051"))
            {
                try 
                {
                    // 1. Open the Application
                    int processId = await OpenApplication(driver);
                    if (processId == 0) return;

                    // Allow some time for app to fully start
                    await Task.Delay(2000);

                    // 2. Perform Interactions using Page Object Model
                    await PerformCalculatorInteractions(driver);

                    // 3. Take Screenshots
                    await TakeScreenshots(driver);

                    // 4. Manage App Lifecycle (Close)
                    await ManageApplicationLifecycle(driver, processId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Global Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Opens the Calculator application.
        /// </summary>
        private static async Task<int> OpenApplication(UiAutomationDriver driver)
        {
            Console.WriteLine("Opening Calculator...");
            try
            {
                var openResult = await driver.OpenApp("calc");
                if (!openResult.Success)
                {
                    Console.WriteLine($"Failed to open app: {openResult.Message}");
                    return 0;
                }
                Console.WriteLine($"App opened with Process ID: {openResult.ProcessId}");
                return openResult.ProcessId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening app: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Demonstrates interaction using the Page Object Model and Selectors.
        /// 
        /// BUILDING SELECTOR PATHS:
        /// Selectors are used to locate elements in the UI tree. They can be built using a fluent API 
        /// or by passing PropertyConditions to navigation methods.
        /// 
        /// 1. Start with a Root Element:
        ///    Usually, you define a top-level window selector.
        ///    var Window = new Selector(new PropertyConditions().NameProperty("Calculator"));
        /// 
        /// 2. Navigate to Descendants:
        ///    Use .Descendants() or .Children() to traverse the tree.
        /// 
        /// 3. Add Conditions (Filters):
        ///    You can specify conditions to match specific elements.
        ///    
        ///    Example A (Fluent API):
        ///       Window.Descendants().ControlType("Button").NameContain("Close")
        ///       - This finds a descendant of 'Window' that is a Button AND has "Close" in its name.
        /// 
        ///    Example B (PropertyConditions Object):
        ///       Window.Descendants(new PropertyConditions().AutomationIdProperty("num2Button"))
        ///       - This finds a descendant with the exact Automation ID "num2Button".
        /// 
        /// 4. Create the Element:
        ///    Once the selector path is defined, create a UiElement (IAutomationElement) with the driver.
        ///    new UiElement(driver, () => Window.Descendants(...));
        /// </summary>
        private static async Task PerformCalculatorInteractions(UiAutomationDriver driver)
        {
            Console.WriteLine("Waiting for interactions...");
            
            // CalcPage internally uses CalcPageLocators where selectors are defined.
            var calcPage = new CalcPage(driver);

            // A. Click Interactions
            try
            {
                Console.WriteLine("Performing Click interactions...");
                calcPage
                    .ClickTwo()
                    .ClickPlus()
                    .ClickTwo()
                    .ClickEqual();

                var resultName = calcPage.GetResult();
                Console.WriteLine($"Click Result: {resultName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Click Interaction Error: {ex.Message}");
            }

            await Task.Delay(1000);

            // B. Keyboard Interactions
            try
            {
                Console.WriteLine("Performing Keyboard interactions...");
                // Sending "2+2=" via keyboard simulation
                // Note: {ADD} might be required for the plus key depending on the backend implementation,
                // or simply "+" if the regular key is sufficient.
                calcPage.SendKey("2");
                calcPage.SendKey("{ADD}"); 
                calcPage.SendKey("2");
                calcPage.SendKey("=");

                await Task.Delay(1000); // Wait for calculation

                var resultName = calcPage.GetResult();
                Console.WriteLine($"Keyboard Result Name: {resultName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Keyboard Interaction Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Demonstrates taking screenshots of specific elements or the entire window.
        /// </summary>
        private static async Task TakeScreenshots(UiAutomationDriver driver)
        {
            try
            {
                Console.WriteLine("Taking screenshots...");
                var locators = new CalcPageLocators(driver);
                var btn2 = locators.ButtonTwo;
                
                // Get Runtime ID to identify the specific element for the screenshot service
                string btnId = btn2.GetRuntimeId();
                Console.WriteLine($"Button Two Runtime ID: {btnId}");
                
                // 1. Element Screenshot
                var screen1 = await driver.TakeElementScreenshot(btnId);
                if (screen1.Success)
                {
                    File.WriteAllBytes("btn_two.png", screen1.ImageData);
                    Console.WriteLine("Saved btn_two.png");
                }
                else
                {
                    Console.WriteLine($"Error taking element screenshot: {screen1.Message}");
                }

                // 2. Window Screenshot (Highlighting element)
                // This captures the window but draws a highlight box around the specified element.
                var screen2 = await driver.TakeWindowScreenshot(btnId);
                if (screen2.Success)
                {
                    File.WriteAllBytes("window_highlight.png", screen2.ImageData);
                    Console.WriteLine("Saved window_highlight.png");
                }
                else
                {
                    Console.WriteLine($"Error taking window screenshot (highlight): {screen2.Message}");
                }
                
                // 3. Full Screen Screenshot
                var screenFull = await driver.TakeWindowScreenshot(null, null);
                if (screenFull.Success)
                {
                    File.WriteAllBytes("full_screen.png", screenFull.ImageData);
                    Console.WriteLine("Saved full_screen.png");
                }
                else
                {
                    Console.WriteLine($"Error taking full screen screenshot: {screenFull.Message}");
                }
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Screenshot Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Demonstrates closing applications by Name or Process ID.
        /// </summary>
        private static async Task ManageApplicationLifecycle(UiAutomationDriver driver, int processId)
        {
            // Example A: Close app by Name
            try
            {
                // Note: The name here usually refers to the window title or process name alias depending on server logic.
                // In many cases, closing by Process ID is more reliable.
                var closeResult = driver.CloseApp("CalculatorApp");
                if (!closeResult.Success) 
                    Console.WriteLine($"CloseApp (by name) Error: {closeResult.Message}");
                else 
                    Console.WriteLine($"CloseApp (by name): {closeResult.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Close app Error: {ex.Message}");
            }

            /*
             * Disabled due to calc changing process IDs on each launch.
             * 
            // Example B: Open and Close by PID
            // We open a new instance just to close it by PID.
            try
            {
                Console.WriteLine("Testing CloseAppByProcessId...");
                var openResult2 = await driver.OpenApp("calc");
                if (openResult2.Success)
                {
                    Console.WriteLine($"Opened calc (PID: {openResult2.ProcessId}) for PID close test.");
                    await Task.Delay(1000);
                    var closePidResult = driver.CloseAppByProcessId(openResult2.ProcessId);
                    if (closePidResult.Success)
                        Console.WriteLine("Successfully closed by PID.");
                    else
                        Console.WriteLine($"Failed to close by PID: {closePidResult.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CloseAppByProcessId Error: {ex.Message}");
            }
            */
        }
    }
}
