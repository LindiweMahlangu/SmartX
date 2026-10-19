using SmartX.Shared.Models;

namespace SmartX.API.Services;

public class TelemetryQueueService : ITelemetryQueueService
{
    private readonly Queue<TelemetryPacket<float>> _standard = new();
    private readonly PriorityQueue<TelemetryPacket<float>, (int Level, long Sequence)> _priority = new();
    private readonly object _lock = new();
    private long _sequence;
    private long _processedStandard;
    private long _processedPriority;

    public bool Enqueue(TelemetryPacket<float> packet, SensorCategory category)
    {
        lock (_lock)
        {
            if (CriticalAlertClassifier.TryGetSeverityLevel(category, packet.Value, packet.Status, out var level))
            {
                _priority.Enqueue(packet, (level, _sequence++));
                return true;
            }

            _standard.Enqueue(packet);
            return false;
        }
    }

    public bool TryDequeue(out TelemetryPacket<float>? packet, out bool wasPriority)
    {
        lock (_lock)
        {
            if (_priority.TryDequeue(out var urgent, out _))
            {
                packet = urgent;
                wasPriority = true;
                _processedPriority++;
                return true;
            }

            if (_standard.TryDequeue(out var normal))
            {
                packet = normal;
                wasPriority = false;
                _processedStandard++;
                return true;
            }

            packet = null;
            wasPriority = false;
            return false;
        }
    }

    public QueueStats GetStats()
    {
        lock (_lock)
        {
            return new QueueStats(_standard.Count, _priority.Count, _processedStandard, _processedPriority);
        }
    }
}
