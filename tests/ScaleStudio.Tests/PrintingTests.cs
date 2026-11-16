using ScaleStudio.Printing;
using ScaleStudio.Printing.Labels;
using ScaleStudio.Printing.Receipts;
using ScaleStudio.Printing.Invoices;
using Xunit;
namespace ScaleStudio.Tests;
public sealed class PrintingTests
{
    [Fact] public void ReceiptsWrapAndPaginateWithinPaper()
    {
        var pages=ScaleStudio.Printing.Receipts.ReceiptLayout.CreatePages(Enumerable.Repeat(new string('测',50),60),80,100);
        Assert.True(pages.Count>1);
        foreach(var page in pages){Assert.InRange(page.Height,1,100);Assert.All(page.Elements,e=>Assert.True(e.Y+e.Height<=page.Height));}
        Assert.Throws<ArgumentException>(()=>ScaleStudio.Printing.Receipts.ReceiptLayout.CreatePages(new[]{"x"},80,20));
    }
    [Theory]
    [InlineData(ElementKind.Barcode,"123456789012")]
    [InlineData(ElementKind.QrCode,"称重记录-123456")]
    public void CodesCanBeDecoded(ElementKind kind,string content)
    {
        var element=new PrintElement{Kind=kind,Width=40,Height=40,Text=content};
        var matrix=Codes.Encode(element,content);
        const int scale=5;
        var width=matrix.Width*scale;var height=matrix.Height*scale;
        var pixels=new byte[width*height*3];
        for(var y=0;y<height;y++)for(var x=0;x<width;x++)
        {
            var color=matrix[x/scale,y/scale]?(byte)0:(byte)255;
            var i=(y*width+x)*3;pixels[i]=pixels[i+1]=pixels[i+2]=color;
        }
        var result=new ZXing.BarcodeReaderGeneric().Decode(new ZXing.RGBLuminanceSource(pixels,width,height,ZXing.RGBLuminanceSource.BitmapFormat.RGB24));
        Assert.NotNull(result);Assert.Equal(content,result.Text);
        System.Xml.Linq.XDocument.Parse(SvgRenderer.Render(new PrintTemplate{Elements={element}}));
    }
    [Fact] public void InvalidElementCollectionsAreRejected() =>
        Assert.Throws<ArgumentException>(()=>PrintTemplate.FromJson("{\"Elements\":null}"));
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
