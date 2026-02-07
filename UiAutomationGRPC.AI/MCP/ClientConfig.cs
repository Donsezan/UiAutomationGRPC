using System;
using System.IO;
using Newtonsoft.Json;

namespace UiAutomationGRPC.LLM
{
    public class ClientConfig
    {
        public string? ServerAddress { get; set; } = "http://localhost:50051";
        public string? AuthToken { get; set; }
        public bool AllowUnsecureTls { get; set; }
        public bool Insecure { get; set; }

        public static ClientConfig Load(string path = "uiautomation.config.json")
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var fullConfig = JsonConvert.DeserializeObject<FullConfig>(json);
                    return fullConfig?.Client ?? new ClientConfig();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error loading config file: {ex.Message}");
                }
            }
            return new ClientConfig();
        }

        private class FullConfig
        {
            public ClientConfig? Client { get; set; }
        }
    }
}
