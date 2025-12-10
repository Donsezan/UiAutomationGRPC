using System;
using UiAutomationGRPC.Library;

namespace UiAutomationGRPC.Library.Helpers
{
    public static class Logger
    {
        public static void WriteLog(IAutomationElement element, string log)
        {
            Console.WriteLine("Step: " + DataHelper.GetCurrentMethodName());
            if (element.Name() != null)
            {
                Console.WriteLine("{0} {1}", log, element.Name());
            }
            else if (element.AutomationId() != null)
            {
                Console.WriteLine("{0} {1}", log, element.AutomationId());
            }
            else if (element.ClassName() != null)
            {
                Console.WriteLine("{0}  {1}", log, element.ClassName());
            }
            else
            {
                Console.WriteLine("{0}" + log + " No any name");
            }
        }

        public static void WriteLog(string message)
        {
            var currentTime = DateTime.Now.ToLongTimeString();
            var memoryUsed = PerformanceHelper.GetMemory();
            if (memoryUsed > 0)
            {
                Console.WriteLine("({0}) ::: {1} ::: {2} Mb memory usage", currentTime, message, memoryUsed);
            }
            else
            {
                if (MeasurementContext.MemoryUsedList.Count > 1)
                {
                    var averageMemoryUsed = PerformanceHelper.GetAverageMemory();
                    Console.WriteLine("({0}) ::: {1} ::: Average memory usage {2} Mb", currentTime, message,
                        averageMemoryUsed);
                }
                else
                {
                    Console.WriteLine("({0}) ::: {1}", currentTime, message);
                }
            }
        }

        public static void WriteLog(string message, string log)
        {
            Console.WriteLine("Step: " + DataHelper.GetCurrentMethodName());
            Console.WriteLine("{0} {1}", log, message);
        }
    }
}
