using System;
using System.Collections.Generic;
using System.Text;

namespace SmartX.Shared.Models;

/// What a physical device would POST to the gateway: it identifies itself by MAC address.
public class DeviceIngestRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public float Value { get; set; }
    public bool Disconnected { get; set; }
}

public record AlertEvent(
    Guid SensorId, string MacAddress, AnomalyStatus Status, string Condition, string Message, DateTime RaisedAt);

public record AlertSnapshot(List<AlertEvent> Active, int DuplicatesSuppressed);
public record QueueStats(int StandardDepth, int PriorityDepth, long ProcessedStandard, long ProcessedPriority);
public record UndoPreview(bool Available, string? Description, int Depth);
public record TimelinePoint(DateTime Timestamp, float Value, AnomalyStatus Status);
public record Suggestion(string Title, string Reason, double Confidence, DeviceCommandRequest? Command, string? SearchText);
public record ProblemDevice(Guid SensorId, string MacAddress, int AnomalyCount, string Summary);
public record InsightsResponse(List<Suggestion> Suggestions, List<ProblemDevice> ProblemDevices);
public record SearchEventRequest(string Text);
