using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace ScaleStudio.Printing;

public enum ElementKind { Text, Line, Rectangle, Barcode, QrCode }
public sealed record PrintElement
{
    public ElementKind Kind { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 20;
    public double Height { get; set; } = 6;
    public double Rotation { get; set; }
    public double FontSize { get; set; } = 4;
    public string FontFamily { get; set; } = "sans-serif";
    public string Text { get; set; } = "";
    public string? Variable { get; set; }
}
public sealed record PrintTemplate
{
    public int Version { get; init; } = 1;
    public string Name { get; set; } = "Template";
    public double Width { get; set; } = 60;
    public double Height { get; set; } = 40;
    public double LeftMargin { get; set; }
    public double TopMargin { get; set; }
    public double RightMargin { get; set; }
    public double BottomMargin { get; set; }
    public string? PrinterName { get; set; }
    public List<PrintElement> Elements { get; init; } = new();
    public void Validate()
    {
        static bool Positive(double v) => double.IsFinite(v) && v > 0;
        if (Version != 1 || !Positive(Width) || !Positive(Height) || Width > 2000 || Height > 10000)
            throw new ArgumentException("Invalid page dimensions or template version.");
        if (new[] { LeftMargin, TopMargin, RightMargin, BottomMargin }.Any(v => !double.IsFinite(v) || v < 0)
            || LeftMargin + RightMargin >= Width || TopMargin + BottomMargin >= Height)
            throw new ArgumentException("Invalid printable area.");
        if (Elements is null || Elements.Count > 2000) throw new ArgumentException("Invalid element list.");
        foreach (var e in Elements)
            if (e is null || e.Text is null || string.IsNullOrWhiteSpace(e.FontFamily) || !Enum.IsDefined(e.Kind) || !double.IsFinite(e.X) || !double.IsFinite(e.Y) || !double.IsFinite(e.Rotation)
                || !Positive(e.Width) || !Positive(e.Height) || !Positive(e.FontSize))
                throw new ArgumentException("Invalid print element.");
    }
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public string ToJson() { Validate(); return JsonSerializer.Serialize(this, JsonOptions); }
    public static PrintTemplate FromJson(string json)
    {
        var template = JsonSerializer.Deserialize<PrintTemplate>(json, JsonOptions) ?? throw new ArgumentException("Empty template.");
        template.Validate(); return template;
    }
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default) =>
        await File.WriteAllTextAsync(path, ToJson(), cancellationToken);
    public static async Task<PrintTemplate> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        FromJson(await File.ReadAllTextAsync(path, cancellationToken));
}

public static class SvgRenderer
{
    public static string Render(PrintTemplate template, IReadOnlyDictionary<string, string>? values = null)
    {
        template.Validate();
        static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        static string E(string value) => SecurityElement.Escape(value) ?? "";
        var svg = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{N(template.Width)}mm\" height=\"{N(template.Height)}mm\" viewBox=\"0 0 {N(template.Width)} {N(template.Height)}\">");
        svg.Append($"<defs><clipPath id=\"area\"><rect x=\"{N(template.LeftMargin)}\" y=\"{N(template.TopMargin)}\" width=\"{N(template.Width-template.LeftMargin-template.RightMargin)}\" height=\"{N(template.Height-template.TopMargin-template.BottomMargin)}\"/></clipPath></defs><g clip-path=\"url(#area)\">");
        foreach (var element in template.Elements)
        {
            var text = element.Variable is { Length: > 0 } key
                ? values != null && values.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"Missing print variable: {key}")
                : element.Text;
            svg.Append($"<g transform=\"translate({N(element.X)} {N(element.Y)}) rotate({N(element.Rotation)})\">");
            svg.Append(element.Kind switch
            {
                ElementKind.Text => $"<text y=\"{N(element.FontSize)}\" font-size=\"{N(element.FontSize)}\" font-family=\"{E(element.FontFamily)}\">{E(text)}</text>",
                ElementKind.Line => $"<line x2=\"{N(element.Width)}\" y2=\"{N(element.Height)}\" stroke=\"black\" stroke-width=\"0.2\"/>",
                ElementKind.Rectangle => $"<rect width=\"{N(element.Width)}\" height=\"{N(element.Height)}\" fill=\"none\" stroke=\"black\" stroke-width=\"0.2\"/>",
                ElementKind.Barcode or ElementKind.QrCode => Codes.ToSvg(element, text),
                _ => throw new ArgumentException("Unknown element")
            });
            svg.Append("</g>");
        }
        return svg.Append("</g></svg>").ToString();
    }
}

public interface IPrintTransport
{
    Task PrintAsync(string printerName, string svg, CancellationToken cancellationToken = default);
}
public static class PrintJob
{
    public static Task SendAsync(PrintTemplate template, IReadOnlyDictionary<string,string> values, IPrintTransport transport, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(template.PrinterName)) throw new InvalidOperationException("Select a printer first.");
        return transport.PrintAsync(template.PrinterName, SvgRenderer.Render(template, values), cancellationToken);
    }
}
