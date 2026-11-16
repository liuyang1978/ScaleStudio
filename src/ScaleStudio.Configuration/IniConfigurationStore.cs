using System.Text;
using System.Collections.Concurrent;

namespace ScaleStudio.Configuration;

/// <summary>Single [settings] section; values are escaped to preserve newlines and backslashes.
/// Synchronizes writers within this process. Not a general-purpose INI editor.</summary>
public sealed class IniConfigurationStore : IConfigurationStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly string _path;
    private readonly SemaphoreSlim _gate;

    public IniConfigurationStore(string path)
    {
        _path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _gate = Locks.GetOrAdd(_path, _ => new SemaphoreSlim(1, 1));
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Any(c => !char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-'))
            throw new ArgumentException("Invalid configuration key.", nameof(key));
    }

    private async Task<Dictionary<string,string>> ReadAsync(CancellationToken ct)
    {
        var items = new Dictionary<string,string>(StringComparer.Ordinal);
        if (!File.Exists(_path)) return items;
        foreach (var line in await File.ReadAllLinesAsync(_path, ct))
        {
            var i = line.IndexOf('=');
            if (i <= 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            items[line[..i].Trim()] = Uri.UnescapeDataString(line[(i+1)..]);
        }
        return items;
    }

    private async Task WriteAsync(Dictionary<string,string> items, CancellationToken ct)
    {
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllLinesAsync(temp, new[]{"[settings]"}.Concat(items.OrderBy(x=>x.Key)
                .Select(x=>x.Key+"="+Uri.EscapeDataString(x.Value))), new UTF8Encoding(false), ct);
            ct.ThrowIfCancellationRequested();
            File.Move(temp, _path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        ValidateKey(key);
        await _gate.WaitAsync(ct);
        try { return (await ReadAsync(ct)).GetValueOrDefault(key); }
        finally { _gate.Release(); }
    }
    public Task SetAsync(string key, string value, CancellationToken ct = default) =>
        ChangeAsync(key, value, ct);
    public Task RemoveAsync(string key, CancellationToken ct = default) => ChangeAsync(key, null, ct);
    private async Task ChangeAsync(string key, string? value, CancellationToken ct)
    {
        ValidateKey(key);
        await _gate.WaitAsync(ct);
        try
        {
            var items = await ReadAsync(ct);
            if (value is null) items.Remove(key); else items[key] = value;
            await WriteAsync(items, ct);
        }
        finally { _gate.Release(); }
    }
}
