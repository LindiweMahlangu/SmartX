using SmartX.Shared.Models;

namespace SmartX.API.Services;

public class AlertTracker : IAlertTracker
{
    private readonly HashSet<(Guid SensorId, AnomalyStatus Status)> _activeKeys = new();
    private readonly Dictionary<(Guid SensorId, AnomalyStatus Status), AlertEvent> _details = new();
    private readonly object _lock = new();
    private int _duplicatesSuppressed;

    public AlertEvent? Evaluate(TelemetryPacket<float> packet, string macAddress, string condition)
    {
        lock (_lock)
        {
            // A healthy reading clears that sensor's active error states.
            if (packet.Status == AnomalyStatus.Normal)
            {
                Clear((packet.SensorId, AnomalyStatus.Spike));
                Clear((packet.SensorId, AnomalyStatus.Disconnected));
                return null;
            }

            var key = (packet.SensorId, packet.Status);
            if (!_activeKeys.Add(key))      // false = already active, so it is a duplicate
            {
                _duplicatesSuppressed++;
                return null;
            }

            var message = packet.Status == AnomalyStatus.Disconnected
                ? $"{macAddress} went offline"
                : $"{macAddress}: {condition} ({packet.MetricName} {packet.Value:F1}{packet.Unit})";

            var alert = new AlertEvent(packet.SensorId, macAddress, packet.Status, condition, message, DateTime.UtcNow);
            _details[key] = alert;
            return alert;
        }
    }

    public AlertSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var active = _details.Values.OrderByDescending(a => a.RaisedAt).ToList();
            return new AlertSnapshot(active, _duplicatesSuppressed);
        }
    }

    private void Clear((Guid SensorId, AnomalyStatus Status) key)
    {
        if (_activeKeys.Remove(key))
        {
            _details.Remove(key);
        }
    }
}
