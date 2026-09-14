using System;
using System.Collections.Generic;
using System.Text;

namespace SmartX.Shared.Models
{
    public class MeterReading
    {
        public string MeterId { get; set; } = string.Empty;
        public double Kilowatts { get; set; }

        public static MeterReading operator +(MeterReading a, MeterReading b) =>
            new() { MeterId = $"{a.MeterId}+{b.MeterId}", Kilowatts = a.Kilowatts + b.Kilowatts };
        public static MeterReading operator -(MeterReading a, MeterReading b) =>
            new() { MeterId = $"{a.MeterId}-{b.MeterId}", Kilowatts = a.Kilowatts - b.Kilowatts };

        public static bool operator >(MeterReading a, MeterReading b) => a.Kilowatts > b.Kilowatts;
        public static bool operator <(MeterReading a, MeterReading b) => a.Kilowatts < b.Kilowatts;

        public override string ToString() => $"{MeterId}: {Kilowatts:F2} kW";
    }
}
