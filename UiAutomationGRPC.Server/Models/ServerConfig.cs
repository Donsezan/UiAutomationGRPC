using System;
using System.IO;
using Newtonsoft.Json;

namespace UiAutomationGRPC.Server.Models
{
    public class ServerConfig
    {
        public string Address { get; set; } = "0.0.0.0:50051";
        public string AuthToken { get; set; }
        public string CertificatePath { get; set; } = "certs/server.crt";
        public string PrivateKeyPath { get; set; } = "certs/server.key";
        public bool Insecure { get; set; }

        public static ServerConfig Load(string path = "uiautomation.config.json")
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var fullConfig = JsonConvert.DeserializeObject<FullConfig>(json);
                    return fullConfig?.Server ?? new ServerConfig();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error loading config file: {ex.Message}");
                }
            }
            return new ServerConfig();
        }

        private class FullConfig
        {
            public ServerConfig Server { get; set; }
        }
    }
}
