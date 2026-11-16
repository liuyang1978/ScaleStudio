using System.IO.Ports;
using ScaleStudio.Weighing;

namespace ScaleStudio.Desktop;

public sealed record SerialOptions(string PortName,int BaudRate=9600,int DataBits=8,
    Parity Parity=Parity.None,StopBits StopBits=StopBits.One);

/// <summary>Desktop-only serial adapter. Bounded synchronous reads avoid indefinite Windows serial cancellation.</summary>
public sealed class SerialWeightTransport(SerialOptions options) : IWeightTransport
{
    private SerialPort? _port;
    public Task ConnectAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_port != null) throw new InvalidOperationException("Already connected.");
        var port = new SerialPort(options.PortName, options.BaudRate, options.Parity, options.DataBits, options.StopBits)
        { ReadTimeout=200,WriteTimeout=500 };
        try { port.Open(); _port=port; return Task.CompletedTask; }
        catch { port.Dispose(); throw; }
    }
    public async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct)
    {
        if (buffer.IsEmpty) throw new ArgumentException("Empty buffer.");
        var port = _port ?? throw new InvalidOperationException("Not connected.");
        var temp = new byte[buffer.Length];
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                int count = await Task.Run(()=>port.Read(temp,0,temp.Length),ct);
                if (count > 0) { temp.AsMemory(0,count).CopyTo(buffer); return count; }
            }
            catch (TimeoutException) { }
        }
    }
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken ct)
    {
        var port = _port ?? throw new InvalidOperationException("Not connected.");
        var bytes = buffer.ToArray();
        await Task.Run(()=>port.Write(bytes,0,bytes.Length),ct);
    }
    public ValueTask DisposeAsync()
    {
        _port?.Dispose(); _port=null;
        return ValueTask.CompletedTask;
    }
}
