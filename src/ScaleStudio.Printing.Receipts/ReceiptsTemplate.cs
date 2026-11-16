namespace ScaleStudio.Printing.Receipts;
public static class ReceiptsTemplate
{
    public static PrintTemplate Create() => new()
    {
        Name = "Receipts", Width = 80, Height = 200,
        LeftMargin = 2, RightMargin = 2, TopMargin = 2, BottomMargin = 2,
        Elements = { new() { X = 5, Y = 5, Text = "ScaleStudio" }, new() { X = 5, Y = 14, Variable = "Weight" } }
    };
}
