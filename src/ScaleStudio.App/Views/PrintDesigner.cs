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
        var weight = new TextBox { Text = "12.50 kg", Watermark = "预览重量" };
        var buttons = new WrapPanel();
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
            var svg = SvgRenderer.Render(PrintTemplate.FromJson(editor.Text ?? ""), new Dictionary<string,string> { ["Weight"] = weight.Text ?? "" });
            var output = Path.ChangeExtension(path.Text!, ".svg");
            await File.WriteAllTextAsync(output, svg); preview.Text = svg; status.Text = $"已导出：{output}。使用浏览器查看并按实际尺寸打印；尚无原生打印驱动。";
        });
        editor.Text = LabelsTemplate.Create().ToJson();
        Content = new StackPanel { Spacing = 8, Children =
        {
            new TextBlock { Text = "打印模板（毫米）", FontSize = 22 },
            new TextBlock { Text = "编辑 JSON 设置纸张、元素坐标、字体、旋转、变量及发票孔边距。当前支持文本、线、矩形；条码/二维码及拖拽设计尚未实现。", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            type, path, weight, buttons, editor, status, preview
        }};
    }
}
