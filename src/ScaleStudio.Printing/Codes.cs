using System.Globalization;
using System.Text;
using ZXing;
using ZXing.Common;
namespace ScaleStudio.Printing;

public static class Codes
{
    public static BitMatrix Encode(PrintElement element, string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > 2000) throw new ArgumentException("Code content must contain 1–2000 characters.");
        return new MultiFormatWriter().encode(text,
            element.Kind == ElementKind.QrCode ? BarcodeFormat.QR_CODE : BarcodeFormat.CODE_128, 0, 0,
            new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] =  element.Kind == ElementKind.QrCode ? 4 : 10, [EncodeHintType.CHARACTER_SET] = "UTF-8" });
    }
    public static string ToSvg(PrintElement element, string text)
    {
        var matrix = Encode(element, text);
        var width = element.Kind == ElementKind.QrCode ? Math.Min(element.Width, element.Height) : element.Width;
        var height = element.Kind == ElementKind.QrCode ? width : element.Height;
        var sb = new StringBuilder($"<svg width=\"{width.ToString(CultureInfo.InvariantCulture)}\" height=\"{height.ToString(CultureInfo.InvariantCulture)}\" viewBox=\"0 0 {matrix.Width} {matrix.Height}\" preserveAspectRatio=\"none\" shape-rendering=\"crispEdges\"><rect width=\"100%\" height=\"100%\" fill=\"white\"/><path fill=\"black\" d=\"");
        for (var y = 0; y < matrix.Height; y++)
            for (var x = 0; x < matrix.Width; x++)
                if (matrix[x,y]) sb.Append($"M{x} {y}h1v1h-1z");
        return sb.Append("\"/></svg>").ToString();
    }
}

/// <summary>Raster page generated from the same surface used for preview.</summary>
public sealed record RasterPage(byte[] Png, double WidthMm, double HeightMm);
public interface IPagePrinter
{
    IReadOnlyList<string> GetPrinters();
    Task PrintAsync(string printer, IReadOnlyList<RasterPage> pages, CancellationToken cancellationToken = default);
}
