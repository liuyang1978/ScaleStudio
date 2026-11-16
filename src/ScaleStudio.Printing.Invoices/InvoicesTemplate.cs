namespace ScaleStudio.Printing.Invoices;
public static class InvoicesTemplate
{
    public static PrintTemplate Create() => new()
    {
        Name = "Invoices", Width = 210, Height = 140,
        LeftMargin = 10, RightMargin = 10, TopMargin = 10, BottomMargin = 10,
        Elements = { new() { X = 13, Y = 13, Text = "ScaleStudio" }, new() { X = 13, Y = 22, Variable = "Weight" } }
    };
}
