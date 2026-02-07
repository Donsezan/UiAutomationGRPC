using UiAutomationGRPC.Client.Calc.Pages;
using UiAutomationGRPC.Library;
using UiAutomationGRPC.Library.Helpers;

namespace UiAutomationGRPC.Client;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Connecting to server...");
        
        // UiAutomationDriver: The main entry point for interacting with the gRPC server.
        // It manages the connection and provides methods for app lifecycle as well as creating elements.
        await using var driver = new UiAutomationDriver("http://127.0.0.1:50051", insecureMode: true);
        
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

    /// <summary>
    /// Opens the Calculator application.
    /// </summary>
    private static async Task<int> OpenApplication(UiAutomationDriver driver)
    {
        Console.WriteLine("Opening Calculator...");
        try
        {
            var (success, message, processId) = await driver.OpenAppAsync("calc");
            if (!success)
            {
                Console.WriteLine($"Failed to open app: {message}");
                return 0;
            }
            Console.WriteLine($"App opened with Process ID: {processId}");
            return processId;
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

        // B. Keyboard Interactions using VirtualKeyboard
        try
        {
            Console.WriteLine("Performing Keyboard interactions...");
            var keyboard = new VirtualKeyboard(driver);
            
            // Sending "2+2=" via keyboard simulation
            await keyboard.SendKeyAsync("2");
            await keyboard.SendKeyAsync("{ADD}"); 
            await keyboard.SendKeyAsync("2");
            await keyboard.SendKeyAsync("=");

            await Task.Delay(1000); // Wait for calculation

            var resultName = calcPage.GetResult();
            Console.WriteLine($"Keyboard Result Name: {resultName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Keyboard Interaction Error: {ex.Message}");
        }

        // C. Mouse Interactions (Global) using VirtualMouse
        try
        {
            Console.WriteLine("Performing Global Mouse interactions...");
            
            // Initialize the VirtualMouse with the driver
            var mouse = new VirtualMouse(driver);

            // Scenario: 2 + 2 = 4 using coordinates
            // 1. Find elements, get their bounds, move to center, click.
            
            // Re-using locators:
            var locators = new CalcPageLocators(driver);
            
            // Helper to perform the global click sequence
            async Task ClickElementGlobally(UiAutomationGRPC.Library.Elements.IAutomationElement element, string desc)
            {
                try 
                {
                    var rect = element.GetRectangle();
                    int centerX = rect.Left + rect.Width / 2;
                    int centerY = rect.Top + rect.Height / 2;
                    
                    Console.WriteLine($"Moving to {desc} at ({centerX}, {centerY})...");
                    // Move to element and click
                    await mouse.MoveAsync(centerX, centerY);
                    await Task.Delay(200);
                    await mouse.LeftClickAsync();
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to click {desc}: {ex.Message}");
                }
            }

            await ClickElementGlobally(locators.ButtonTwo, "Button Two");
            await ClickElementGlobally(locators.ButtonPlus, "Button Plus");
            await ClickElementGlobally(locators.ButtonTwo, "Button Two");
            await ClickElementGlobally(locators.ButtonEqual, "Button Equal");

            await Task.Delay(1000);
            var resultName = calcPage.GetResult();
            Console.WriteLine($"Global Mouse Result: {resultName}");

            Console.WriteLine("Mouse interactions completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Mouse Interaction Error: {ex.Message}");
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
            var (success1, message1, imageData1) = await driver.TakeElementScreenshotAsync(btnId);
            if (success1)
            {
                File.WriteAllBytes("btn_two.png", imageData1);
                Console.WriteLine("Saved btn_two.png");
            }
            else
            {
                Console.WriteLine($"Error taking element screenshot: {message1}");
            }

            // 2. Window Screenshot (Highlighting element)
            // This captures the window but draws a highlight box around the specified element.
            var (success2, message2, imageData2) = await driver.TakeWindowScreenshotAsync(btnId);
            if (success2)
            {
                File.WriteAllBytes("window_highlight.png", imageData2);
                Console.WriteLine("Saved window_highlight.png");
            }
            else
            {
                Console.WriteLine($"Error taking window screenshot (highlight): {message2}");
            }
            
            // 3. Full Screen Screenshot
            var (successFull, messageFull, imageDataFull) = await driver.TakeWindowScreenshotAsync();
            if (successFull)
            {
                File.WriteAllBytes("full_screen.png", imageDataFull);
                Console.WriteLine("Saved full_screen.png");
            }
            else
            {
                Console.WriteLine($"Error taking full screen screenshot: {messageFull}");
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
            var (success, message) = await driver.CloseAppAsync("CalculatorApp");
            if (!success) 
                Console.WriteLine($"CloseApp (by name) Error: {message}");
            else 
                Console.WriteLine($"CloseApp (by name): {message}");
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
            Console.WriteLine("Testing CloseAppByProcessIdAsync...");
            var (success, message, processId2) = await driver.OpenAppAsync("calc");
            if (success)
            {
                Console.WriteLine($"Opened calc (PID: {processId2}) for PID close test.");
                await Task.Delay(1000);
                var (closePidSuccess, closePidMessage) = await driver.CloseAppByProcessIdAsync(processId2);
                if (closePidSuccess)
                    Console.WriteLine("Successfully closed by PID.");
                else
                    Console.WriteLine($"Failed to close by PID: {closePidMessage}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CloseAppByProcessIdAsync Error: {ex.Message}");
        }
        */
    }
}
