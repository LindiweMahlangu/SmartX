namespace SmartX.Shared.Models;

public enum MeshNodeKind
{
    Gateway,
    Sensor,
    Relay
}

/// One node in the mesh, with its drawing position and live health.
public record MeshNodeDto(
    string Id,
    string Name,
    MeshNodeKind Kind,
    double X,
    double Y,
    bool IsOnline,
    bool IsFailed,
    int BatteryPercent,
    bool Reachable);

/// A link between two nodes. InSpanningTree marks the minimum-spanning-tree backbone.
public record MeshLinkDto(string From, string To, int Cost, bool IsOnline, bool InSpanningTree);

public record MeshTopologyDto(
    List<MeshNodeDto> Nodes,
    List<MeshLinkDto> Links,
    int SpanningTreeCost,
    List<string> IsolatedNodeIds,
    List<MeshNodeDto> LowBatteryWatchlist);

/// The route a node's packets take to the gateway, two ways: cheapest and fewest hops.
public record RouteResultDto(
    string FromId,
    string FromName,
    bool Found,
    string Message,
    List<string> Path,
    List<string> PathNames,
    int TotalCost,
    int Hops,
    List<string> FewestHopsPath,
    List<string> FewestHopsNames,
    int FewestHopsCost);

public record HistoryEntry(DateTime Timestamp, float Value, AnomalyStatus Status);

/// Result of a time-range query against a sensor's AVL-tree archive.
public record HistoryQueryResult(
    string MacAddress,
    string MetricName,
    string Unit,
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalMatches,
    List<HistoryEntry> Entries,
    float? Min,
    float? Max,
    float? Average,
    int NodesVisited,
    int TreeSize,
    int TreeHeight);

public record NodeRiskDto(
    string Id, string Name, MeshNodeKind Kind, int BatteryPercent,
    int? MinutesToEmpty, int SensorsCutOff, int ExtraRouteCost, string Level);

public record MeshSuggestionDto(string Level, string Text, string? Mac);

public record MeshInsightsDto(
    List<NodeRiskDto> Risks, List<MeshSuggestionDto> Suggestions, int RelayPairs, int IsolatingPairs);