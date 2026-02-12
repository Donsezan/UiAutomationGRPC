using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UiAutomation;

namespace UiAutomationGRPC.LLM
{
    public class McpServer
    {
        private readonly UiAutomationService.UiAutomationServiceClient _client;
        private readonly Stream _inputStream;
        private readonly Stream _outputStream;
        private readonly string _screenshotFolder;

        public McpServer(UiAutomationService.UiAutomationServiceClient client)
        {
            _client = client;
            _inputStream = Console.OpenStandardInput();
            _outputStream = Console.OpenStandardOutput();
            _screenshotFolder = Path.Combine(Path.GetTempPath(), "UiAutomationGRPC_Screenshots");
            Directory.CreateDirectory(_screenshotFolder);
        }

        public async Task RunAsync()
        {
            using (var reader = new StreamReader(_inputStream, Encoding.UTF8))
            using (var writer = new StreamWriter(_outputStream, new UTF8Encoding(false)) { AutoFlush = true })
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    try
                    {
                        var request = JObject.Parse(line);
                        var response = await HandleRequestAsync(request);
                        if (response != null)
                        {
                            await writer.WriteLineAsync(response.ToString(Formatting.None));
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error to stderr
                        await Console.Error.WriteLineAsync($"Error processing request: {ex.Message}");
                    }
                }
            }
        }

