using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.Versioning;
using ScaleStudio.Printing;
namespace ScaleStudio.Desktop;

[SupportedOSPlatform("windows")]
public sealed class WindowsPagePrinter : IPagePrinter
{
    public IReadOnlyList<string> GetPrinters() => PrinterSettings.InstalledPrinters.Cast<string>().ToArray();
    public Task PrintAsync(string printer,IReadOnlyList<RasterPage> pages,CancellationToken cancellationToken=default)
    {
        if(pages.Count==0)throw new ArgumentException("Empty print job.");
        return Task.Run(()=>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var document=new PrintDocument {DocumentName="ScaleStudio",OriginAtMargins=false,PrintController=new StandardPrintController()};
            document.PrinterSettings.PrinterName=printer;
            if(!document.PrinterSettings.IsValid)throw new InvalidOperationException("Printer is unavailable.");
            var index=0;
            document.QueryPageSettings+=(_,e)=>
            {
                var page=pages[index];
                e.PageSettings.PaperSize=new PaperSize("ScaleStudio",(int)Math.Round(page.WidthMm/25.4*100),(int)Math.Round(page.HeightMm/25.4*100));
                e.PageSettings.Margins=new Margins(0,0,0,0);
            };
            document.PrintPage+=(_,e)=>
            {
                if(cancellationToken.IsCancellationRequested){e.Cancel=true;return;}
                var page=pages[index];
                var width=(float)(page.WidthMm/25.4*100);var height=(float)(page.HeightMm/25.4*100);
                if(Math.Abs(e.PageBounds.Width-width)>3||Math.Abs(e.PageBounds.Height-height)>3)throw new InvalidOperationException("Printer rejected the requested paper size; configure a matching paper form.");
                using var stream=new MemoryStream(page.Png);using var image=Image.FromStream(stream);
                var graphics=e.Graphics??throw new InvalidOperationException("No printer graphics.");
                graphics.TranslateTransform(-e.PageSettings.HardMarginX,-e.PageSettings.HardMarginY);
                graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.DrawImage(image,new RectangleF(0,0,width,height));
                e.HasMorePages=++index<pages.Count;
            };
            document.Print();
            cancellationToken.ThrowIfCancellationRequested();
        },cancellationToken);
    }
}
