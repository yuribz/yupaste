namespace Yupaste;

public sealed record StoredFile(string Id, string Name, byte[] Content);

/// <summary>
/// Thread-safe in-memory holder for hosted files. Space is reserved before an upload
/// allocates its buffer, so the used total covers in-flight uploads and concurrent
/// uploads can't exceed the limit.
/// </summary>
public sealed class FileStore
{
    public const long DefaultLimitBytes = 64L * 1024 * 1024;
    public const long LargeLimitBytes = 128L * 1024 * 1024;

    private readonly object _lock = new();
    private readonly List<StoredFile> _files = new();
    private long _usedBytes;
    private long _limitBytes = DefaultLimitBytes;

    public (IReadOnlyList<StoredFile> Files, long UsedBytes, long LimitBytes) Snapshot()
    {
        lock (_lock)
        {
            return (_files.ToArray(), _usedBytes, _limitBytes);
        }
    }

    public bool TryReserve(long bytes)
    {
        lock (_lock)
        {
            if (bytes > _limitBytes - _usedBytes) return false;
            _usedBytes += bytes;
            return true;
        }
    }

    public void Release(long bytes)
    {
        lock (_lock)
        {
            _usedBytes -= bytes;
        }
    }

    /// <summary>Stores a file whose length was already reserved with <see cref="TryReserve"/>.</summary>
    public StoredFile Add(string requestedName, byte[] content)
    {
        lock (_lock)
        {
            var file = new StoredFile(Guid.NewGuid().ToString("N"), MakeUniqueName(CleanName(requestedName)), content);
            _files.Add(file);
            return file;
        }
    }

    public bool Remove(string id)
    {
        lock (_lock)
        {
            int index = _files.FindIndex(f => f.Id == id);
            if (index < 0) return false;

            _usedBytes -= _files[index].Content.Length;
            _files.RemoveAt(index);
            return true;
        }
    }

    public bool TrySetLimit(long bytes)
    {
        lock (_lock)
        {
            if (_usedBytes > bytes) return false;
            _limitBytes = bytes;
            return true;
        }
    }

    private static string CleanName(string name)
    {
        string baseName = name.Replace('\\', '/').Split('/')[^1];
        string cleaned = new string(baseName.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return cleaned is "" or "." or ".." ? "file" : cleaned;
    }

    // Case-insensitive because zip entries differing only by case collide on Windows and macOS.
    private string MakeUniqueName(string name)
    {
        string stem = Path.GetFileNameWithoutExtension(name);
        string extension = Path.GetExtension(name);
        string candidate = name;

        for (int i = 1; _files.Any(f => f.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)); i++)
        {
            candidate = $"{stem} ({i}){extension}";
        }

        return candidate;
    }
}
