using System;
using System.Threading.Tasks;
using Grpc.Core;
using UiAutomation;

namespace UiAutomationGRPC.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Connecting to server...");
            
            // Grpc.Core channel creation
            var channel = new Channel("127.0.0.1:50051", ChannelCredentials.Insecure);
            // Wait for connection? Grpc.Core connects lazily.
            
            var client = new UiAutomationService.UiAutomationServiceClient(channel);

            try 
            {
                // 1. Open Calc
                Console.WriteLine("Opening Calculator...");
                var openResponse = await client.OpenAppAsync(new OpenAppRequest { AppName = "calc" });
                if (!openResponse.Success) 
                {
                    Console.WriteLine($"Failed to open app: {openResponse.Message}");
                    return;
                }
                Console.WriteLine($"App opened with Process ID: {openResponse.ProcessId}");
                
                // Wait for it to appear
                await Task.Delay(2000);

                // 2. Find Calculator Window
                Console.WriteLine("Finding Calculator Window...");
                
                var winCondition = new Condition
                {
                    PropertyCondition = new PropertyCondition 
                    { 
                        PropertyName = "Name", 
                        PropertyValue = "Calculator",
                        PropertyType = PropertyType.String 
                    }
                };

                var win = await client.FindElementAsync(new FindElementRequest 
                { 
                    StartRuntimeId = "", // Desktop
                    Condition = winCondition,
                    Scope = TreeScope.Descendants
                });
                
                Console.WriteLine($"Found Window: {win.Name} ({win.RuntimeId})");

                // 3. Find Controls (Buttons 2, +, =, Result)
                string twoId = "num2Button";
                string plusId = "plusButton";
                string equalId = "equalButton";
                
                var btnTwo = await FindByAutomationId(client, win.RuntimeId, twoId);
                var btnPlus = await FindByAutomationId(client, win.RuntimeId, plusId);
                var btnEqual = await FindByAutomationId(client, win.RuntimeId, equalId);

                // 4. Perform 2 + 2 =
                Console.WriteLine("Clicking 2...");
                await PerformClick(client, btnTwo.RuntimeId);
                await Task.Delay(500);

                Console.WriteLine("Clicking +...");
                await PerformClick(client, btnPlus.RuntimeId);
                await Task.Delay(500);

                Console.WriteLine("Clicking 2...");
                await PerformClick(client, btnTwo.RuntimeId);
                await Task.Delay(500);

                Console.WriteLine("Clicking =...");
                await PerformClick(client, btnEqual.RuntimeId);
                await Task.Delay(1000);

                // 5. Get Result
                var resultEl = await FindByAutomationId(client, win.RuntimeId, "CalculatorResults");
                
                // Get Name Property
                var nameProp = await client.GetPropertyAsync(new GetPropertyRequest 
                { 
                    RuntimeId = resultEl.RuntimeId, 
                    PropertyName = "Name" 
                });

                Console.WriteLine($"Result Element Name: {nameProp.Value}");
                
                if (nameProp.Value.EndsWith("4"))
                {
                    Console.WriteLine("SUCCESS: Result ends with 4.");
                }
                else 
                {
                    Console.WriteLine("FAILURE: Result does not end with 4.");
                }
            }
            catch (RpcException rpcEx)
            {
                Console.WriteLine($"RPC Error: {rpcEx.Status.StatusCode} - {rpcEx.Status.Detail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                await channel.ShutdownAsync();
            }
        }

        private static async Task<ElementResponse> FindByAutomationId(UiAutomationService.UiAutomationServiceClient client, string rootId, string autoId)
        {
            Console.WriteLine($"Finding Element (Id: {autoId})...");
            return await client.FindElementAsync(new FindElementRequest 
            { 
                StartRuntimeId = rootId, 
                Scope = TreeScope.Descendants,
                Condition = new Condition 
                { 
                    PropertyCondition = new PropertyCondition
                    {
                        PropertyName = "AutomationId",
                        PropertyValue = autoId,
                        PropertyType = PropertyType.String
                    }
                }
            });
        }

        private static async Task PerformClick(UiAutomationService.UiAutomationServiceClient client, string runtimeId)
        {
            var resp = await client.PerformActionAsync(new PerformActionRequest
            {
                RuntimeId = runtimeId,
                Action = ActionType.Click
            });
            
            if (!resp.Success) Console.WriteLine($"Click warning: {resp.Message}");
        }
    }
}
