namespace SmartX.API.Services;

public class CommandUndoService : ICommandUndoService
{
    private readonly Stack<UndoEntry> _stack = new();
    private readonly object _lock = new();

    public void Push(UndoEntry entry) { lock (_lock) { _stack.Push(entry); } }

    public UndoEntry? Pop() { lock (_lock) { return _stack.Count > 0 ? _stack.Pop() : null; } }

    public UndoEntry? Peek() { lock (_lock) { return _stack.Count > 0 ? _stack.Peek() : null; } }

    public int Count { get { lock (_lock) { return _stack.Count; } } }
}
