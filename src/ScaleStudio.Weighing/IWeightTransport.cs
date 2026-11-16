namespace ScaleStudio.Weighing;

public interface IWeightTransport : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken ct);
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct);
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct);
}

public interface IWeightTransportFactory
{
    IWeightTransport Create(ConnectionKind kind);
}

public sealed class SimulationTransport : IWeightTransport
{
    private byte[] _pending = [];
    public Task ConnectAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.CompletedTask; }
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct)
    {
        if (_pending.Length == 0)
        {
            await Task.Delay(200, ct);
            var weight = (Random.Shared.Next(1000, 5000)/100m).ToString("F2",System.Globalization.CultureInfo.InvariantCulture);
            _pending = System.Text.Encoding.ASCII.GetBytes("sg"+weight+"kg\r\n");
        }
        var count = Math.Min(buffer.Length,_pending.Length);
        _pending.AsMemory(0,count).CopyTo(buffer);
        _pending = _pending[count..];
        return count;
    }
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return ValueTask.CompletedTask; }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
