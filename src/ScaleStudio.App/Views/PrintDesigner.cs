using Avalonia.Controls;
using ScaleStudio.Printing;
using ScaleStudio.Printing.Labels;
using ScaleStudio.Printing.Receipts;
using ScaleStudio.Printing.Invoices;
namespace ScaleStudio.App.Views;

public sealed class PrintDesigner : UserControl
{
    public PrintDesigner()
    {
        var editor = new TextBox { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 240 };
        var status = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var preview = new TextBox { AcceptsReturn = true, IsReadOnly = true, MinHeight = 100 };
        var type = new ComboBox { ItemsSource = new[] { "标签", "小票", "发票套打" }, SelectedIndex = 0 };
        var path = new TextBox { Text = Path.Combine(App.DataDirectory, "print-template.json"), Watermark = "模板路径" };
        var buttons = new WrapPanel();
        var surface = new PrintSurface();
        var properties = new StackPanel { Spacing = 4 };
        var variables = new TextBox { Text = "{\"Weight\":\"12.50 kg\",\"Barcode\":\"12345678\"}", AcceptsReturn = true };
        var printers = new ComboBox { MinWidth = 200 };
        var receiptLines = new TextBox { AcceptsReturn=true,MinHeight=70,Watermark="小票明细，每行一条；点击“小票分页”按 80 × 200 mm 自动换行分页" };
        IReadOnlyList<PrintTemplate>? receiptPages = null;
        IReadOnlyDictionary<string,string> Values() => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(variables.Text ?? "{}") ?? new();
        void Refresh()
        {
            var template = PrintTemplate.FromJson(editor.Text ?? "");
            var values = Values();
            _ = SvgRenderer.Render(template, values);
            surface.Template = template;
            surface.Values = values;
            surface.Selected = null; surface.Refresh();
        }
        void Properties()
        {
            properties.Children.Clear();
            if (surface.Selected is not {} selected) return;
            void Field(string name, string value, Action<string> apply)
            {
                var box = new TextBox { Text = value };
                box.LostFocus += (_, _) =>
                {
                    var saved=surface.Template.ToJson();
                    try { apply(box.Text ?? ""); surface.Template.Validate(); _=SvgRenderer.Render(surface.Template,Values()); editor.Text = surface.Template.ToJson(); surface.InvalidateVisual(); }
                    catch(Exception ex) { surface.Template=PrintTemplate.FromJson(saved);surface.Selected=null;editor.Text=saved;surface.Refresh();status.Text=ex.Message; }
                };
                properties.Children.Add(new TextBlock { Text = name }); properties.Children.Add(box);
            }
            void Number(string name, double value, Action<double> apply) => Field(name, value.ToString(System.Globalization.CultureInfo.InvariantCulture), s => apply(double.Parse(s,System.Globalization.CultureInfo.InvariantCulture)));
            Field("内容",selected.Text,v=>selected.Text=v);Field("关联变量",selected.Variable??"",v=>selected.Variable=v);
            Field("字体",selected.FontFamily,v=>selected.FontFamily=v);
            Number("字号 mm",selected.FontSize,v=>selected.FontSize=v);
            Number("X mm",selected.X,v=>selected.X=v);Number("Y mm",selected.Y,v=>selected.Y=v);
            Number("宽 mm",selected.Width,v=>selected.Width=v);Number("高 mm",selected.Height,v=>selected.Height=v);
            Number("旋转 °",selected.Rotation,v=>selected.Rotation=v);
        }
        PrintElement? previous = null;
        surface.Changed += () => { editor.Text = surface.Template.ToJson(); if (!ReferenceEquals(previous,surface.Selected)) { previous=surface.Selected; Properties(); } };
        surface.DragCompleted += Properties;
        void Button(string title, Func<Task> action)
        {
            var button = new Button { Content = title, Margin = new Avalonia.Thickness(0, 0, 8, 8) };
            button.Click += async (_, _) => { try { await action(); } catch (Exception ex) { status.Text = ex.Message; } };
            buttons.Children.Add(button);
        }
        Button("新建", () => { editor.Text = (type.SelectedIndex switch { 1 => ReceiptsTemplate.Create(), 2 => InvoicesTemplate.Create(), _ => LabelsTemplate.Create() }).ToJson(); return Task.CompletedTask; });
        Button("保存模板", async () => { var template = PrintTemplate.FromJson(editor.Text ?? ""); await template.SaveAsync(path.Text!); status.Text = "模板已保存"; });
        Button("加载模板", async () => editor.Text = (await PrintTemplate.LoadAsync(path.Text!)).ToJson());
        Button("导出 SVG 预览", async () =>
        {
            var svg = SvgRenderer.Render(PrintTemplate.FromJson(editor.Text ?? ""), Values());
            var output = Path.ChangeExtension(path.Text!, ".svg");
            await File.WriteAllTextAsync(output, svg); preview.Text = svg; status.Text = $"已导出：{output}";
        });
        Button("应用模板 / 更新画布", () => { Refresh(); return Task.CompletedTask; });
        Button("小票分页", () =>
        {
            receiptPages=ReceiptLayout.CreatePages((receiptLines.Text??"").Replace("\r","").Split('\n'));
            editor.Text=receiptPages[0].ToJson();Refresh();status.Text=$"已生成 {receiptPages.Count} 页小票；预览和打印使用全部页。编辑当前模板前请退出分页。";return Task.CompletedTask;
        });
        Button("退出分页",()=>{receiptPages=null;status.Text="已退出分页，使用当前模板";return Task.CompletedTask;});
        IReadOnlyList<RasterPage> RasterPages()
        {
            Refresh();
            if(receiptPages==null)return new[]{surface.Rasterize()};
            if(receiptPages.Sum(t=>t.Width*t.Height/25.4/25.4*300*300)>40_000_000)throw new InvalidOperationException("分页任务过大，请分批打印");
            var saved=surface.Template;
            try { return receiptPages.Select(t=>{surface.Template=t;return surface.Rasterize();}).ToArray(); }
            finally {surface.Template=saved;surface.Refresh();}
        }
        foreach (var kind in Enum.GetValues<ElementKind>())
        {
            var itemKind = kind;
            Button("添加 "+kind, () =>
            {
                surface.Template = PrintTemplate.FromJson(editor.Text ?? "");
                var element = new PrintElement { Kind=itemKind,X=5,Y=5,Width=30,Height=itemKind==ElementKind.QrCode?30:10,Text=itemKind is ElementKind.Barcode or ElementKind.QrCode?"12345678":"文字" };
                surface.Template.Elements.Add(element); editor.Text=surface.Template.ToJson(); Refresh(); return Task.CompletedTask;
            });
        }
        Button("删除选中", () => { if(surface.Selected!=null)surface.Template.Elements.Remove(surface.Selected);editor.Text=surface.Template.ToJson();Refresh();properties.Children.Clear();return Task.CompletedTask; });
        Button("刷新打印机", () => { printers.ItemsSource=App.PagePrinter?.GetPrinters()??Array.Empty<string>();printers.SelectedItem=surface.Template.PrinterName;status.Text=App.PagePrinter==null?"本平台没有打印适配器":"请选择打印机";return Task.CompletedTask; });
        Button("关联打印机", () => { var template=PrintTemplate.FromJson(editor.Text??"");template.PrinterName=printers.SelectedItem as string;editor.Text=template.ToJson();return Task.CompletedTask; });
        Button("打印预览", async () =>
        {
            var pages=RasterPages();
            var images=new List<Avalonia.Media.Imaging.Bitmap>();
            try
            {
                var stack=new StackPanel {Spacing=16};
                foreach(var page in pages){using var stream=new MemoryStream(page.Png);var image=new Avalonia.Media.Imaging.Bitmap(stream);images.Add(image);stack.Children.Add(new Image {Source=image,Width=page.WidthMm*PrintSurface.Scale,Height=page.HeightMm*PrintSurface.Scale});}
                var window=new Window {Title="打印预览",Width=800,Height=700,Content=new ScrollViewer {Content=stack}};
                if(TopLevel.GetTopLevel(this) is Window owner)await window.ShowDialog(owner);
            }
            finally {foreach(var image in images)image.Dispose();}
        });
        Button("发送到打印机", async () =>
        {
            Refresh();
            var printer=printers.SelectedItem as string;
            if(App.PagePrinter==null||string.IsNullOrWhiteSpace(printer))throw new InvalidOperationException("请选择打印机");
            var pages=RasterPages();
            var confirm=new Window {Title="确认打印",Width=420,Height=220};
            var yes=new Button {Content="确认打印"};var no=new Button {Content="取消"};
            yes.Click+=(_,_)=>confirm.Close(true);no.Click+=(_,_)=>confirm.Close(false);
            confirm.Content=new StackPanel {Margin=new Avalonia.Thickness(16),Spacing=10,Children={new TextBlock {Text=$"打印机：{printer}\n页数：{pages.Count}；首张：{pages[0].WidthMm} × {pages[0].HeightMm} mm",TextWrapping=Avalonia.Media.TextWrapping.Wrap},yes,no}};
            if(TopLevel.GetTopLevel(this) is not Window owner||!await confirm.ShowDialog<bool>(owner))return;
            await App.PagePrinter.PrintAsync(printer,pages);status.Text="任务已发送（不代表纸张已输出）";
        });
        editor.Text = LabelsTemplate.Create().ToJson();
        Refresh();
        Content = new StackPanel { Spacing = 8, Children =
        {
            new TextBlock { Text = "打印模板（毫米）", FontSize = 22 },
            new TextBlock { Text = "在画布选中并拖拽元素，在下方修改属性。纸张与孔边距可在 JSON 编辑。条码为 Code128，二维码为 QR。修改 JSON 或加载后点“更新画布”。变量以 JSON 字典配置。", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            type, path, variables, printers, receiptLines, buttons, status,
            new ScrollViewer {MaxHeight=600,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,Content=surface},
            properties, editor, preview
        }};
    }
}
