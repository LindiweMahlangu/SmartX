using Microsoft.AspNetCore.SignalR;
using SmartX.API.Hubs;
using SmartX.API.Services;
using SmartX.Shared.Models;
using System.Collections.Concurrent;

namespace SmartX.Api.Services;

public class TelemetryIngestionService : ITelemetryIngestionService
{
    private readonly ConcurrentDictionary<Guid, List<TelemetryPacket<float>>> _history = new();
    private readonly IHubContext<TelemetryHub> _hub;
    private const int MaxHistoryPerSensor = 500;
    private const int BaselineWindow = 20;
    private const double SpikeZScoreThreshold = 3.0;

    public TelemetryIngestionService(IHubContext<TelemetryHub> hub) => _hub = hub;

    public async Task<List<TelemetryPacket<float>>> IngestBatchesAsync(TelemetryBatchRequest request)
    {
        // Step 1: raw jagged array of historical batches (variable length per batch)
        float[][] rawBatches = request.Batches;

        // Step 2: flatten into the optimised List<T> collection
        var flushedPackets = new List<TelemetryPacket<float>>();
        for (int batchIndex = 0; batchIndex < rawBatches.Length; batchIndex++)
        {
            float[] batch = rawBatches[batchIndex];
            for (int i = 0; i < batch.Length; i++)
            {
                flushedPackets.Add(new TelemetryPacket<float>
                {
                    sensorId = request.SensorId,
                    MetricName = request.MetricName,
                    Unit = request.Unit,
                    Value = batch[i]
                });
            }
        }

        // Step 3: anomaly detection against a rolling baseline
        var sensorHistory = _history.GetOrAdd(request.SensorId, _ => new List<TelemetryPacket<float>>());
        foreach (var packet in flushedPackets)
        {
            packet.Status = DetectAnomaly(sensorHistory, packet.Value);
            sensorHistory.Add(packet);
        }

        if (sensorHistory.Count > MaxHistoryPerSensor)
            sensorHistory.RemoveRange(0, sensorHistory.Count - MaxHistoryPerSensor);

        // Step 4: push to connected dashboards in real time
        foreach (var packet in flushedPackets)
            await _hub.Clients.All.SendAsync("ReceiveTelemetry", packet);

        return flushedPackets;
    }

    public List<TelemetryPacket<float>> GetHistory(Guid sensorId) =>
        _history.TryGetValue(sensorId, out var list) ? list : new List<TelemetryPacket<float>>();

    private static AnomalyStatus DetectAnomaly(List<TelemetryPacket<float>> history, float value)
    {
        if (history.Count < 5) return AnomalyStatus.Normal;

        var recentValues = history.Skip(Math.Max(0, history.Count - BaselineWindow))
                                   .Select(p => (double)p.Value).ToList();
        double mean = recentValues.Average();
        double variance = recentValues.Sum(v => Math.Pow(v - mean, 2)) / recentValues.Count;
        double stdDev = Math.Sqrt(variance);

        if (stdDev < 0.0001) return AnomalyStatus.Normal;

        double zScore = Math.Abs(value - mean) / stdDev;
        return zScore > SpikeZScoreThreshold ? AnomalyStatus.Spike : AnomalyStatus.Normal;
    }
}