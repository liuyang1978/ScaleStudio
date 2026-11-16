using System.Globalization;
using System.Text.RegularExpressions;

namespace ScaleStudio.Weighing;

public enum ProtocolMode { ReceiveOnly, Poll }
public enum ConnectionKind { Serial, Bluetooth, Usb, Simulation }

public sealed record ProtocolOptions
{
    public ProtocolMode Mode { get; init; } = ProtocolMode.ReceiveOnly;
    public string Pattern { get; init; } = @"^(?<stable>sg|wg)\s*(?<weight>[+-]?\s*\d+(?:\.\d+)?)\s*(?<unit>kg|g)$";
    public string StableValue { get; init; } = "sg";
    public string UnstableValue { get; init; } = "wg";
    public string DefaultUnit { get; init; } = "kg";
    public string Delimiter { get; init; } = "\n";
    public string PollCommand { get; init; } = "s\r\n";
    public int PollIntervalMs { get; init; } = 200;
    public int ReconnectDelayMs { get; init; } = 1000;
    public int MaxFrameLength { get; init; } = 4096;
}

public sealed record WeightReading(DateTimeOffset Timestamp, decimal Weight, string Unit, bool? IsStable, string Raw);

public sealed class WeightParser
{
    private readonly ProtocolOptions _options;
    private readonly Regex _regex;
    public WeightParser(ProtocolOptions options)
    {
        _options = options;
        _regex = new Regex(options.Pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(100));
        if (!_regex.GetGroupNames().Contains("weight"))
            throw new ArgumentException("Pattern must contain a named weight group.", nameof(options));
    }
    public bool TryParse(string frame, out WeightReading? reading)
    {
        reading = null;
        Match match;
        try { match = _regex.Match(frame.Trim()); }
        catch (RegexMatchTimeoutException) { return false; }
        if (!match.Success || !decimal.TryParse(Regex.Replace(match.Groups["weight"].Value, @"\s+", ""),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var weight)) return false;
        var stable = match.Groups["stable"].Value;
        bool? isStable = stable.Equals(_options.StableValue, StringComparison.OrdinalIgnoreCase) ? true :
            stable.Equals(_options.UnstableValue, StringComparison.OrdinalIgnoreCase) ? false : null;
        var unit = match.Groups["unit"].Success ? match.Groups["unit"].Value : _options.DefaultUnit;
        reading = new WeightReading(DateTimeOffset.UtcNow, weight, unit, isStable, frame);
        return true;
    }
}
