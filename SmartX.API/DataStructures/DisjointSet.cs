namespace SmartX.API.DataStructures;

public class DisjointSet
{
    private readonly Dictionary<string, string> _parent = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _rank = new(StringComparer.OrdinalIgnoreCase);

    public void Add(string id)
    {
        if (_parent.TryAdd(id, id))
        {
            _rank[id] = 0;
        }
    }

    public string Find(string id)
    {
        var root = id;
        while (!string.Equals(_parent[root], root, StringComparison.OrdinalIgnoreCase))
        {
            root = _parent[root];
        }

        // Path compression: point everything on the way up straight at the root.
        var current = id;
        while (!string.Equals(current, root, StringComparison.OrdinalIgnoreCase))
        {
            var next = _parent[current];
            _parent[current] = root;
            current = next;
        }

        return root;
    }

    /// Joins two sets. Returns false if they were already the same set.
    public bool Union(string a, string b)
    {
        var rootA = Find(a);
        var rootB = Find(b);
        if (string.Equals(rootA, rootB, StringComparison.OrdinalIgnoreCase)) return false;

        if (_rank[rootA] < _rank[rootB])
        {
            _parent[rootA] = rootB;
        }
        else if (_rank[rootA] > _rank[rootB])
        {
            _parent[rootB] = rootA;
        }
        else
        {
            _parent[rootB] = rootA;
            _rank[rootA]++;
        }

        return true;
    }
}
