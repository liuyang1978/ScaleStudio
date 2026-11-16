using System.Threading.Channels;
using System.Globalization;
namespace ScaleStudio.Logging;

public enum LogLevel { Trace, Debug, Info, Warning, Error, Fatal }
public sealed record LoggingOptions
{
    public string Directory { get; init; } = "Logs";
    public LogLevel MinimumLevel { get; init; } = LogLevel.Info;
    public bool Hourly { get; init; } = true;
    public int RetentionDays { get; init; } = 30;
}

public sealed class FileLogger : IAsyncDisposable
{
    private sealed record Entry(DateTimeOffset Time, LogLevel Level, string Message);
    private readonly LoggingOptions options;
    private readonly Channel<Entry> queue = Channel.CreateUnbounded<Entry>(new() { SingleReader = true });
    private readonly Task worker;
    public FileLogger(LoggingOptions options)
    {
        if (options.RetentionDays < 1) throw new ArgumentOutOfRangeException(nameof(options));
        this.options = options with { Directory = Path.GetFullPath(options.Directory) };
        worker = RunAsync();
    }
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (level < options.MinimumLevel) return;
        if (!queue.Writer.TryWrite(new(DateTimeOffset.Now, level, message + (exception is null ? "" : Environment.NewLine + exception))))
            throw new ObjectDisposedException(nameof(FileLogger));
    }
    private async Task RunAsync()
    {
        DateOnly? cleaned = null;
        await foreach (var entry in queue.Reader.ReadAllAsync())
        {
            var day = DateOnly.FromDateTime(entry.Time.Date);
            if (cleaned != day) { Cleanup(entry.Time); cleaned = day; }
            var directory = Path.Combine(options.Directory, entry.Time.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));
            System.IO.Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, entry.Time.ToString(options.Hourly ? "yyyyMMdd_HH" : "yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
            await File.AppendAllTextAsync(file, $"{entry.Time:O} [{entry.Level}] {entry.Message}{Environment.NewLine}");
        }
    }
    private void Cleanup(DateTimeOffset now)
    {
        if (!System.IO.Directory.Exists(options.Directory)) return;
        foreach (var file in System.IO.Directory.EnumerateFiles(options.Directory, "*.log", SearchOption.AllDirectories))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (DateTime.TryParseExact(name, new[] { "yyyyMMdd", "yyyyMMdd_HH" }, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date) && date.Date < now.Date.AddDays(-options.RetentionDays))
                File.Delete(file);
        }
    }
    public async ValueTask DisposeAsync()
    {
        queue.Writer.TryComplete();
        await worker;
    }
}
