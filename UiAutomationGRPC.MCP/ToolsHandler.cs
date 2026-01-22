using Google.Protobuf.Collections;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UiAutomation;

namespace UiAutomationGRPC.MCP
{
    public class ToolsHandler
    {
        private readonly UiAutomationService.UiAutomationServiceClient _client;

        public ToolsHandler(UiAutomationService.UiAutomationServiceClient client)
        {
            _client = client;
        }

        public JArray GetToolsList()
        {
            var tools = new JArray
            {
                new JObject
                {
                    ["name"] = "find_element",
                    ["description"] = "Finds a UI element based on conditions.",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["condition_type"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "property", "and", "or" } },
                            ["property_name"] = new JObject { ["type"] = "string", ["description"] = "e.g. Name, AutomationId, ControlType" },
                            ["property_value"] = new JObject { ["type"] = "string" },
                            ["scope"] = new JObject { ["type"] = "string", ["description"] = "ELEMENT, CHILDREN, DESCENDANTS, SUBTREE" },
                            ["start_runtime_id"] = new JObject { ["type"] = "string", ["description"] = "Optional root element runtime ID" }
                        },
                        ["required"] = new JArray { "condition_type" }
                    }
                },
                new JObject
                {
                    ["name"] = "perform_action",
                    ["description"] = "Performs an action on a UI element.",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["runtime_id"] = new JObject { ["type"] = "string" },
                            ["action"] = new JObject { ["type"] = "string", ["description"] = "INVOKE, CLICK, SET_VALUE, etc." },
                            ["arguments"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "string" } }
                        },
                        ["required"] = new JArray { "runtime_id", "action" }
                    }
                },
                new JObject
                {
                    ["name"] = "take_screenshot",
                    ["description"] = "Takes a screenshot.",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["mode"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "SCREENSHOT_MODE_ELEMENT", "SCREENSHOT_MODE_WINDOW" } },
                            ["runtime_id"] = new JObject { ["type"] = "string" }
                        },
                        ["required"] = new JArray { "mode" }
                    }
                },
                new JObject
                {
                    ["name"] = "open_app",
                    ["description"] = "Opens an application.",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["app_name"] = new JObject { ["type"] = "string", ["description"] = "Executable name or path" },
                            ["arguments"] = new JObject { ["type"] = "string", ["description"] = "Optional arguments" }
                        },
                        ["required"] = new JArray { "app_name" }
                    }
                },
                new JObject
                {
                    ["name"] = "get_property",
                    ["description"] = "Gets a property value of an element.",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                             ["runtime_id"] = new JObject { ["type"] = "string" },
                             ["property_name"] = new JObject { ["type"] = "string" }
                        },
                        ["required"] = new JArray { "runtime_id", "property_name" }
                    }
                }
                new JObject
                {
                    ["name"] = "get_children",
                    ["description"] = "Gets the children of a UI element.",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                             ["runtime_id"] = new JObject { ["type"] = "string", ["description"] = "Parent element runtime ID. Empty for desktop." }
                        }
                    }
                },
                new JObject
                {
                    ["name"] = "close_app",
                    ["description"] = "Closes an application.",
                    ["inputSchema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["app_name"] = new JObject { ["type"] = "string" }
                        },
                        ["required"] = new JArray { "app_name" }
                    }
                }
            };
        }

        public async Task<JToken> ExecuteToolAsync(string name, JObject args)
        {
            try
            {
                switch (name)
                {
                    case "find_element":
                        return await FindElement(args);
                    case "perform_action":
                        return await PerformAction(args);
                    case "take_screenshot":
                        return await TakeScreenshot(args);
                    case "open_app":
                        return await OpenApp(args);
                    case "get_property":
                        return await GetProperty(args);
                    case "get_children":
                        return await GetChildren(args);
                    case "close_app":
                        return await CloseApp(args);
                    case "get_children":
                        return await GetChildren(args);
                    case "close_app":
                        return await CloseApp(args);
                    default:
                        throw new ArgumentException($"Unknown tool: {name}");
                }
            }
            catch (Exception ex)
            {
                 return new JObject
                 {
                     ["content"] = new JArray 
                     {
                         new JObject 
                         {
                             ["type"] = "text",
                             ["text"] = $"Error executing tool {name}: {ex.Message}"
                         }
                     },
                     ["isError"] = true
                 };
            }
        }

        private async Task<JToken> FindElement(JObject args)
        {
            var req = new FindElementRequest
            {
                 StartRuntimeId = args["start_runtime_id"]?.ToString() ?? "",
                 Scope = ParseScope(args["scope"]?.ToString()),
                 Condition = ParseCondition(args)
            };

            var resp = await _client.FindElementAsync(req);
            
            return new JObject
            {
                ["content"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = JObject.FromObject(resp).ToString()
                    }
                }
            };
        }

        private async Task<JToken> PerformAction(JObject args)
        {
            var req = new PerformActionRequest
            {
                RuntimeId = args["runtime_id"]?.ToString() ?? "",
                Action = ParseAction(args["action"]?.ToString())
            };

            if (args["arguments"] is JArray arr)
            {
                foreach (var item in arr)
                {
                    req.Arguments.Add(item.ToString());
                }
            }

            var resp = await _client.PerformActionAsync(req);

            return new JObject
            {
                ["content"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = resp.Message
                    }
                }
            };
        }

        private async Task<JToken> TakeScreenshot(JObject args)
        {
            var modeStr = args["mode"]?.ToString() ?? "SCREENSHOT_MODE_WINDOW";
             Enum.TryParse<ScreenshotMode>(modeStr, true, out var mode);

            var req = new ScreenshotRequest
            {
                RuntimeId = args["runtime_id"]?.ToString() ?? "",
                Mode = mode
            };

            var resp = await _client.TakeScreenshotAsync(req);
            string result = resp.Success ? $"Screenshot taken ({resp.ImageData.Length} bytes)" : $"Failed: {resp.Message}";
            
            // In a real scenario, we might want to return the base64 image or a path.
            // For now, let's return the simplified text result. 
            // If we want to return image, we can use "type": "image", "data": base64...
            
            return new JObject
            {
                ["content"] = new JArray
                {
                    new JObject
                    {
                         ["type"] = "text",
                         ["text"] = result
                    }
                }
            };
        }

        private async Task<JToken> OpenApp(JObject args)
        {
            var req = new AppRequest
            {
                AppName = args["app_name"]?.ToString() ?? "",
                Arguments = args["arguments"]?.ToString() ?? ""
            };

            var resp = await _client.OpenAppAsync(req);

            return new JObject
            {
                ["content"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = resp.Success ? $"Opened app with PID: {resp.ProcessId}" : $"Failed: {resp.Message}"
                    }
                }
            };
        }

        private async Task<JToken> GetProperty(JObject args)
        {
            var req = new GetPropertyRequest
            {
                RuntimeId = args["runtime_id"]?.ToString() ?? "",
                PropertyName = args["property_name"]?.ToString() ?? ""
            };

            var resp = await _client.GetPropertyAsync(req);

            return new JObject
            {
                ["content"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = resp.Success ? resp.Value : $"Failed: {resp.Message}"
                    }
                }
            };
        }

        private TreeScope ParseScope(string? scope)
        {
            if (Enum.TryParse<TreeScope>(scope, true, out var result))
                return result;
            return TreeScope.Descendants; // Default
        }

        private ActionType ParseAction(string? action)
        {
             if (Enum.TryParse<ActionType>(action, true, out var result))
                return result;
             return ActionType.Invoke;
        }

        private Condition ParseCondition(JObject args)
        {
            var type = args["condition_type"]?.ToString();
            var cond = new Condition();

            if (type == "property")
            {
                cond.PropertyCondition = new PropertyCondition
                {
                    PropertyName = args["property_name"]?.ToString() ?? "Name",
                    PropertyValue = args["property_value"]?.ToString() ?? "",
                    PropertyType = PropertyType.String
                };
            }
            // Add more condition types as needed (and, or, true_condition)
            else 
            {
                cond.TrueCondition = true;
            }
            return cond;
        }
        private async Task<JToken> GetChildren(JObject args)
        {
            var req = new GetChildrenRequest
            {
                RuntimeId = args["runtime_id"]?.ToString() ?? ""
            };
            var resp = await _client.GetChildrenAsync(req);
            
            return new JObject
            {
                ["content"] = new JArray 
                { 
                    new JObject 
                    { 
                        ["type"] = "text", 
                        ["text"] = JObject.FromObject(resp).ToString() 
                    } 
                }
            };
        }

        private async Task<JToken> CloseApp(JObject args)
        {
             var req = new AppRequest
             {
                 AppName = args["app_name"]?.ToString() ?? ""
             };
             var resp = await _client.CloseAppAsync(req);
             return new JObject
             {
                 ["content"] = new JArray
                 {
                     new JObject
                     {
                         ["type"] = "text",
                         ["text"] = resp.Message
                     }
                 }
             };
        }
    }
}
