using SmartX.Shared.Models;

namespace SmartX.API.Services;

public class TelemetryHistoryService : ITelemetryHistoryService
{
    private const int MaxPointsPerSensor = 500;

    private readonly Dictionary<Guid, SortedDictionary<DateTime, TelemetryPacket<float>>> _logs = new();
    private readonly object _lock = new();

    public void Add(TelemetryPacket<float> packet)
    {
        lock (_lock)
        {
            if (!_logs.TryGetValue(packet.SensorId, out var log))
            {
                log = new SortedDictionary<DateTime, TelemetryPacket<float>>();
                _logs[packet.SensorId] = log;
            }

            // Keys must be unique: nudge by one tick if two packets share a timestamp.
            var key = packet.Timestamp;
            while (log.ContainsKey(key))
            {
                key = key.AddTicks(1);
            }

            packet.Timestamp = key;
            log.Add(key, packet);

            while (log.Count > MaxPointsPerSensor)
            {
                log.Remove(log.Keys.First()); // drop the oldest reading
            }
        }
    }

    public IReadOnlyList<TelemetryPacket<float>> GetTimeline(Guid sensorId, int take)
    {
        lock (_lock)
        {
            if (!_logs.TryGetValue(sensorId, out var log))
            {
                return Array.Empty<TelemetryPacket<float>>();
            }

            return log.Values.TakeLast(take).ToList();
        }
    }

    public IReadOnlyList<float> GetRecentValues(Guid sensorId, int take)
    {
        lock (_lock)
        {
            if (!_logs.TryGetValue(sensorId, out var log))
            {
                return Array.Empty<float>();
            }

            return log.Values
                .Where(p => p.Status != AnomalyStatus.Disconnected)
                .TakeLast(take)
                .Select(p => p.Value)
                .ToList();
        }
    }
}
