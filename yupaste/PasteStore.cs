namespace Yupaste;

/// <summary>Thread-safe in-memory holder for the single shared paste.</summary>
public sealed class PasteStore
{
    private readonly object _lock = new();
    private string _content = string.Empty;
    private long _version;

    public (string Content, long Version) Get()
    {
        lock (_lock)
        {
            return (_content, _version);
        }
    }

    public long Set(string content)
    {
        lock (_lock)
        {
            _content = content;
            _version++;
            return _version;
        }
    }
}
