using Grpc.Core;
using Grpc.Reflection;
using Grpc.Reflection.V1Alpha;
using System;
using System.Diagnostics;
using System.ServiceProcess;
using UiAutomation;

namespace UiAutomationGRPC.Server
{
    public partial class MainService : ServiceBase
    {
        public MainService()
        {
            InitializeComponent();
        }

        private Grpc.Core.Server _server;

        protected override void OnStart(string[] args)
        {
            try 
            {
                var reflectionServiceImpl = new ReflectionServiceImpl(UiAutomationService.Descriptor, ServerReflection.Descriptor);
                _server = new Grpc.Core.Server
                {
                    Services = { 
                        UiAutomationService.BindService(new UiAutomationServiceImplementation()),
                        ServerReflection.BindService(reflectionServiceImpl)
                    },
                    Ports = { new ServerPort("127.0.0.1", 50051, ServerCredentials.Insecure) }
                };
                _server.Start();
            }
            catch (Exception ex)
            {
                // In a real windows service, you'd log this to EventLog.
                EventLog.WriteEntry("UiAutomationGRPC", $"Failed to start gRPC server: {ex}", EventLogEntryType.Error);
                throw; // Rethrow to fail the service start
            }
        }

        protected override void OnStop()
        {
            _server?.ShutdownAsync().Wait();
        }

        public void RunAsConsole(string[] args)
        {
            OnStart(args);
            Console.WriteLine("Server started. Running indefinitely...");
            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
            OnStop();
        }
    }
}
