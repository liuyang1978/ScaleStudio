using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ScaleStudio.Printing;
namespace ScaleStudio.App.Views;
public sealed class PrintSurface : Control
{
    public const double Scale = 96 / 25.4;
    public PrintTemplate Template { get; set; } = new();
    public IReadOnlyDictionary<string,string> Values { get; set; } = new Dictionary<string,string>();
    public PrintElement? Selected { get; set; }
    public bool Editable { get; set; } = true;
    public event Action? Changed;
    public event Action? DragCompleted;
    private Point? start;
    private double originalX, originalY;
    public void Refresh(){Width=Template.Width*Scale;Height=Template.Height*Scale;InvalidateVisual();}
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.White,new Rect(Bounds.Size));
        using var scale=context.PushTransform(Matrix.CreateScale(Scale,Scale));
        using var clip=context.PushClip(new Rect(Template.LeftMargin,Template.TopMargin,Template.Width-Template.LeftMargin-Template.RightMargin,Template.Height-Template.TopMargin-Template.BottomMargin));
        foreach(var e in Template.Elements)
        {
            using var transform=context.PushTransform(Matrix.CreateRotation(e.Rotation*Math.PI/180)*Matrix.CreateTranslation(e.X,e.Y));
            var text=e.Variable is {Length:>0} key ? Values.GetValueOrDefault(key,$"[{key}]"):e.Text;
            if(e.Kind==ElementKind.Text)context.DrawText(new FormattedText(text,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(e.FontFamily),e.FontSize,Brushes.Black),new Point());
            else if(e.Kind==ElementKind.Line)context.DrawLine(new Pen(Brushes.Black,.2),new Point(),new Point(e.Width,e.Height));
            else if(e.Kind==ElementKind.Rectangle)context.DrawRectangle(null,new Pen(Brushes.Black,.2),new Rect(0,0,e.Width,e.Height));
            else
            {
                var matrix=Codes.Encode(e,text);
                var w=e.Kind==ElementKind.QrCode?Math.Min(e.Width,e.Height):e.Width;
                var h=e.Kind==ElementKind.QrCode?w:e.Height;
                context.FillRectangle(Brushes.White,new Rect(0,0,w,h));
                for(var y=0;y<matrix.Height;y++)for(var x=0;x<matrix.Width;x++)
                    if(matrix[x,y])context.FillRectangle(Brushes.Black,new Rect(x*w/matrix.Width,y*h/matrix.Height,w/matrix.Width,h/matrix.Height));
            }
            if(Editable&&ReferenceEquals(Selected,e))context.DrawRectangle(null,new Pen(Brushes.DodgerBlue,.3),new Rect(0,0,e.Width,e.Height));
        }
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if(!Editable||!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)return;
        var p=e.GetPosition(this)/Scale;
        Selected=Template.Elements.LastOrDefault(item=>{
            var dx=p.X-item.X;var dy=p.Y-item.Y;var a=-item.Rotation*Math.PI/180;
            return new Rect(0,0,item.Width,item.Height).Contains(new Point(dx*Math.Cos(a)-dy*Math.Sin(a),dx*Math.Sin(a)+dy*Math.Cos(a)));
        });
        if(Selected!=null){start=p;originalX=Selected.X;originalY=Selected.Y;e.Pointer.Capture(this);}
        Changed?.Invoke();InvalidateVisual();e.Handled=true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);if(start is not {} s||Selected==null)return;
        var p=e.GetPosition(this)/Scale;Selected.X=Math.Clamp(originalX+p.X-s.X,0,Template.Width);Selected.Y=Math.Clamp(originalY+p.Y-s.Y,0,Template.Height);
        InvalidateVisual();Changed?.Invoke();e.Handled=true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e){start=null;e.Pointer.Capture(null);DragCompleted?.Invoke();base.OnPointerReleased(e);}
    public RasterPage Rasterize()
    {
        _=SvgRenderer.Render(Template,Values);
        var previous=Editable;Editable=false;
        try
        {
            Refresh();Measure(new Size(Width,Height));Arrange(new Rect(0,0,Width,Height));
            var pixels=new PixelSize((int)Math.Ceiling(Template.Width/25.4*300),(int)Math.Ceiling(Template.Height/25.4*300));
            if((long)pixels.Width*pixels.Height>40_000_000)throw new InvalidOperationException("纸张过大，请减小尺寸");
            using var bitmap=new RenderTargetBitmap(pixels,new Vector(300,300));bitmap.Render(this);
            using var stream=new MemoryStream();bitmap.Save(stream);return new(stream.ToArray(),Template.Width,Template.Height);
        }
        finally{Editable=previous;InvalidateVisual();}
    }
}