        private async Task<JObject?> HandleRequestAsync(JObject request)
        {
            var method = request["method"]?.ToString();
            var id = request["id"];

            if (method == "initialize")
            {
                return new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id,
                    ["result"] = new JObject
                    {
                        ["protocolVersion"] = "2024-11-05",
                        ["capabilities"] = new JObject
                        {
                            ["tools"] = new JObject()
                        },
                        ["serverInfo"] = new JObject
                        {
                            ["name"] = "UiAutomation-MCP-CSharp",
                            ["version"] = "1.0.0"
                        }
                    }
                };
            }
            else if (method == "notifications/initialized")
            {
                return null; // No response needed
            }
            else if (method == "tools/list")
            {
                return new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id,
                    ["result"] = new JObject
                    {
                        ["tools"] = new JArray
                        {
                            GetToolDefinition_OpenApp(),
                            GetToolDefinition_GetAppStructure(),
                            GetToolDefinition_PerformAction(),
                            GetToolDefinition_PerformActionWithStructure(),
                            GetToolDefinition_CloseApp(),
                            GetToolDefinition_TakeScreenshot(),
                            GetToolDefinition_ClearCache()
                        }
                    }
                };
            }
            else if (method == "tools/call")
            {
                var paramsObj = request["params"] as JObject;
                var name = paramsObj?["name"]?.ToString();
                var args = paramsObj?["arguments"] as JObject;

                try
                {
                    JObject resultData;
                    switch (name)
                    {
                        case "open_app":
                            resultData = await HandleOpenApp(args);
                            break;
                        case "get_app_structure":
                            resultData = await HandleGetAppStructure(args);
                            break;
                        case "perform_action":
                            resultData = await HandlePerformAction(args);
                            break;
                        case "perform_action_with_structure":
                            resultData = await HandlePerformActionWithStructure(args);
                            break;
                        case "close_app":
                            resultData = await HandleCloseApp(args);
                            break;
                        case "take_screenshot":
                            resultData = await HandleTakeScreenshot(args);
                            break;
                        case "clear_cache":
                            resultData = await HandleClearCache();
                            break;
                        default:
                            throw new Exception($"Unknown tool: {name}");
                    }

                    return new JObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = id,
                        ["result"] = resultData
                    };
                }
                catch (Exception ex)
                {
                    return new JObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = id,
                        ["error"] = new JObject
                        {
                            ["code"] = -32603,
                            ["message"] = ex.Message
                        }
                    };
                }
            }

            return null;
        }

        // --- Tool Definitions ---

        private JObject GetToolDefinition_OpenApp()
        {
            return JObject.FromObject(new
            {
                name = "open_app",
                description = "Opens an application.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        app_name = new { type = "string", description = "Path to the executable or app name." },
                        arguments = new { type = "string", description = "Command line arguments." }
                    },
                    required = new[] { "app_name" }
                }
            });
        }

        private JObject GetToolDefinition_GetAppStructure()
        {
            return JObject.FromObject(new
            {
                name = "get_app_structure",
                description = "Gets the UI structure of the application as JSON.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        process_id = new { type = "integer", description = "Process ID of the application." },
                        app_name = new { type = "string", description = "Name of the app (if process_id not used)." },
                        use_process_id = new { type = "boolean", description = "Set to true to use process_id lookup." }
                    },
                    required = new[] { "use_process_id" }
                }
            });
        }

        private JObject GetToolDefinition_PerformAction()
        {
            return JObject.FromObject(new
            {
                name = "perform_action",
                description = "Performs an action on a UI element found in the structure.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        runtime_id = new { type = "string", description = "The runtime ID of the element (from get_app_structure)." },
                        action = new { type = "string", description = "The action to perform (e.g., INVOKE, CLICK, SET_VALUE)." },
                        arguments = new { type = "array", items = new { type = "string" }, description = "Arguments for the action (e.g. text for SET_VALUE)." }
                    },
                    required = new[] { "runtime_id", "action" }
                }
            });
        }

        private JObject GetToolDefinition_PerformActionWithStructure()
        {
            return JObject.FromObject(new
            {
                name = "perform_action_with_structure",
                description = "Performs an action on a UI element and returns the updated app structure. Ideal for LLM 'See-Think-Act' loops.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        runtime_id = new { type = "string", description = "The runtime ID of the element (from get_app_structure)." },
                        action = new { type = "string", description = "The action to perform (e.g., INVOKE, CLICK, SET_VALUE)." },
                        arguments = new { type = "array", items = new { type = "string" }, description = "Arguments for the action (e.g., text for SET_VALUE)." }
                    },
                    required = new[] { "runtime_id", "action" }
                }
            });
        }

        private JObject GetToolDefinition_CloseApp()
        {
            return JObject.FromObject(new
            {
                name = "close_app",
                description = "Closes the application by Process ID.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        process_id = new { type = "integer", description = "Process ID of the application to close." }
                    },
                    required = new[] { "process_id" }
                }
            });
        }

        // --- Handlers ---

        private async Task<JObject> HandleOpenApp(JObject? args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            var req = new AppRequest
            {
                AppName = args["app_name"]?.ToString() ?? "",
                Arguments = args["arguments"]?.ToString() ?? ""
            };
            var resp = await _client.OpenAppAsync(req);
            
            var content = new JArray();
            content.Add(new JObject
            {
                ["type"] = "text",
                ["text"] = JsonConvert.SerializeObject(resp)
            });

            return new JObject { ["content"] = content, ["isError"] = !resp.Success };
        }

        private async Task<JObject> HandleGetAppStructure(JObject? args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            var req = new AppStructureRequest
            {
                ProcessId = args["process_id"]?.Value<int>() ?? 0,
                AppName = args["app_name"]?.ToString() ?? "",
                UseProcessId = args["use_process_id"]?.Value<bool>() ?? false
            };
            var resp = await _client.GetAppStructureAsync(req);

             var content = new JArray();
             // The structure comes as a string in JsonStructure.
             // We can return it as text.
             var text = resp.Success ? resp.JsonStructure : resp.Message;

            content.Add(new JObject
            {
                ["type"] = "text",
                ["text"] = text
            });

            return new JObject { ["content"] = content, ["isError"] = !resp.Success };
        }

        private async Task<JObject> HandlePerformAction(JObject? args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            
            var req = BuildPerformActionRequest(args);
            var resp = await _client.PerformActionAsync(req);

            var content = new JArray();
            content.Add(new JObject
            {
                ["type"] = "text",
                ["text"] = JsonConvert.SerializeObject(new { resp.Success, resp.Message })
            });

            return new JObject { ["content"] = content, ["isError"] = !resp.Success };
        }

        private async Task<JObject> HandlePerformActionWithStructure(JObject? args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            
            var req = BuildPerformActionRequest(args);
            var resp = await _client.PerformActionWithStructureAsync(req);

            var content = new JArray();
            var text = resp.Success ? resp.JsonStructure : resp.Message;
            content.Add(new JObject
            {
                ["type"] = "text",
                ["text"] = text
            });

            return new JObject { ["content"] = content, ["isError"] = !resp.Success };
        }

        private PerformActionRequest BuildPerformActionRequest(JObject args)
        {
            var actionStr = args["action"]?.ToString();
            if (!Enum.TryParse<ActionType>(actionStr, true, out var actionType))
            {
                 // Fallback: try parsing as int if user sent int
                 if (int.TryParse(actionStr, out int actionInt))
                    actionType = (ActionType)actionInt;
                 else 
                    throw new ArgumentException($"Invalid action type: {actionStr}");
            }

            var req = new PerformActionRequest
            {
                RuntimeId = args["runtime_id"]?.ToString() ?? "",
                Action = actionType
            };

            var argArray = args["arguments"] as JArray;
            if (argArray != null)
            {
                foreach(var a in argArray)
                {
                    req.Arguments.Add(a.ToString());
                }
            }

            return req;
        }

        private async Task<JObject> HandleCloseApp(JObject? args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            
            var req = new CloseAppByProcessIdRequest
            {
                 ProcessId = args["process_id"]?.Value<int>() ?? 0
            };
            
            var resp = await _client.CloseAppByProcessIdAsync(req);

             var content = new JArray();
            content.Add(new JObject
            {
                ["type"] = "text",
                ["text"] = JsonConvert.SerializeObject(new { resp.Success, resp.Message })
            });

            return new JObject { ["content"] = content, ["isError"] = !resp.Success };
        }

        private JObject GetToolDefinition_ClearCache()
        {
            return JObject.FromObject(new
            {
                name = "clear_cache",
                description = "Clears the server-side element cache. Call as a teardown step after closing an application to free memory and prevent stale element references.",
                inputSchema = new
                {
                    type = "object",
                    properties = new { }
                }
            });
        }

        private async Task<JObject> HandleClearCache()
        {
            var resp = await _client.ClearCacheAsync(new ClearCacheRequest());

            var content = new JArray();
            content.Add(new JObject
            {
                ["type"] = "text",
                ["text"] = JsonConvert.SerializeObject(new { resp.Success, resp.Message })
            });

            return new JObject { ["content"] = content, ["isError"] = !resp.Success };
        }

        private JObject GetToolDefinition_TakeScreenshot()
        {
            return JObject.FromObject(new
            {
                name = "take_screenshot",
                description = "Takes a screenshot of the application window or a specific element. Returns the file path to the saved screenshot image.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        mode = new { type = "string", description = "Screenshot mode: 'element' for a specific element, 'window' for the entire window.", @enum = new[] { "element", "window" } },
                        runtime_id = new { type = "string", description = "The runtime ID of the element (required for 'element' mode, optional for 'window' mode to capture that element's window)." },
                        process_id = new { type = "integer", description = "Process ID (optional, used for 'window' mode if runtime_id is not provided)." }
                    },
                    required = new[] { "mode" }
                }
            });
        }

        private async Task<JObject> HandleTakeScreenshot(JObject? args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            var modeStr = args["mode"]?.ToString()?.ToLowerInvariant() ?? "window";
            var runtimeId = args["runtime_id"]?.ToString() ?? "";
            var processId = args["process_id"]?.Value<int>() ?? 0;

            var screenshotMode = modeStr == "element" 
                ? ScreenshotMode.Element 
                : ScreenshotMode.Window;

            var req = new ScreenshotRequest
            {
                Mode = screenshotMode,
                RuntimeId = runtimeId,
                ProcessId = processId
            };

            var resp = await _client.TakeScreenshotAsync(req);

            var content = new JArray();

            if (resp.Success && resp.ImageData != null && resp.ImageData.Length > 0)
            {
                // Save to temp file
                var fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                var filePath = Path.Combine(_screenshotFolder, fileName);
                File.WriteAllBytes(filePath, resp.ImageData.ToByteArray());

                content.Add(new JObject
                {
                    ["type"] = "text",
                    ["text"] = JsonConvert.SerializeObject(new 
                    { 
                        success = true, 
                        message = "Screenshot saved successfully.",
                        file_path = filePath,
                        file_name = fileName
                    })
                });

                return new JObject { ["content"] = content, ["isError"] = false };
            }
            else
            {
                content.Add(new JObject
                {
                    ["type"] = "text",
                    ["text"] = JsonConvert.SerializeObject(new { success = false, message = resp.Message ?? "Failed to take screenshot." })
                });

                return new JObject { ["content"] = content, ["isError"] = true };
            }
        }
    }
}
