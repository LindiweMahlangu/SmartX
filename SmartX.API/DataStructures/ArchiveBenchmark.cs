using System.Diagnostics;

namespace SmartX.API.DataStructures;

public record BenchmarkResult(
    int Readings,
    int TreeHeight,
    int BestPossibleHeight,
    int MaxAllowedAvlHeight,
    int MatchingReadings,
    int CountedWithoutVisiting,
    bool SameResultAsFullScan,
    int NodesVisitedByTree,
    int ReadingsCheckedByScan,
    double TreeMilliseconds,
    double ScanMilliseconds,
    string Summary);

public static class ArchiveBenchmark
{
    private const int Repeats = 20;

    public static BenchmarkResult Run(int readings)
    {
        var tree = new AvlTree<DateTime, float>();
        var all = new List<KeyValuePair<DateTime, float>>(readings);
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var rng = new Random(42);

        for (int i = 0; i < readings; i++)
        {
            var time = start.AddSeconds(i * 3L);          // one reading every 3 seconds
            var value = (float)(40 + rng.NextDouble() * 5);
            tree.Insert(time, value);
            all.Add(new KeyValuePair<DateTime, float>(time, value));
        }

        // "All readings between 14:00 and 16:00" on a day in the middle of the data.
        var middleDay = start.AddSeconds(readings * 3L / 2).Date;
        var from = middleDay.AddHours(14);
        var to = middleDay.AddHours(16);

        // Warm up so the first timing is not dominated by JIT compilation.
        tree.Range(from, to, int.MaxValue, false, out _);
        all.Where(r => r.Key >= from && r.Key <= to).ToList();

        var viaTree = new List<KeyValuePair<DateTime, float>>();
        int visited = 0;
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < Repeats; i++)
        {
            viaTree = tree.Range(from, to, int.MaxValue, false, out visited);
        }
        timer.Stop();
        double treeMs = timer.Elapsed.TotalMilliseconds / Repeats;

        var viaScan = new List<KeyValuePair<DateTime, float>>();
        timer.Restart();
        for (int i = 0; i < Repeats; i++)
        {
            viaScan = all.Where(r => r.Key >= from && r.Key <= to).ToList();
        }
        timer.Stop();
        double scanMs = timer.Elapsed.TotalMilliseconds / Repeats;

        bool same = viaTree.Count == viaScan.Count
                    && viaTree.Zip(viaScan).All(pair => pair.First.Key == pair.Second.Key);

        int counted = tree.CountRange(from, to);
        int bestHeight = (int)Math.Ceiling(Math.Log2(readings + 1));
        int allowedHeight = (int)Math.Floor(1.44 * Math.Log2(readings + 2));

        string summary =
            $"Of {readings:N0} stored readings, {viaTree.Count:N0} fall between 14:00 and 16:00. " +
            $"The AVL tree touched {visited:N0} nodes and took {treeMs:F3} ms. " +
            $"A full scan had to check all {readings:N0} readings and took {scanMs:F3} ms. " +
            $"Both found the same readings: {same}.";

        return new BenchmarkResult(
            readings, tree.Height, bestHeight, allowedHeight, viaTree.Count, counted, same,
            visited, readings, Math.Round(treeMs, 4), Math.Round(scanMs, 4), summary);
    }
}
