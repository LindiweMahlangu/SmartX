using System;
using System.Collections.Generic;
using System.Text;

namespace SmartX.Shared.Models
{
    public class DeviceState
    {
        public Guid SensorId { get; set; }
        public string MacAddress { get; set; } = string.Empty;
        public SensorCategory Category { get; set; }
        public string LastMetricName { get; set; } = string.Empty;
        public float LastValue { get; set; }
        public string LastUnit { get; set; } = string.Empty;
        public AnomalyStatus LastStatus { get; set; } = AnomalyStatus.Normal;
        public DateTime LastSeen { get; set; }= DateTime.UtcNow;
        public bool ActuatorOn { get; set; } 
    }
}
