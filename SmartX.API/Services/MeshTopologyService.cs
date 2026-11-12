using SmartX.API.DataStructures;
using SmartX.Shared.Models;

namespace SmartX.API.Services;

public class MeshTopologyService : IMeshTopologyService
{
    public const string GatewayId = "AA:BB:CC:00:00:00";

    private sealed class NodeInfo
    {
        public string Name = string.Empty;
        public MeshNodeKind Kind;
        public double X;
        public double Y;
        public double StartBattery = 100;
        public double DrainPerMinute;
        public DateTime BatterySetAt = DateTime.UtcNow;
        public bool ManuallyFailed;
    }

    private readonly MeshGraph _graph = new();
    private readonly Dictionary<string, NodeInfo> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDeviceStateService _deviceState;
    private readonly object _lock = new();
    private int _autoPlaced;

    public MeshTopologyService(IDeviceStateService deviceState)
    {
        _deviceState = deviceState;
        SeedMesh();
    }

    private void SeedMesh()
    {
        AddNode(GatewayId, "Gateway Hub", MeshNodeKind.Gateway, 580, 170, 100, 0);

        AddNode("AA:BB:CC:DD:EE:11", "Relay 1", MeshNodeKind.Relay, 440, 90, 88, 0.05);
        AddNode("AA:BB:CC:DD:EE:12", "Relay 2", MeshNodeKind.Relay, 440, 250, 64, 0.08);
        AddNode("AA:BB:CC:DD:EE:13", "Relay 3", MeshNodeKind.Relay, 290, 90, 41, 0.10);
        AddNode("AA:BB:CC:DD:EE:14", "Relay 4", MeshNodeKind.Relay, 290, 250, 23, 0.12);

        // The three seeded sensors (same MACs the simulator registers).
        AddNode("AA:BB:CC:DD:EE:01", "Node A (moisture)", MeshNodeKind.Sensor, 110, 70, 77, 0.06);
        AddNode("AA:BB:CC:DD:EE:02", "Node B (power)", MeshNodeKind.Sensor, 110, 170, 35, 0.09);
        AddNode("AA:BB:CC:DD:EE:03", "Node C (pump)", MeshNodeKind.Sensor, 110, 270, 58, 0.07);

        // Links and their cost (lower = faster / cheaper to transmit).
        _graph.AddEdge(GatewayId, "AA:BB:CC:DD:EE:11", 2);
        _graph.AddEdge(GatewayId, "AA:BB:CC:DD:EE:12", 5);
        _graph.AddEdge("AA:BB:CC:DD:EE:11", "AA:BB:CC:DD:EE:12", 2);
        _graph.AddEdge("AA:BB:CC:DD:EE:11", "AA:BB:CC:DD:EE:13", 3);
        _graph.AddEdge("AA:BB:CC:DD:EE:12", "AA:BB:CC:DD:EE:14", 3);
        _graph.AddEdge("AA:BB:CC:DD:EE:13", "AA:BB:CC:DD:EE:14", 4);
        _graph.AddEdge("AA:BB:CC:DD:EE:13", "AA:BB:CC:DD:EE:01", 2);
        _graph.AddEdge("AA:BB:CC:DD:EE:14", "AA:BB:CC:DD:EE:02", 2);
        _graph.AddEdge("AA:BB:CC:DD:EE:14", "AA:BB:CC:DD:EE:03", 3);
        _graph.AddEdge("AA:BB:CC:DD:EE:01", "AA:BB:CC:DD:EE:02", 4);
        _graph.AddEdge("AA:BB:CC:DD:EE:02", "AA:BB:CC:DD:EE:03", 5);
        _graph.AddEdge("AA:BB:CC:DD:EE:13", "AA:BB:CC:DD:EE:02", 6);
    }

    private void AddNode(string id, string name, MeshNodeKind kind, double x, double y, double battery, double drain)
    {
        _nodes[id] = new NodeInfo
        {
            Name = name,
            Kind = kind,
            X = x,
            Y = y,
            StartBattery = battery,
            DrainPerMinute = drain,
            BatterySetAt = DateTime.UtcNow
        };
        _graph.AddNode(id);
    }

    /// A newly registered sensor joins the mesh, linked to Relay 4 and Relay 2.
    public void RegisterSensor(SensorProfile sensor)
    {
        lock (_lock)
        {
            if (_nodes.ContainsKey(sensor.MacAddress)) return;

            _autoPlaced++;
            var suffix = sensor.MacAddress.Length >= 2 ? sensor.MacAddress[^2..] : sensor.MacAddress;
            double x = 50 + ((_autoPlaced - 1) % 6) * 100;

            AddNode(sensor.MacAddress, $"Sensor {suffix}", MeshNodeKind.Sensor, x, 335, 100, 0.10);
            _graph.AddEdge(sensor.MacAddress, "AA:BB:CC:DD:EE:14", 3);
            _graph.AddEdge(sensor.MacAddress, "AA:BB:CC:DD:EE:12", 5);
        }
    }

