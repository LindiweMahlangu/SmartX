namespace SmartX.API.DataStructures;

public record MeshEdge(string A, string B, int Cost)
{
    public string Other(string id) => string.Equals(id, A, StringComparison.OrdinalIgnoreCase) ? B : A;
}

public record PathResult(List<string> Path, int TotalCost);

/// An undirected weighted graph held as an adjacency list. Every algorithm takes an
/// "isUsable" test so failed or offline nodes are skipped, which is what makes the mesh self-healing.
public class MeshGraph
{
    private readonly Dictionary<string, List<MeshEdge>> _adjacency = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MeshEdge> _edges = new();

    public IReadOnlyList<MeshEdge> Edges => _edges;
    public IEnumerable<string> NodeIds => _adjacency.Keys;

    public void AddNode(string id)
    {
        if (!_adjacency.ContainsKey(id))
        {
            _adjacency[id] = new List<MeshEdge>();
        }
    }

    public void AddEdge(string a, string b, int cost)
    {
        AddNode(a);
        AddNode(b);

        var edge = new MeshEdge(a, b, cost);
        _edges.Add(edge);
        _adjacency[a].Add(edge);
        _adjacency[b].Add(edge);
    }

    /// Dijkstra's algorithm: the cheapest path by total link cost.
    public PathResult? ShortestPath(string from, string to, Func<string, bool> isUsable)
    {
        if (!_adjacency.ContainsKey(from) || !_adjacency.ContainsKey(to)) return null;
        if (!isUsable(from) || !isUsable(to)) return null;

        var distance = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [from] = 0 };
        var previous = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var heap = new BinaryMinHeap<string>();
        heap.Push(from, 0);

        while (heap.Count > 0)
        {
            var (node, priority) = heap.Pop();
            int settled = (int)priority;

            if (settled > distance[node]) continue; // an older, longer entry for this node
            if (string.Equals(node, to, StringComparison.OrdinalIgnoreCase)) break;

            foreach (var edge in _adjacency[node])
            {
                var next = edge.Other(node);
                if (!isUsable(next)) continue;

                int candidate = settled + edge.Cost;
                if (!distance.TryGetValue(next, out var known) || candidate < known)
                {
                    distance[next] = candidate;
                    previous[next] = node;
                    heap.Push(next, candidate);
                }
            }
        }

        if (!distance.ContainsKey(to)) return null;

        var path = new List<string>();
        string? current = to;
        while (current is not null)
        {
            path.Add(current);
            previous.TryGetValue(current, out var step);
            current = step;
        }
        path.Reverse();

        return new PathResult(path, distance[to]);
    }

    /// Breadth-first search: the route with the fewest hops, ignoring link cost.
    public PathResult? FewestHops(string from, string to, Func<string, bool> isUsable)
    {
        if (!_adjacency.ContainsKey(from) || !_adjacency.ContainsKey(to)) return null;
        if (!isUsable(from) || !isUsable(to)) return null;

        var previous = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { from };
        var queue = new Queue<string>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (string.Equals(node, to, StringComparison.OrdinalIgnoreCase)) break;

            foreach (var edge in _adjacency[node])
            {
                var next = edge.Other(node);
                if (!isUsable(next) || !visited.Add(next)) continue;

                previous[next] = node;
                queue.Enqueue(next);
            }
        }

        if (!visited.Contains(to)) return null;

        var path = new List<string>();
        string? current = to;
        while (current is not null)
        {
            path.Add(current);
            previous.TryGetValue(current, out var step);
            current = step;
        }
        path.Reverse();

        return new PathResult(path, CostOf(path));
    }

    /// Every usable node that can still reach 'start' (breadth-first).
    public HashSet<string> ReachableFrom(string start, Func<string, bool> isUsable)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!_adjacency.ContainsKey(start) || !isUsable(start)) return visited;

        var queue = new Queue<string>();
        visited.Add(start);
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            foreach (var edge in _adjacency[node])
            {
                var next = edge.Other(node);
                if (isUsable(next) && visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return visited;
    }

    /// Kruskal's algorithm: take links cheapest first and keep each one unless it would close a loop.
    /// The result is the lowest-total-cost set of links that still connects every usable node.
    public List<MeshEdge> MinimumSpanningTree(Func<string, bool> isUsable, out int totalCost)
    {
        var sets = new DisjointSet();
        foreach (var id in _adjacency.Keys.Where(isUsable))
        {
            sets.Add(id);
        }

        var tree = new List<MeshEdge>();
        totalCost = 0;

        foreach (var edge in _edges.Where(e => isUsable(e.A) && isUsable(e.B)).OrderBy(e => e.Cost))
        {
            if (sets.Union(edge.A, edge.B))
            {
                tree.Add(edge);
                totalCost += edge.Cost;
            }
        }

        return tree;
    }

    private int CostOf(List<string> path)
    {
        int total = 0;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            var edge = _adjacency[path[i]].First(e =>
                string.Equals(e.Other(path[i]), path[i + 1], StringComparison.OrdinalIgnoreCase));
            total += edge.Cost;
        }
        return total;
    }
}
