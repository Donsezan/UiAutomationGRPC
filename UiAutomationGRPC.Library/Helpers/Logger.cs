using UiAutomationGRPC.Library.Elements;

namespace UiAutomationGRPC.Library.Helpers
{
    /// <summary>
    /// Helper for logging.
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// Writes a log entry for a specific UI element.
        /// </summary>
        /// <param name="element">The UI element.</param>
        /// <param name="log">The log message.</param>
        public static async Task WriteLogAsync(IAutomationElement element, string log)
        {
            Console.WriteLine("Step: " + DataHelper.GetCurrentMethodName());
            var name = await element.NameAsync();
            if (name != null)
            {
                Console.WriteLine("{0} {1}", log, name);
            }
            else
            {
                var automationId = await element.AutomationIdAsync();
                if (automationId != null)
                {
                    Console.WriteLine("{0} {1}", log, automationId);
                }
                else
                {
                    var className = await element.ClassNameAsync();
                    if (className != null)
                    {
                        Console.WriteLine("{0}  {1}", log, className);
                    }
                    else
                    {
                        Console.WriteLine("{0}" + log + " No any name");
                    }
                }
            }
        }

        /// <summary>
        /// Writes a log entry with memory usage info.
        /// </summary>
        /// <param name="message">The message.</param>
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

        /// <summary>
        /// Writes a simple log entry with method name.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="log">The log prefix.</param>
        public static void WriteLog(string message, string log)
        {
            Console.WriteLine("Step: " + DataHelper.GetCurrentMethodName());
            Console.WriteLine("{0} {1}", log, message);
        }
    }
}
