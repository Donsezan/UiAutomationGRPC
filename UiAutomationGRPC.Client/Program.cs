using System;
using System.Threading.Tasks;
using Grpc.Core;
using UiAutomation;
using UiAutomationGRPC.Client.Calc.Pages;

namespace UiAutomationGRPC.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Connecting to server...");
            
            var channel = new Channel("127.0.0.1:50051", ChannelCredentials.Insecure);
            var client = new UiAutomationService.UiAutomationServiceClient(channel);

            try 
            {
                // 1. Open Calc
                Console.WriteLine("Opening Calculator...");
                var openResponse = await client.OpenAppAsync(new AppRequest { AppName = "calc" });
                if (!openResponse.Success) 
                {
                    Console.WriteLine($"Failed to open app: {openResponse.Message}");
                    return;
                }
                Console.WriteLine($"App opened with Process ID: {openResponse.ProcessId}");
                 // Allow some time for app to start
                await Task.Delay(2000);
                         
                // 3. Initialize Page
                // Wait for Calculator to be ready is implicit in PageObject or explicit here
                var calcPage = new CalcPage(client);

                // 4. Interaction
                Console.WriteLine("Waiting for interactions...");

                try {
                     calcPage
                        .ClickTwo()
                        .ClickPlus()
                        .ClickTwo()
                        .ClickEqual();

                     await Task.Delay(1000); // Wait for calculation

                    // Verify
                    var resultName = calcPage.GetResult();
                    Console.WriteLine($"Result Name: {resultName}");
                    
                    if (resultName.EndsWith("4"))
                        Console.WriteLine("SUCCESS: Result is 4.");
                    else
                        Console.WriteLine("FAILURE: Unexpected result.");

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Interaction Error: {ex.Message}");
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
    }
}
