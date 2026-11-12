using SmartX.API.DataStructures;
using SmartX.Shared.Models;

namespace SmartX.API.Services;

public class TelemetryArchive : ITelemetryArchive
{
    private const int MaxPointsPerSensor = 20000;

    private readonly Dictionary<Guid, AvlTree<DateTime, TelemetryPacket<float>>> _trees = new();
    private readonly object _lock = new();

    public void Add(TelemetryPacket<float> packet)
    {
        lock (_lock)
        {
            Insert(GetOrCreate(packet.SensorId), packet);
        }
    }

    public void Seed(Guid sensorId, IEnumerable<TelemetryPacket<float>> packets)
    {
        lock (_lock)
        {
            var tree = GetOrCreate(sensorId);
            foreach (var packet in packets)
            {
                Insert(tree, packet);
            }
        }
    }

    public HistoryQueryResult Query(
        Guid sensorId, string macAddress, DateTime fromUtc, DateTime toUtc, int limit, bool newestFirst)
    {
        lock (_lock)
        {
            if (!_trees.TryGetValue(sensorId, out var tree) || tree.Count == 0)
            {
                return new HistoryQueryResult(
                    macAddress, string.Empty, string.Empty, fromUtc, toUtc, 0,
                    new List<HistoryEntry>(), null, null, null, 0, 0, 0);
            }

            int totalMatches = tree.CountRange(fromUtc, toUtc);
            var items = tree.Range(fromUtc, toUtc, limit, newestFirst, out int nodesVisited);

            var entries = items
                .Select(i => new HistoryEntry(i.Key, i.Value.Value, i.Value.Status))
                .ToList();

            var readings = items
                .Where(i => i.Value.Status != AnomalyStatus.Disconnected)
                .Select(i => i.Value.Value)
                .ToList();

            var newest = tree.Max()!.Value;   // KeyValuePair of the most recent reading
            var sample = newest.Value;

            return new HistoryQueryResult(
                macAddress,
                sample.MetricName,
                sample.Unit,
                fromUtc,
                toUtc,
                totalMatches,
                entries,
                readings.Count > 0 ? (float?)readings.Min() : null,
                readings.Count > 0 ? (float?)readings.Max() : null,
                readings.Count > 0 ? (float?)readings.Average() : null,
                nodesVisited,
                tree.Count,
                tree.Height);
        }
    }

    private AvlTree<DateTime, TelemetryPacket<float>> GetOrCreate(Guid sensorId)
    {
        if (!_trees.TryGetValue(sensorId, out var tree))
        {
            tree = new AvlTree<DateTime, TelemetryPacket<float>>();
            _trees[sensorId] = tree;
        }
        return tree;
    }

    private static void Insert(AvlTree<DateTime, TelemetryPacket<float>> tree, TelemetryPacket<float> packet)
    {
        // Keys must be unique, so nudge by one tick if another reading has the same timestamp.
        var key = packet.Timestamp;
        while (!tree.Insert(key, packet))
        {
            key = key.AddTicks(1);
        }

        // Retention: drop the oldest reading once the archive is full.
        while (tree.Count > MaxPointsPerSensor)
        {
            tree.RemoveMin();
        }
    }
}
