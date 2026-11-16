using System.Text;
namespace ScaleStudio.Scanning;

public sealed record Scan(string DeviceId, string Text);
public sealed record ScanOptions
{
    public string Terminator { get; init; } = "\r";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMilliseconds(100);
    public int MaximumLength { get; init; } = 4096;
}
public sealed class Scanner : IDisposable
{
    private sealed class Buffer { public StringBuilder Text = new(); public long Last; }
    private readonly Dictionary<string, Buffer> buffers = new();
    private readonly ScanOptions options;
    private readonly TimeProvider clock;
    private readonly ITimer timer;
    private bool disposed;
    public event Action<Scan>? ScanReceived;
    public Scanner(ScanOptions? options = null, TimeProvider? clock = null)
    {
        this.options = options ?? new();
        this.clock = clock ?? TimeProvider.System;
        if (string.IsNullOrEmpty(this.options.Terminator) || this.options.Timeout <= TimeSpan.Zero || this.options.MaximumLength < this.options.Terminator.Length)
            throw new ArgumentException("Invalid scan options.");
        timer = this.clock.CreateTimer(_ => Expire(), null, this.options.Timeout, this.options.Timeout);
    }
    public void Feed(string deviceId, string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);
        List<Scan> completed = new();
        lock (buffers)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Expire();
            if (!buffers.TryGetValue(deviceId, out var buffer)) buffers[deviceId] = buffer = new();
            foreach (var ch in text)
            {
                buffer.Last = clock.GetTimestamp();
                buffer.Text.Append(ch);
                if (buffer.Text.ToString().EndsWith(options.Terminator, StringComparison.Ordinal))
                {
                    var value = buffer.Text.ToString(0, buffer.Text.Length - options.Terminator.Length);
                    buffer.Text.Clear();
                    if (value.Length > 0) completed.Add(new(deviceId, value));
                }
                else if (buffer.Text.Length > options.MaximumLength) buffer.Text.Clear();
            }
        }
        foreach (var scan in completed) ScanReceived?.Invoke(scan);
    }
    public void RemoveDevice(string deviceId) { lock (buffers) buffers.Remove(deviceId); }
    private void Expire()
    {
        lock (buffers)
            foreach (var key in buffers.Where(p => clock.GetElapsedTime(p.Value.Last) >= options.Timeout).Select(p => p.Key).ToArray())
                buffers.Remove(key);
    }
    public void Dispose()
    {
        lock (buffers) { disposed = true; buffers.Clear(); ScanReceived = null; }
        timer.Dispose();
    }
}
