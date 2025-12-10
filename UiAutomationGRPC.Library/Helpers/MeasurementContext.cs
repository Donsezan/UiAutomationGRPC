using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace UiAutomationGRPC.Library.Helpers
{
    /// <summary>
    /// Context for performance measurements.
    /// </summary>
    public class MeasurementContext
    {
        /// <summary>
        /// List of memory usage measurements.
        /// </summary>
        public static List<float> MemoryUsedList { get; } = new List<float>();

        /// <summary>
        /// Memory usage limit in MB.
        /// </summary>
        public const int MemoryUsageLimit = 1000;
        
        /// <summary>
        /// Time delay before measurement in milliseconds.
        /// </summary>
        public const int TimeDelayBeforeMeasure = 60000;
        
        /// <summary>
        /// Number of measurements to take.
        /// </summary>
        public const int MeasureCounter = 10;
        
        //public const int TimeDelayBeforeMeasure = 6; //debug
        //public const int MeasureCounter = 1;
        
        /// <summary>
        /// Accumulated time in milliseconds.
        /// </summary>
        public static long TimeMilliseconds { get; set; }
        
        /// <summary>
        /// List of measurement strings.
        /// </summary>
        public static List<string> Measurements = new List<string>();
    }

    /// <summary>
    /// Model for a single measurement.
    /// </summary>
    public class MeasureModel
    {
        /// <summary>
        /// CPU usage list.
        /// </summary>
        public List<float> Cpu = new List<float>();
        
        /// <summary>
        /// Memory usage list.
        /// </summary>
        public List<float> Memory = new List<float>();
        
        /// <summary>
        /// Time list.
        /// </summary>
        public List<long> Time = new List<long>();
        private readonly string _place;

        /// <summary>
        /// Initializes a new instance of the <see cref="MeasureModel"/> class.
        /// </summary>
        /// <param name="place">The place/context of measurement.</param>
        public MeasureModel(string place)
        {
            _place = place;
        }

        /// <summary>
        /// Calculates average measurement data.
        /// </summary>
        /// <returns>Array of strings containing average Time, CPU, Memory, and Place.</returns>
        public string[] CountMeasureData()
        {
            var averageCpu = Cpu.ToArray().Sum() / MeasurementContext.MeasureCounter;
            averageCpu = (float)Math.Round(averageCpu, 2, MidpointRounding.AwayFromZero);

            var averageMemory = Memory.ToArray().Sum() / MeasurementContext.MeasureCounter;
            averageMemory = (float)Math.Round(averageMemory, 2, MidpointRounding.AwayFromZero);

            var averageTime = (double)Time.ToArray().Sum() / MeasurementContext.MeasureCounter / 1000;
            averageTime = Math.Round(averageTime, 2, MidpointRounding.AwayFromZero);

            return new[]{averageTime.ToString(CultureInfo.InvariantCulture),
                averageCpu.ToString(CultureInfo.InvariantCulture),
                averageMemory.ToString(CultureInfo.InvariantCulture),
                "\""+_place+"\"" };
        }
    }
}
