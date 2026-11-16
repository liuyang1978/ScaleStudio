namespace ScaleStudio.Printing.Labels;
public static class LabelsTemplate
{
    public static PrintTemplate Create() => new()
    {
        Name = "Labels", Width = 60, Height = 40,
        LeftMargin = 0, RightMargin = 0, TopMargin = 0, BottomMargin = 0,
        Elements = { new() { X = 3, Y = 3, Text = "ScaleStudio" }, new() { X = 3, Y = 12, Variable = "Weight" } }
    };
}
