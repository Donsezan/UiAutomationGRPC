using System.Diagnostics;
using System.Text;

namespace UiAutomationGRPC.Library.Helpers;

/// <summary>
/// Helper for performance monitoring.
/// </summary>
public static class PerformanceHelper
{
    private const string AppName = "TestContexts.AppName";
    private const string PerformanceFilePath = "TestContexts.PerformanceFilePath";

    /// <summary>
    /// Gets the average CPU usage over a sampling period.
    /// </summary>
    /// <returns>Average CPU usage percentage.</returns>
    public static float GetCpu()
    {
        const int delayBetweenMeasurement = 1000;
        const int measureCount = 60;
        float averageCpu = 0;

        try
        {
            var cpuList = new List<float>();
            using var myAppCpu = new PerformanceCounter("Process", "% Processor Time", AppName, true);

            for (var i = 0; i <= measureCount; i++)
            {
                var cpu = myAppCpu.NextValue() / Environment.ProcessorCount;
                if (i > 0)
                {
                    cpuList.Add(cpu);
                    Console.WriteLine($"{AppName} CPU % = {cpu}");
                }
                Thread.Sleep(delayBetweenMeasurement);
            }

            var sum = cpuList.Sum();
            averageCpu = sum / measureCount;
            Console.WriteLine($"{AppName} Average CPU % = {averageCpu}");
        }
        catch
        {
            // Ignored - performance counters may not be available
        }

        return averageCpu;
    }

    /// <summary>
    /// Saves performance data to a file.
    /// </summary>
    public static void SavePerformanceData(string measure, string message)
    {
        var localDate = DateTime.Now;
        var csvContent = new StringBuilder();
        var writeToFile = $"{localDate}, {measure}, {message}";
        csvContent.AppendLine(writeToFile);
        File.AppendAllText(PerformanceFilePath, csvContent.ToString());
        Console.WriteLine($"({localDate}) ::: {measure} ::: {message}");
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
            using var counter = new PerformanceCounter("Process", "Working Set - Private", AppName, true);
            currentMemoryUsedInMb = counter.RawValue / 1048576f;
            MeasurementContext.MemoryUsedList.Add(currentMemoryUsedInMb);
        }
        catch
        {
            currentMemoryUsedInMb = 0;
        }

        if (currentMemoryUsedInMb > MeasurementContext.MemoryUsageLimit)
        {
            Console.WriteLine($"::: Memory usage limit overflow: {currentMemoryUsedInMb} Mb :::");
            throw new InvalidOperationException("Memory usage limit overflow");
        }

        return currentMemoryUsedInMb;
    }

    /// <summary>
    /// Gets average memory usage from the context list.
    /// </summary>
    public static float GetAverageMemory()
    {
        if (MeasurementContext.MemoryUsedList.Count == 0)
            return 0;

        MeasurementContext.MemoryUsedList.RemoveAt(0);
        var averageMemoryUsed = (float)Math.Round(
            MeasurementContext.MemoryUsedList.Sum() / MeasurementContext.MemoryUsedList.Count, 2);
        MeasurementContext.MemoryUsedList.Clear();

        return averageMemoryUsed;
    }

    /// <summary>
    /// Saves performance measures to CSV.
    /// </summary>
    public static void SavePerformanceData(string[] measures)
    {
        var fullFilePath = $"{PerformanceFilePath}.csv";

        if (!File.Exists(fullFilePath))
        {
            using var sw = File.CreateText(fullFilePath);
            sw.WriteLine("\"Data\",\"Release\",\"AppVersion\",\"Duration\",\"CPUmgzb\",\"Memory\",\"GamePage\"");
        }

        var sb = new StringBuilder();
        sb.Append($"{DateTime.Now:yyyy-MM-ddTHH:mm:ss},\"{AppName}\",");
        sb.Append(string.Join(",", measures));
        sb.AppendLine();

        File.AppendAllText(fullFilePath, sb.ToString());
        Logger.WriteLog("Measure done");
    }

    /// <summary>
    /// Writes memory leak measurements to file.
    /// </summary>
    public static void WriteMemoryLeaksMeasure(string nameOfMethod)
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
                Logger.WriteLog($"Memory after seconds: {currentTime / 1000}");

                if (i == controlMeasureCount - 1)
                {
                    var fullFilePath = $"{PerformanceFilePath}MemoryLeaks.csv";
                    if (!File.Exists(fullFilePath))
                    {
                        using var sw = File.CreateText(fullFilePath);
                        sw.WriteLine("Test name and time seconds,Measure mb");
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine($"{nameOfMethod}{currentTime / 1000},{list.Min()}");
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
    public static void TrackMemoryLeaks(Action action, string nameOfMethod, params int[] attempts)
    {
        WriteMemoryLeaksMeasure($"{nameOfMethod} First measure ");

        for (var n = attempts.Length - 1; n >= 0; n--)
        {
            for (var i = 0; i < attempts[n]; i++)
                action();

            WriteMemoryLeaksMeasure($"{nameOfMethod} Second measure ");
        }
    }
}

/// <summary>
/// Message for performance measure.
/// </summary>
public class MeasureMessage
{
    public string Time { get; set; } = string.Empty;
    public string CPU { get; set; } = string.Empty;
    public string Memory { get; set; } = string.Empty;
    public string Place { get; set; } = string.Empty;
}
