using ScaleStudio.Printing;
using ScaleStudio.Printing.Labels;
using ScaleStudio.Printing.Receipts;
using ScaleStudio.Printing.Invoices;
using Xunit;
namespace ScaleStudio.Tests;
public sealed class PrintingTests
{
    [Fact] public void TemplatesRoundTrip()
    {
        foreach (var template in new[] { LabelsTemplate.Create(), ReceiptsTemplate.Create(), InvoicesTemplate.Create() })
        {
            var copy = PrintTemplate.FromJson(template.ToJson());
            Assert.Equal(template.Width, copy.Width);
            Assert.Equal(template.Elements.Count, copy.Elements.Count);
            var svg = SvgRenderer.Render(copy, new Dictionary<string,string> { ["Weight"] = "<12&>" });
            Assert.Contains("&lt;12&amp;&gt;", svg);
            Assert.Contains("clipPath", svg);
            System.Xml.Linq.XDocument.Parse(svg);
        }
    }
    [Fact] public void RejectsInvalidPrintableArea()
    {
        var template = InvoicesTemplate.Create();
        template.LeftMargin = template.Width;
        Assert.Throws<ArgumentException>(template.Validate);
    }
    [Fact] public void MissingVariableFailsBeforePrinting() =>
        Assert.Throws<KeyNotFoundException>(() => SvgRenderer.Render(LabelsTemplate.Create()));
    [Fact] public async Task PrinterMustBeExplicitlySelected() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => PrintJob.SendAsync(LabelsTemplate.Create(), new Dictionary<string,string>(), new Transport()));
    private sealed class Transport : IPrintTransport
    {
        public Task PrintAsync(string printerName, string svg, CancellationToken cancellationToken = default) => throw new Exception("Must not reach transport");
    }
}
