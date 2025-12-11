using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UiAutomationGRPC.Library.Helpers
{
    /// <summary>
    /// Helper for performance monitoring.
    /// </summary>
    public static class PerformanceHelper
    {
        const string appName = "TestContexts.AppName";
        const string perfomanceFilePath = "TestContexts.PerfomanceFilePath";
        
        /// <summary>
        /// Gets the average CPU usage.
        /// </summary>
        /// <returns>Average CPU usage percentage.</returns>
        public static float GetCpu()
        {
            //const int delayBetweenMeasurement = 10;
            //const int measureCount = 3; //debug
           
            const int delayBetweenMeasurement = 1000;
            const int measureCount = 60; //get data times with period 1 second
            float averageCpu = 0;
            try
            {
                var cpuList = new List<float>();
                var myAppCpu =
                    new PerformanceCounter(
                        "Process", "% Processor Time", appName, true);
                for (var i = 0; i <= measureCount; i++)
                {
                    var cpu = myAppCpu.NextValue() / Environment.ProcessorCount;
                    if (i > 0) // skip first 0 value
                    {
                        cpuList.Add(cpu);
                        Console.WriteLine(appName + " CPU % = " + cpu);
                    }
                    Thread.Sleep(delayBetweenMeasurement);
                }
                var sum = cpuList.ToArray().Sum();
                averageCpu = sum / measureCount;
                Console.WriteLine(appName + " CPU % = " + averageCpu);
            }
            catch
            {
                // ignored
            }
            return averageCpu;
        }

        /// <summary>
        /// Saves performance data to file.
        /// </summary>
        /// <param name="measure">Measurement type.</param>
        /// <param name="message">Log message.</param>
        public static void SavePerformanceData(string measure, string message)
        {
            var localDate = DateTime.Now;
            var csvcontent = new StringBuilder();
            var writeToFile = $"{localDate}, {measure}, {message}";
            csvcontent.AppendLine(writeToFile);
            File.AppendAllText(perfomanceFilePath, csvcontent.ToString());
            Console.WriteLine("({0}) ::: {1} ::: {2}", localDate, measure, message);
        }

        /// <summary>
        /// Gets the current memory usage of the process.
        /// </summary>
        /// <returns>Memory usage in MB.</returns>
        public static float GetMemory()
        {
            float currentMemoryUsedInMb;
            try
            {
                var counter = new PerformanceCounter("Process", "Working Set - Private", appName, true);
                currentMemoryUsedInMb = counter.RawValue / 1048576f;
                MeasurementContext.MemoryUsedList.Add(currentMemoryUsedInMb);

            }
            catch
            {
                currentMemoryUsedInMb = 0;
            }

            if (currentMemoryUsedInMb > MeasurementContext.MemoryUsageLimit)
            {
                Console.WriteLine("::: Memory usage limit overflow: " + currentMemoryUsedInMb + " Mb :::");
                throw new Exception("Memory usage limit overflow");
            }

            return currentMemoryUsedInMb;
        }

        /// <summary>
        /// Gets average memory usage from the context list.
        /// </summary>
        /// <returns>Average memory usage.</returns>
        public static float GetAverageMemory()
        {
            float memoryUsedSum = 0;
            MeasurementContext.MemoryUsedList.RemoveAt(0);
            foreach (var currentMemUsd in MeasurementContext.MemoryUsedList)
                memoryUsedSum += currentMemUsd;
            var averageMemoryUsed = (float)Math.Round(memoryUsedSum / MeasurementContext.MemoryUsedList.Count, 2);
            MeasurementContext.MemoryUsedList.Clear();

            return averageMemoryUsed;
        }

        /// <summary>
        /// Saves performance measures to CSV.
        /// </summary>
        /// <param name="measures">Array of measure messages.</param>
        public static void SavePerformanceData(string[] measures)
        {

            var fullFilePath = perfomanceFilePath + ".csv";
            if (!File.Exists(fullFilePath))
                using (var sw = File.CreateText(fullFilePath))
                {
                    sw.WriteLine("\"Data\",\"Release\",\"AppVersion\",\"Duration\",\"CPUmgzb\",\"Memory\",\"GamePage\"");
                }

            var sb = new StringBuilder();
            sb.Append($"{DateTime.Now:yyyy-MM-ddTHH:mm:ss},\"{appName}\",");
            var i = 1;
            foreach (var measureMessage in measures)
            {
                sb.Append(i != measures.Length ? $"{measureMessage}," : $"{measureMessage}");
                i++;
            }
            sb.Append("\r\n");


            File.AppendAllText(fullFilePath, sb.ToString());
            Logger.WriteLog("Measure done");
        }

        /// <summary>
        /// Measurements memory leaks.
        /// </summary>
        /// <param name="nameofMethod">Method name.</param>
        public static void WriteMemoryLeaksMeasure(string nameofMethod)
        {
            const int controlMeasureCount = 10;
            const int timeBetweenMeasure = 50000;
            const int measureTimes = 3;
            var currentTime = 0;
            for (var a = 0; a < measureTimes; a++)
            {
                var list = new List<float>();
                for (var i = 0; i < controlMeasureCount; i++)
                {
                    const int intervals = 1000;
                    Thread.Sleep(intervals);
                    list.Add(GetMemory());
                    currentTime += intervals;
                    Logger.WriteLog("Memory after seconds: " + currentTime / 1000);
                    if (i == controlMeasureCount - 1)
                    {
                        var fullFilePath = perfomanceFilePath + "MemoryLeaks.csv";
                        if (!File.Exists(fullFilePath))
                            using (var sw = File.CreateText(fullFilePath))
                            {
                                sw.WriteLine("{0},{1}", "Test name and time seconds", "Measure mb");
                            }
                        var sb = new StringBuilder();
                        sb.Append($"{nameofMethod + currentTime / 1000},{list.Min()}");
                        sb.Append("\n");
                        File.AppendAllText(fullFilePath, sb.ToString());
                    }
                }
                Thread.Sleep(timeBetweenMeasure);
                currentTime += timeBetweenMeasure;
            }
        }

        /// <summary>
        /// Tracks memory leaks by executing an action multiple times.
        /// </summary>
        /// <param name="action">Action to execute.</param>
        /// <param name="nameofMethod">Method name.</param>
        /// <param name="attempts">Attempts array.</param>
        public static void TrackMemoryLeaks(Action action, string nameofMethod, params int[] attempts)
        {
            WriteMemoryLeaksMeasure(nameofMethod + " First measure ");
            for (var n = attempts.Length - 1; n >= 0; n--)
            {
                for (var i = 0; i < attempts[n]; i++)
                    action();
                WriteMemoryLeaksMeasure(nameofMethod + " Second measure ");
            }
        }
    }

    /// <summary>
    /// Message for performance measure.
    /// </summary>
    public class MeasureMessage
    {
        public string Time { get; set; }
        public string CPU { get; set; }
        public string Memory { get; set; }
        public string Place { get; set; }
    }
}
