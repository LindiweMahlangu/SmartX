using Microsoft.AspNetCore.SignalR;
using SmartX.API.Hubs;
using SmartX.Shared.Models;

namespace SmartX.API.Services;

public class TelemetryIngestionService : ITelemetryIngestionService
{
    private const int BaselineWindow = 20;
    private const double SpikeZScoreThreshold = 3.0;
    private const int MaxHistoryReturned = 500;

    private readonly IHubContext<TelemetryHub> _hub;
    private readonly IDeviceStateService _deviceState;
    private readonly ISensorRepository _sensors;
    private readonly ITelemetryQueueService _queue;
    private readonly ITelemetryHistoryService _history;
    private readonly IAlertTracker _alerts;
    private readonly IRecommendationEngine _recommendations;

    public TelemetryIngestionService(
        IHubContext<TelemetryHub> hub,
        IDeviceStateService deviceState,
        ISensorRepository sensors,
        ITelemetryQueueService queue,
        ITelemetryHistoryService history,
        IAlertTracker alerts,
        IRecommendationEngine recommendations)
    {
        _hub = hub;
        _deviceState = deviceState;
        _sensors = sensors;
        _queue = queue;
        _history = history;
        _alerts = alerts;
        _recommendations = recommendations;
    }

    public Task<List<TelemetryPacket<float>>> IngestBatchesAsync(TelemetryBatchRequest request)
    {
        // Part 1 requirement kept: raw jagged array of batches -> flat List<T> of packets.
        float[][] rawBatches = request.Batches;
        var category = _sensors.Get(request.SensorId)?.Category ?? SensorCategory.Environmental;

        var flushedPackets = new List<TelemetryPacket<float>>();
        for (int batchIndex = 0; batchIndex < rawBatches.Length; batchIndex++)
        {
            float[] batch = rawBatches[batchIndex];
            for (int i = 0; i < batch.Length; i++)
            {
                flushedPackets.Add(new TelemetryPacket<float>
                {
                    SensorId = request.SensorId,
                    MetricName = request.MetricName,
                    Unit = request.Unit,
                    Value = batch[i]
                });
            }
        }

        foreach (var packet in flushedPackets)
        {
            _queue.Enqueue(packet, category);
        }

        return Task.FromResult(flushedPackets);
    }

    public async Task ProcessPacketAsync(TelemetryPacket<float> packet, bool wasPriority)
    {
        var sensor = _sensors.Get(packet.SensorId);

        if (packet.Status != AnomalyStatus.Disconnected)
        {
            var recent = _history.GetRecentValues(packet.SensorId, BaselineWindow);
            packet.Status = DetectAnomaly(recent, packet.Value);

            // A reading critical enough for the priority queue is an anomaly by definition.
            if (wasPriority && packet.Status == AnomalyStatus.Normal)
            {
                packet.Status = AnomalyStatus.Spike;
            }
        }

        _history.Add(packet);                              // SortedDictionary
        _deviceState.UpsertFromTelemetry(packet, sensor);  // latest state

        if (packet.Status != AnomalyStatus.Normal)
        {
            _recommendations.RecordAnomaly(packet.SensorId, DateTime.UtcNow);
        }

        var mac = sensor?.MacAddress ?? "unknown";
        var alert = _alerts.Evaluate(packet, mac, ConditionFor(sensor?.Category, packet, wasPriority)); // HashSet
        if (alert is not null)
        {
            _recommendations.RecordTrigger(alert);
            await _hub.Clients.All.SendAsync("ReceiveAlert", alert);
        }

        await _hub.Clients.All.SendAsync("ReceiveTelemetry", packet);
    }

    public List<TelemetryPacket<float>> GetHistory(Guid sensorId) =>
        _history.GetTimeline(sensorId, MaxHistoryReturned).ToList();

    private static string ConditionFor(SensorCategory? category, TelemetryPacket<float> packet, bool wasPriority)
    {
        if (packet.Status == AnomalyStatus.Disconnected) return "Disconnected";
        if (wasPriority) return category == SensorCategory.PowerConsumption ? "CriticalHigh" : "CriticalLow";
        return "Spike";
    }

    private static AnomalyStatus DetectAnomaly(IReadOnlyList<float> recent, float value)
    {
        if (recent.Count < 5) return AnomalyStatus.Normal;

        double mean = recent.Average(v => (double)v);
        double variance = recent.Sum(v => Math.Pow(v - mean, 2)) / recent.Count;
        double stdDev = Math.Sqrt(variance);

        if (stdDev < 0.0001) return AnomalyStatus.Normal;

        double zScore = Math.Abs(value - mean) / stdDev;
        return zScore > SpikeZScoreThreshold ? AnomalyStatus.Spike : AnomalyStatus.Normal;
    }
}