using ScaleStudio.Logging;
using ScaleStudio.Scanning;
using Xunit;
namespace ScaleStudio.Tests;

public sealed class LoggingScanningTests
{
    [Fact]
    public void DevicesAndTerminatorsAreIndependent()
    {
        using var scanner = new Scanner(new() { Terminator = "\r\n" });
        var scans = new List<Scan>();
        scanner.ScanReceived += scans.Add;
        scanner.Feed("a", "12\r");
        scanner.Feed("b", "34\r\n");
        scanner.Feed("a", "\n");
        Assert.Equal(new[] { new Scan("b", "34"), new Scan("a", "12") }, scans);
    }
    [Fact]
    public void RemovedDeviceDoesNotRetainPartialScan()
    {
        using var scanner = new Scanner();
        var scans = new List<Scan>();
        scanner.ScanReceived += scans.Add;
        scanner.Feed("a", "old");
        scanner.RemoveDevice("a");
        scanner.Feed("a", "new\r");
        Assert.Equal("new", Assert.Single(scans).Text);
    }
    [Fact]
    public async Task ClosingDrainsQueueAndFiltersLevels()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var logger = new FileLogger(new() { Directory = directory });
            logger.Log(LogLevel.Debug, "filtered");
            for (var i = 0; i < 100; i++) logger.Log(LogLevel.Info, $"entry {i}");
            await logger.DisposeAsync();
            var text = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(directory, "*.log", SearchOption.AllDirectories)));
            Assert.DoesNotContain("filtered", text);
            Assert.Equal(100, text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
