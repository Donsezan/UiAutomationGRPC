using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UiAutomation;

namespace UiAutomationGRPC.MCP
{
    public class McpServer
    {
        private readonly UiAutomationService.UiAutomationServiceClient _client;
        private readonly ToolsHandler _toolsHandler;

        public McpServer(UiAutomationService.UiAutomationServiceClient client)
        {
            _client = client;
            _toolsHandler = new ToolsHandler(_client);
        }

        public async Task RunAsync()
        {
            var input = Console.OpenStandardInput();
            var output = Console.OpenStandardOutput();
            using var reader = new StreamReader(input);
            using var writer = new StreamWriter(output) { AutoFlush = true };

            while (!reader.EndOfStream)
            {
                string? line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    await HandleMessageAsync(line, writer);
                }
                catch (Exception ex)
                {
                    // Log error to stderr so it doesn't interfere with JSON-RPC on stdout
                    Console.Error.WriteLine($"Error processing message: {ex.Message}");
                }
            }
        }

        private async Task HandleMessageAsync(string json, StreamWriter writer)
        {
            JObject? request;
            try
            {
                request = JObject.Parse(json);
            }
            catch
            {
                return;
            }

            if (request == null) return;

            var id = request["id"];
            var method = request["method"]?.ToString();
            
            // Handle notifications (no id)
            if (id == null) 
            {
                if (method == "notifications/initialized")
                {
                    // Client initialized
                }
                return;
            }

            JObject response = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id
            };

            try
            {
                JToken result = method switch
                {
                    "initialize" => HandleInitialize(),
                    "tools/list" => HandleToolsList(),
                    "tools/call" => await HandleToolCallAsync(request["params"]),
                    "ping" => new JObject(),
                    _ => throw new InvalidOperationException($"Method not found: {method}")
                };

                response["result"] = result;
            }
            catch (Exception ex)
            {
                response["error"] = new JObject
                {
                    ["code"] = -32603,
                    ["message"] = ex.Message
                };
            }

            await writer.WriteLineAsync(response.ToString(Formatting.None));
        }

        private JToken HandleInitialize()
        {
            return new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JObject
                {
                    ["tools"] = new JObject()
                },
                ["serverInfo"] = new JObject
                {
                    ["name"] = "UiAutomation-MCP-Server",
                    ["version"] = "1.0"
                }
            };
        }

        private JToken HandleToolsList()
        {
            return new JObject
            {
                ["tools"] = _toolsHandler.GetToolsList()
            };
        }

        private async Task<JToken> HandleToolCallAsync(JToken? parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            
            string name = parameters["name"]?.ToString() ?? throw new ArgumentException("Tool name missing");
            JObject args = (parameters["arguments"] as JObject) ?? new JObject();

            return await _toolsHandler.ExecuteToolAsync(name, args);
        }
    }
}
