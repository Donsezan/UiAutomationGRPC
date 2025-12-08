using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace UiAutomationGRPC.Library.Helpers
{
    public class MeasurementContext
    {
        public static List<float> MemoryUsedList { get; } = new List<float>();

        public const int MemoryUsageLimit = 1000;
        public const int TimeDelayBeforeMeasure = 60000;
        public const int MeasureCounter = 10;
        //public const int TimeDelayBeforeMeasure = 6; //debug
        //public const int MeasureCounter = 1;
        public static long TimeMilliseconds { get; set; }
        public static List<string> Measurements = new List<string>();
    }

    public class MeasureModel
    {
        public List<float> Cpu = new List<float>();
        public List<float> Memory = new List<float>();
        public List<long> Time = new List<long>();
        private readonly string _place;

        public MeasureModel(string place)
        {
            _place = place;
        }

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