    public bool SetNodeFailed(string macAddress, bool failed)
    {
        lock (_lock)
        {
            if (!_nodes.TryGetValue(macAddress, out var info) || info.Kind == MeshNodeKind.Gateway)
            {
                return false;
            }

            info.ManuallyFailed = failed;
            if (!failed)
            {
                // Restoring a node also means repairing it / replacing its battery.
                info.StartBattery = 100;
                info.BatterySetAt = DateTime.UtcNow;
            }
            return true;
        }
    }

    public MeshTopologyDto GetTopology()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var usable = BuildUsability(now);

            var reachable = _graph.ReachableFrom(GatewayId, usable);
            var spanning = _graph.MinimumSpanningTree(usable, out int spanningCost);
            var spanningKeys = new HashSet<string>(
                spanning.Select(e => EdgeKey(e.A, e.B)), StringComparer.OrdinalIgnoreCase);

            var nodes = _nodes
                .Select(pair => ToDto(pair.Key, pair.Value, usable, reachable, now))
                .ToList();

            var links = _graph.Edges
                .Select(e => new MeshLinkDto(
                    e.A, e.B, e.Cost, usable(e.A) && usable(e.B), spanningKeys.Contains(EdgeKey(e.A, e.B))))
                .ToList();

            var isolated = nodes.Where(n => n.IsOnline && !n.Reachable).Select(n => n.Id).ToList();

            // Low-battery watchlist: a min-heap pops the weakest batteries first.
            var heap = new BinaryMinHeap<MeshNodeDto>();
            foreach (var node in nodes.Where(n => n.Kind != MeshNodeKind.Gateway))
            {
                heap.Push(node, node.BatteryPercent);
            }

            var watchlist = new List<MeshNodeDto>();
            while (heap.Count > 0 && watchlist.Count < 3)
            {
                watchlist.Add(heap.Pop().Item);
            }

