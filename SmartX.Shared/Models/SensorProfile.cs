using System;
using System.Collections.Generic;
using System.Text;

namespace SmartX.Shared.Models
{
    public class SensorProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string MacAddress { get; set; } = string.Empty;
        public SensorCategory Category { get; set; }
        public DeploymentNode DeploymentLocation { get; set; } = new();
        public List<string> AttachmentFileNames { get; set; } = new();
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    }

    public class TelemetryBatchRequest
    {
        public Guid SensorId { get; set; }
        public string MetricName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public float[][] Batches { get; set; } = Array.Empty<float[]>();
    }

    public  record IntBatchRequest (Guid SensorId, string MetricName, string Unit, int[][] Batches);
    public record BoolBatchRequest(Guid SensorId, string MetricName, string Unit, bool[][] Batches);
    public record MenuPillar(string Title, string Route, bool Enabled, string Description);
    public record MeterAggregateResult(string Meter3, string Delta, string Comparison); 
}

    

