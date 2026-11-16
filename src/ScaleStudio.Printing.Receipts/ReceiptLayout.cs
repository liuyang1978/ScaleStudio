namespace ScaleStudio.Printing.Receipts;

public static class ReceiptLayout
{
    /// <summary>Fixed-height lines, with a repeated heading; rejects lines that exceed the configured character capacity.</summary>
    public static IReadOnlyList<PrintTemplate> CreatePages(IEnumerable<string> lines,double width=80,double maximumHeight=200)
    {
        if(!double.IsFinite(width)||width<40||width>210||!double.IsFinite(maximumHeight)||maximumHeight<40||maximumHeight>500)
            throw new ArgumentException("Receipt width must be 40–210 mm and page height 40–500 mm.");
        var capacity=(int)Math.Floor((width-10)/4);
        var rows=new List<string>();
        foreach(var line in lines)
        {
            var elements=System.Globalization.StringInfo.GetTextElementEnumerator(line);
            var row=new System.Text.StringBuilder();var count=0;
            while(elements.MoveNext())
            {
                row.Append(elements.GetTextElement());count++;
                if(count==capacity){rows.Add(row.ToString());row.Clear();count=0;}
            }
            if(row.Length>0||line.Length==0)rows.Add(row.ToString());
            if(rows.Count>10000)throw new ArgumentException("Too many receipt lines.");
        }
        if(rows.Count==0)rows.Add("");
        var rowsPerPage=(int)Math.Floor((maximumHeight-24)/6);
        var pages=new List<PrintTemplate>();
        foreach(var chunk in rows.Chunk(rowsPerPage))
        {
            var template=ReceiptsTemplate.Create();
            template.Width=width;template.Height=24+chunk.Length*6;template.Elements.Clear();
            template.Elements.Add(new(){X=5,Y=5,Width=width-10,Text=$"ScaleStudio — {pages.Count+1}",FontSize=4});
            for(var i=0;i<chunk.Length;i++)template.Elements.Add(new(){X=5,Y=16+i*6,Width=width-10,Text=chunk[i],FontSize=4});
            template.Validate();pages.Add(template);
        }
        return pages;
    }
}
