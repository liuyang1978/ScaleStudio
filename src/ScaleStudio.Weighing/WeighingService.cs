using System.Text;

namespace ScaleStudio.Weighing;

/// <summary>One running loop per instance. The transport must honor cancellation or use bounded reads.</summary>
public sealed class WeighingService : IAsyncDisposable
{
    private readonly Func<IWeightTransport> _factory;
    private readonly ProtocolOptions _options;
    private readonly WeightParser _parser;
    private readonly SemaphoreSlim _gate = new(1,1);
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private bool _disposed;
    public event Action<WeightReading>? ReadingReceived;
    public event Action<Exception?>? ConnectionChanged;

    public WeighingService(Func<IWeightTransport> factory, ProtocolOptions options)
    {
        if (options.PollIntervalMs <= 0 || options.ReconnectDelayMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(options));
        _ = new FrameDecoder(options.Delimiter, options.MaxFrameLength);
        _factory = factory; _options = options; _parser = new WeightParser(options);
    }

    public async Task StartAsync()
    {
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed,this);
            if (_loop != null) return;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _loop = Task.Run(()=>RunAsync(token));
        }
        finally { _gate.Release(); }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync();
        try { await StopCoreAsync(); }
        finally { _gate.Release(); }
    }

    private async Task StopCoreAsync()
    {
        if (_loop == null) return;
        _cts!.Cancel();
        try { await _loop; }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
        finally { _cts.Dispose(); _cts = null; _loop = null; }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var transport = _factory();
                await transport.ConnectAsync(ct);
                Notify(ConnectionChanged, null);
                using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var receive = ReceiveAsync(transport, session.Token);
                var poll = _options.Mode == ProtocolMode.Poll ? PollAsync(transport, session.Token) :
                    Task.Delay(Timeout.Infinite, session.Token);
                try { await await Task.WhenAny(receive,poll); }
                finally
                {
                    session.Cancel();
                    try { await Task.WhenAll(receive,poll); }
                    catch (OperationCanceledException) when (session.IsCancellationRequested) { }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { Notify(ConnectionChanged,ex); }
            if (!ct.IsCancellationRequested) await Task.Delay(_options.ReconnectDelayMs,ct);
        }
    }

    private async Task ReceiveAsync(IWeightTransport transport, CancellationToken ct)
    {
        var decoder = new FrameDecoder(_options.Delimiter,_options.MaxFrameLength);
        var buffer = new byte[1024];
        while (!ct.IsCancellationRequested)
        {
            int n = await transport.ReadAsync(buffer,ct);
            if (n == 0) throw new IOException("Transport disconnected.");
            foreach (var frame in decoder.Push(buffer.AsSpan(0,n)))
                if (_parser.TryParse(frame,out var reading)) Notify(ReadingReceived,reading!);
        }
    }

    private async Task PollAsync(IWeightTransport transport, CancellationToken ct)
    {
        var command = Encoding.ASCII.GetBytes(_options.PollCommand);
        while (!ct.IsCancellationRequested)
        {
            await transport.WriteAsync(command,ct);
            await Task.Delay(_options.PollIntervalMs,ct);
        }
    }

    private static void Notify<T>(Action<T>? handlers,T value)
    {
        if (handlers == null) return;
        foreach (Action<T> handler in handlers.GetInvocationList())
            try { handler(value); } catch (Exception ex) { System.Diagnostics.Trace.TraceError(ex.ToString()); }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try { if (_disposed) return; _disposed=true; await StopCoreAsync(); }
        finally { _gate.Release(); }
    }
}
