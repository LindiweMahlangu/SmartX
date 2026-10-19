using SmartX.Shared.Models;

namespace SmartX.API.Services;

public class RecommendationEngine : IRecommendationEngine
{
    private static readonly TimeSpan LinkWindow = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan TriggerLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RiskWindow = TimeSpan.FromMinutes(5);
    private const int MinSupport = 2;
    private const int MinAnomaliesToFlag = 3;
    private const int MaxProblemDevices = 3;

    private readonly ISensorRepository _sensors;
    private readonly object _lock = new();

    // trigger -> (action -> times the operator did it shortly after)
    private readonly Dictionary<string, Dictionary<string, int>> _transitions = new();
    private readonly Dictionary<string, ActionTemplate> _templates = new();
    private readonly Dictionary<string, DateTime> _lastActionAt = new();
    private readonly Queue<RecentTrigger> _recentTriggers = new();
    private readonly Dictionary<Guid, Queue<DateTime>> _anomalyTimes = new();

    private record RecentTrigger(string Key, string Mac, string Condition, DateTime At);

    public RecommendationEngine(ISensorRepository sensors)
    {
        _sensors = sensors;
    }

    public void RecordTrigger(AlertEvent alert)
    {
        lock (_lock)
        {
            PruneTriggers(DateTime.UtcNow);
            _recentTriggers.Enqueue(new RecentTrigger(
                $"{alert.MacAddress}|{alert.Condition}", alert.MacAddress, alert.Condition, alert.RaisedAt));
        }
    }

    public void RecordAnomaly(Guid sensorId, DateTime at)
    {
        lock (_lock)
        {
            if (!_anomalyTimes.TryGetValue(sensorId, out var times))
            {
                times = new Queue<DateTime>();
                _anomalyTimes[sensorId] = times;
            }

            times.Enqueue(at);
            while (times.Count > 200)
            {
                times.Dequeue();
            }
        }
    }

    // Pattern analysis: link what the operator just did to every alert from the last 2 minutes.
    public void RecordAction(string actionKey, ActionTemplate template)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            PruneTriggers(now);

            _templates[actionKey] = template;
            _lastActionAt[actionKey] = now;

            var linkedTriggers = _recentTriggers
                .Where(t => now - t.At <= LinkWindow)
                .Select(t => t.Key)
                .Distinct()
                .ToList();

            foreach (var trigger in linkedTriggers)
            {
                Increment(trigger, actionKey, 1);
            }
        }
    }

    public void SeedPattern(string triggerKey, string actionKey, ActionTemplate template, int occurrences)
    {
        lock (_lock)
        {
            _templates[actionKey] = template;
            Increment(triggerKey, actionKey, occurrences);
        }
    }

    public InsightsResponse GetInsights()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            PruneTriggers(now);
            return new InsightsResponse(BuildSuggestions(), BuildProblemDevices(now));
        }
    }

    // Algorithmic suggestion: most frequent follow-ups for each recent alert, with confidence.
    private List<Suggestion> BuildSuggestions()
    {
        var suggestions = new List<Suggestion>();
        var alreadySuggested = new HashSet<string>();

        foreach (var trigger in _recentTriggers.Reverse())
        {
            if (!_transitions.TryGetValue(trigger.Key, out var actions))
            {
                continue;
            }

            int total = actions.Values.Sum();

            foreach (var pair in actions.OrderByDescending(a => a.Value).Take(2))
            {
                if (pair.Value < MinSupport || !_templates.TryGetValue(pair.Key, out var template))
                {
                    continue;
                }

                // Already done since this alert fired, so nothing left to suggest.
                if (_lastActionAt.TryGetValue(pair.Key, out var lastDone) && lastDone >= trigger.At)
                {
                    continue;
                }

                if (!alreadySuggested.Add(pair.Key))
                {
                    continue;
                }

                suggestions.Add(new Suggestion(
                    template.Label,
                    $"After {trigger.Mac} reported {trigger.Condition}, operators did this {pair.Value} of {total} times.",
                    Math.Round((double)pair.Value / total, 2),
                    template.Command,
                    template.SearchText));
            }
        }

        return suggestions.OrderByDescending(s => s.Confidence).Take(5).ToList();
    }

    // Problem devices: rank by anomalies in the last 5 minutes using a PriorityQueue as a max-heap.
    private List<ProblemDevice> BuildProblemDevices(DateTime now)
    {
        var heap = new PriorityQueue<(Guid Id, int Count), int>();

        foreach (var entry in _anomalyTimes)
        {
            var times = entry.Value;
            while (times.Count > 0 && now - times.Peek() > RiskWindow)
            {
                times.Dequeue();
            }

            if (times.Count >= MinAnomaliesToFlag)
            {
                heap.Enqueue((entry.Key, times.Count), -times.Count);
            }
        }

        var problems = new List<ProblemDevice>();
        while (heap.Count > 0 && problems.Count < MaxProblemDevices)
        {
            var worst = heap.Dequeue();
            var mac = _sensors.Get(worst.Id)?.MacAddress ?? worst.Id.ToString();
            problems.Add(new ProblemDevice(
                worst.Id, mac, worst.Count,
                $"{worst.Count} anomalies in the last {(int)RiskWindow.TotalMinutes} minutes. Worth inspecting."));
        }

        return problems;
    }

    private void Increment(string trigger, string actionKey, int amount)
    {
        if (!_transitions.TryGetValue(trigger, out var actions))
        {
            actions = new Dictionary<string, int>();
            _transitions[trigger] = actions;
        }

        actions.TryGetValue(actionKey, out var current);
        actions[actionKey] = current + amount;
    }

    private void PruneTriggers(DateTime now)
    {
        while (_recentTriggers.Count > 0 && now - _recentTriggers.Peek().At > TriggerLifetime)
        {
            _recentTriggers.Dequeue();
        }
    }
}
