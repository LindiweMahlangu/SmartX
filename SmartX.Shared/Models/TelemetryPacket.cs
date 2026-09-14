using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SmartX.Shared.Models
{
    public class TelemetryPacket<T>
    {
        public Guid sensorId { get; set; }
        public string MetricName { get; set; } = string.Empty;
        public T Value { get; set; } = default!;
        public string Unit { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public AnomalyStatus Status { get; set; } = AnomalyStatus.Normal;

        public override string ToString() =>

                $"[{Timestamp:HH:mm:ss}]{MetricName}={Value}{Unit}({Status})";

    }

    public static class TelemetryPacketFactory
    {
        public static List<TelemetryPacket<T>> WrapBatches<T>(
            Guid SensorId, string metricName, string unit, T[][] batches) where T : struct
        {
            var result = new List<TelemetryPacket<T>>();

            for (int batchIndex = 0; batchIndex < batches.Length; batchIndex++)
            {
                T[] batch = batches[batchIndex];
                for (int i = 0; i < batch.Length; i++)
                {
                    result.Add(new TelemetryPacket<T>
                    {
                        sensorId = SensorId,
                        MetricName = metricName,
                        Value = batch[i],
                        Unit = unit,

                    });
                }
            }
            return result;
        }

    }
}


