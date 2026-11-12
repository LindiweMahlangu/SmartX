namespace SmartX.API.DataStructures;

public class BinaryMinHeap<T>
{
    private readonly List<(T Item, double Priority)> _heap = new();

    public int Count => _heap.Count;

    public void Push(T item, double priority)
    {
        _heap.Add((item, priority));
        SiftUp(_heap.Count - 1);
    }

    public (T Item, double Priority) Peek()
    {
        if (_heap.Count == 0) throw new InvalidOperationException("The heap is empty.");
        return _heap[0];
    }

    public (T Item, double Priority) Pop()
    {
        if (_heap.Count == 0) throw new InvalidOperationException("The heap is empty.");

        var top = _heap[0];
        var last = _heap[_heap.Count - 1];
        _heap.RemoveAt(_heap.Count - 1);

        if (_heap.Count > 0)
        {
            _heap[0] = last;
            SiftDown(0);
        }

        return top;
    }

    private void SiftUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (_heap[index].Priority >= _heap[parent].Priority) break;

            Swap(index, parent);
            index = parent;
        }
    }

    private void SiftDown(int index)
    {
        int count = _heap.Count;
        while (true)
        {
            int left = 2 * index + 1;
            int right = left + 1;
            int smallest = index;

            if (left < count && _heap[left].Priority < _heap[smallest].Priority) smallest = left;
            if (right < count && _heap[right].Priority < _heap[smallest].Priority) smallest = right;
            if (smallest == index) break;

            Swap(index, smallest);
            index = smallest;
        }
    }

    private void Swap(int a, int b)
    {
        (_heap[a], _heap[b]) = (_heap[b], _heap[a]);
    }
}
