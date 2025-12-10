using System;
using System.Threading.Tasks;
using UiAutomationGRPC.Client.Calc.Pages;
using UiAutomationGRPC.Library;

namespace UiAutomationGRPC.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Connecting to server...");
            
            // Using the new Driver to organize usage
            using (var driver = new UiAutomationDriver("127.0.0.1:50051"))
            {
                // var client = driver.Client; // Internal now
                try 
                {             
                    // 1. Open Calc
                    Console.WriteLine("Opening Calculator...");
                    int processId = 0;
                    try 
                    {
                        var openResult = await driver.OpenApp("calc");
                        if (!openResult.Success)
                        {
                            Console.WriteLine($"Failed to open app: {openResult.Message}");
                            return;
                        }
                        processId = openResult.ProcessId;
                        Console.WriteLine($"App opened with Process ID: {processId}");
                    }
                    catch (Exception ex)
                    {
                         Console.WriteLine(ex.Message);
                         return;
                    }

                     // Allow some time for app to start
                    await Task.Delay(2000);
                             
                    // 3. Initialize Page
                    // Wait for Calculator to be ready is implicit in PageObject or explicit here
                    var calcPage = new CalcPage(driver);

                    // 4. Interaction
                    Console.WriteLine("Waiting for interactions...");

                    // Example 1: Click interactions
                    try {
                         Console.WriteLine("Performing Click interactions...");
                         calcPage
                            .ClickTwo()
                            .ClickPlus()
                            .ClickTwo()
                            .ClickEqual();
                         
                         // Verify
                         var resultName = calcPage.GetResult();
                         Console.WriteLine($"Click Result: {resultName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Click Interaction Error: {ex.Message}");
                    }

                    await Task.Delay(1000);

                    // Example 2: Keyboard interactions
                    try
                    {
                        Console.WriteLine("Performing Keyboard interactions...");
                        // Sending "2+2="
                        calcPage.SendKey("2");
                        calcPage.SendKey("{ADD}"); // + is {ADD} or + depending on implementation, SendKeys usually takes + as shift, but for calc maybe simple
                        calcPage.SendKey("2");
                        calcPage.SendKey("=");

                        await Task.Delay(1000); // Wait for calculation

                        // Verify
                        var resultName = calcPage.GetResult();
                        Console.WriteLine($"Keyboard Result Name: {resultName}");

                        if (resultName.EndsWith("8")) // accumulated if not cleared, or 4 if cleared or new
                            Console.WriteLine("SUCCESS: Result ends with expected value (context dependent).");
                        else
                            Console.WriteLine($"Result: {resultName}");

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Keyboard Interaction Error: {ex.Message}");
                    }

                    // Example 3: Close app by Name
                    try
                    {
                        var closeResult = driver.CloseApp("CalculatorApp");
                        if (!closeResult.Success) Console.WriteLine($"CloseApp Error: {closeResult.Message}");
                        else Console.WriteLine(closeResult.Message);
                    }
                    catch (Exception ex)
                    {

                        Console.WriteLine($"Close app Error: {ex.Message}");
                    }

                    // Example 4: Open and Close by PID
                    try
                    {
                        Console.WriteLine("Testing CloseAppByProcessId...");
                        var openResult2 = await driver.OpenApp("calc");
                        if (openResult2.Success)
                        {
                            Console.WriteLine($"Opened calc (PID: {openResult2.ProcessId}) for PID close test.");
                            await Task.Delay(1000); 
                            var closePidResult = driver.CloseAppByProcessId(openResult2.ProcessId);
                            if (closePidResult.Success) Console.WriteLine("Successfully closed by PID.");
                            else Console.WriteLine($"Failed to close by PID: {closePidResult.Message}");
                        }
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"CloseAppByProcessId Error: {ex.Message}");
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
    }
}
