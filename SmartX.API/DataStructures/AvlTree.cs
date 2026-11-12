namespace SmartX.API.DataStructures;

public class AvlTree<TKey, TValue> where TKey : IComparable<TKey>
{
    private sealed class Node
    {
        public TKey Key;
        public TValue Value;
        public Node? Left;
        public Node? Right;
        public int Height = 1;
        public int Size = 1;

        public Node(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }

    private Node? _root;

    public int Count => _root?.Size ?? 0;
    public int Height => HeightOf(_root);

    /// Adds a key. Returns false (and changes nothing) if the key already exists.
    public bool Insert(TKey key, TValue value)
    {
        bool inserted = false;
        _root = Insert(_root, key, value, ref inserted);
        return inserted;
    }

    public bool TryGetValue(TKey key, out TValue? value)
    {
        var node = _root;
        while (node is not null)
        {
            int cmp = key.CompareTo(node.Key);
            if (cmp == 0)
            {
                value = node.Value;
                return true;
            }
            node = cmp < 0 ? node.Left : node.Right;
        }

        value = default;
        return false;
    }

    public KeyValuePair<TKey, TValue>? Min()
    {
        var node = _root;
        if (node is null) return null;
        while (node.Left is not null) node = node.Left;
        return new KeyValuePair<TKey, TValue>(node.Key, node.Value);
    }

    public KeyValuePair<TKey, TValue>? Max()
    {
        var node = _root;
        if (node is null) return null;
        while (node.Right is not null) node = node.Right;
        return new KeyValuePair<TKey, TValue>(node.Key, node.Value);
    }

    /// Removes the smallest key (used to drop the oldest reading).
    public bool RemoveMin()
    {
        if (_root is null) return false;
        _root = RemoveMin(_root);
        return true;
    }

    /// All entries with from <= key <= to, in key order (or reverse), stopping after 'limit'.
    /// nodesVisited reports how many nodes were touched, to show the tree is not scanning everything.
    public List<KeyValuePair<TKey, TValue>> Range(TKey from, TKey to, int limit, bool descending, out int nodesVisited)
    {
        var result = new List<KeyValuePair<TKey, TValue>>();
        int visited = 0;
        Collect(_root, from, to, limit, descending, result, ref visited);
        nodesVisited = visited;
        return result;
    }

    /// How many keys lie in [from, to], computed from subtree sizes in O(log n).
    public int CountRange(TKey from, TKey to)
    {
        int count = CountLess(_root, to, inclusive: true) - CountLess(_root, from, inclusive: false);
        return Math.Max(count, 0);
    }

    // ---- internals ----

    private static Node Insert(Node? node, TKey key, TValue value, ref bool inserted)
    {
        if (node is null)
        {
            inserted = true;
            return new Node(key, value);
        }

        int cmp = key.CompareTo(node.Key);
        if (cmp < 0) node.Left = Insert(node.Left, key, value, ref inserted);
        else if (cmp > 0) node.Right = Insert(node.Right, key, value, ref inserted);
        else return node; // duplicate key: leave the tree as it is

        return Balance(node);
    }

    private static Node? RemoveMin(Node node)
    {
        if (node.Left is null) return node.Right;
        node.Left = RemoveMin(node.Left);
        return Balance(node);
    }

    private static void Collect(
        Node? node, TKey from, TKey to, int limit, bool descending,
        List<KeyValuePair<TKey, TValue>> result, ref int visited)
    {
        if (node is null || result.Count >= limit) return;

        visited++;

        bool searchLeft = from.CompareTo(node.Key) < 0;   // smaller keys may still be in range
        bool searchRight = to.CompareTo(node.Key) > 0;    // larger keys may still be in range
        bool inRange = from.CompareTo(node.Key) <= 0 && to.CompareTo(node.Key) >= 0;

        if (descending)
        {
            if (searchRight) Collect(node.Right, from, to, limit, descending, result, ref visited);
            if (inRange && result.Count < limit) result.Add(new KeyValuePair<TKey, TValue>(node.Key, node.Value));
            if (searchLeft) Collect(node.Left, from, to, limit, descending, result, ref visited);
        }
        else
        {
            if (searchLeft) Collect(node.Left, from, to, limit, descending, result, ref visited);
            if (inRange && result.Count < limit) result.Add(new KeyValuePair<TKey, TValue>(node.Key, node.Value));
            if (searchRight) Collect(node.Right, from, to, limit, descending, result, ref visited);
        }
    }

    private static int CountLess(Node? node, TKey key, bool inclusive)
    {
        int count = 0;
        while (node is not null)
        {
            int cmp = key.CompareTo(node.Key);
            if (cmp > 0 || (inclusive && cmp == 0))
            {
                count += SizeOf(node.Left) + 1; // this node and everything left of it
                node = node.Right;
            }
            else
            {
                node = node.Left;
            }
        }
        return count;
    }

    private static int HeightOf(Node? node) => node?.Height ?? 0;
    private static int SizeOf(Node? node) => node?.Size ?? 0;

    private static void Update(Node node)
    {
        node.Height = 1 + Math.Max(HeightOf(node.Left), HeightOf(node.Right));
        node.Size = 1 + SizeOf(node.Left) + SizeOf(node.Right);
    }

    private static Node Balance(Node node)
    {
        Update(node);
        int balance = HeightOf(node.Left) - HeightOf(node.Right);

        if (balance > 1) // left side too tall
        {
            if (HeightOf(node.Left!.Left) < HeightOf(node.Left.Right))
            {
                node.Left = RotateLeft(node.Left);        // left-right case
            }
            return RotateRight(node);
        }

        if (balance < -1) // right side too tall
        {
            if (HeightOf(node.Right!.Right) < HeightOf(node.Right.Left))
            {
                node.Right = RotateRight(node.Right);     // right-left case
            }
            return RotateLeft(node);
        }

        return node;
    }

    private static Node RotateRight(Node top)
    {
        var newTop = top.Left!;
        top.Left = newTop.Right;
        newTop.Right = top;
        Update(top);
        Update(newTop);
        return newTop;
    }

    private static Node RotateLeft(Node top)
    {
        var newTop = top.Right!;
        top.Right = newTop.Left;
        newTop.Left = top;
        Update(top);
        Update(newTop);
        return newTop;
    }
}