            return new MeshTopologyDto(nodes, links, spanningCost, isolated, watchlist);
        }
    }

    public RouteResultDto GetRoute(string macAddress)
    {
        lock (_lock)
        {
            var none = new List<string>();

            if (!_nodes.TryGetValue(macAddress, out var info))
            {
                return new RouteResultDto(macAddress, macAddress, false,
                    "No node with that MAC address exists in the mesh.", none, none, 0, 0, none, none, 0);
            }

            var usable = BuildUsability(DateTime.UtcNow);

            if (!usable(macAddress))
            {
                return new RouteResultDto(macAddress, info.Name, false,
                    $"{info.Name} is offline, so it cannot send or relay packets.", none, none, 0, 0, none, none, 0);
            }

            var cheapest = _graph.ShortestPath(macAddress, GatewayId, usable);
            if (cheapest is null)
            {
                return new RouteResultDto(macAddress, info.Name, false,
                    $"{info.Name} is online but cut off: no working path leads to the gateway.", none, none, 0, 0, none, none, 0);
            }

            var fewest = _graph.FewestHops(macAddress, GatewayId, usable) ?? cheapest;

            return new RouteResultDto(
                macAddress, info.Name, true, "Route found.",
                cheapest.Path, NamesOf(cheapest.Path), cheapest.TotalCost, cheapest.Path.Count - 1,
                fewest.Path, NamesOf(fewest.Path), fewest.TotalCost);
        }
    }

    //The heap ranks nodes by time-to-empty. Each relay is removed from the graph and every sensor is routed again.
    public MeshInsightsDto GetInsights()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var usable = BuildUsability(now);

            var sensors = _nodes
                .Where(n => n.Value.Kind == MeshNodeKind.Sensor && usable(n.Key))
                .Select(n => n.Key).ToList();

            var baseline = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var sensorId in sensors)
            {
                var path = _graph.ShortestPath(sensorId, GatewayId, usable);
                if (path is not null) baseline[sensorId] = path.TotalCost;
            }

            // 1 and 2: per-node risk, ranked by the heap (soonest to run flat first).
            var heap = new BinaryMinHeap<NodeRiskDto>();
            foreach (var pair in _nodes.Where(n => n.Value.Kind != MeshNodeKind.Gateway))
            {
                var id = pair.Key;
                var info = pair.Value;
                if (!usable(id)) continue;

                double battery = BatteryOf(info, now);
                int? minutesLeft = info.DrainPerMinute > 0 ? (int)Math.Round(battery / info.DrainPerMinute) : null;

                int cutOff = 0, extra = 0;
                if (info.Kind == MeshNodeKind.Relay)
                    (cutOff, extra) = ImpactOfLosing(new[] { id }, sensors, baseline, usable);

                var risk = new NodeRiskDto(id, info.Name, info.Kind, (int)Math.Round(battery),
                                           minutesLeft, cutOff, extra, LevelFor(minutesLeft, cutOff));
                heap.Push(risk, minutesLeft ?? double.MaxValue);
            }

            var risks = new List<NodeRiskDto>();
            while (heap.Count > 0) risks.Add(heap.Pop().Item);

            // 3: every pair of relays.
            var relays = _nodes.Where(n => n.Value.Kind == MeshNodeKind.Relay && usable(n.Key))
                               .Select(n => n.Key).ToList();
            int pairCount = 0, isolatingPairs = 0;
            for (int a = 0; a < relays.Count; a++)
                for (int b = a + 1; b < relays.Count; b++)
                {
                    pairCount++;
                    var (pairCutOff, _) = ImpactOfLosing(new[] { relays[a], relays[b] }, sensors, baseline, usable);
                    if (sensors.Count > 0 && pairCutOff == sensors.Count) isolatingPairs++;
                }

            return new MeshInsightsDto(risks, BuildSuggestions(risks, isolatingPairs, pairCount), pairCount, isolatingPairs);
        }
    }

    private (int CutOff, int ExtraCost) ImpactOfLosing(
        IReadOnlyCollection<string> lost, List<string> sensors, Dictionary<string, int> baseline, Func<string, bool> usable)
    {
        bool Without(string id) => usable(id) && !lost.Contains(id, StringComparer.OrdinalIgnoreCase);

        int cutOff = 0, extra = 0;
        foreach (var sensorId in sensors)
        {
            if (lost.Contains(sensorId, StringComparer.OrdinalIgnoreCase)) continue;

            var path = _graph.ShortestPath(sensorId, GatewayId, Without);
            if (path is null) cutOff++;
            else if (baseline.TryGetValue(sensorId, out var before)) extra += Math.Max(0, path.TotalCost - before);
        }
        return (cutOff, extra);
    }

    private static string LevelFor(int? minutesLeft, int cutOff)
    {
        if (minutesLeft.HasValue && minutesLeft.Value <= 60) return "critical";
        if (cutOff > 0 || (minutesLeft.HasValue && minutesLeft.Value <= 240)) return "warning";
        return "ok";
    }

    private List<MeshSuggestionDto> BuildSuggestions(List<NodeRiskDto> risks, int isolatingPairs, int pairCount)
    {
        var suggestions = new List<MeshSuggestionDto>();

        foreach (var pair in _nodes.Where(n => n.Value.ManuallyFailed))
            suggestions.Add(new MeshSuggestionDto("warning",
                $"{pair.Value.Name} is switched off. Restore it to bring its routes and battery back.", pair.Key));

        foreach (var risk in risks.Where(r => r.Level != "ok").Take(4))
        {
            string effect = risk.SensorsCutOff > 0
                ? $" and {risk.SensorsCutOff} sensor(s) would lose their only route"
                : risk.ExtraRouteCost > 0
                    ? $" and other sensors would pay {risk.ExtraRouteCost} more per route"
                    : string.Empty;

            string timeLeft = FormatDuration(risk.MinutesToEmpty);

            suggestions.Add(risk.Level == "critical"
                ? new MeshSuggestionDto("critical", $"Replace the battery on {risk.Name} now: it runs flat in about {timeLeft}{effect}.", risk.Id)
                : new MeshSuggestionDto("warning", $"Plan to replace the battery on {risk.Name} (about {timeLeft} left){effect}.", risk.Id));
        }

        if (isolatingPairs > 0)
            suggestions.Add(new MeshSuggestionDto("info",
                $"{isolatingPairs} of {pairCount} relay pairs would cut off every sensor if both failed. " +
                "A second uplink from a sensor to a different relay would remove that weak spot.", null));

        if (suggestions.Count == 0)
            suggestions.Add(new MeshSuggestionDto("info", "No action needed: every battery has more than 4 hours left.", null));

        return suggestions;
    }
    private static string FormatDuration(int? minutes)
    {
        if (!minutes.HasValue) return "an unknown time";
        if (minutes.Value < 60) return $"{minutes.Value} min";
        return $"{minutes.Value / 60} h {minutes.Value % 60} min";
    }
    // ---- helpers ----

    private Func<string, bool> BuildUsability(DateTime now)
    {
        var offlineSensors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var state in _deviceState.GetAll())
        {
            if (state.LastStatus == AnomalyStatus.Disconnected)
            {
                offlineSensors.Add(state.MacAddress);
            }
        }

        return id => _nodes.TryGetValue(id, out var info)
                     && !info.ManuallyFailed
                     && BatteryOf(info, now) > 0
                     && !offlineSensors.Contains(id);
    }

    private static double BatteryOf(NodeInfo info, DateTime now)
    {
        if (info.Kind == MeshNodeKind.Gateway) return 100; // mains powered

        var minutes = (now - info.BatterySetAt).TotalMinutes;
        return Math.Max(0, info.StartBattery - info.DrainPerMinute * minutes);
    }

    private static MeshNodeDto ToDto(
        string id, NodeInfo info, Func<string, bool> usable, HashSet<string> reachable, DateTime now) =>
        new(id, info.Name, info.Kind, info.X, info.Y, usable(id), info.ManuallyFailed,
            (int)Math.Round(BatteryOf(info, now)), reachable.Contains(id));

    private List<string> NamesOf(List<string> path) =>
        path.Select(id => _nodes.TryGetValue(id, out var info) ? info.Name : id).ToList();

    private static string EdgeKey(string a, string b) =>
        string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
}
