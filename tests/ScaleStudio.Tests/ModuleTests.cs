using System.Text;
using ScaleStudio.Weighing;
using ScaleStudio.Storage;
using ScaleStudio.Configuration;
using Xunit;

namespace ScaleStudio.Tests;

public class ModuleTests
{
    [Theory]
    [InlineData("sg0000.00kg",0,true)]
    [InlineData("wg-12.45kg",-12.45,false)]
    public void ParsesWeight(string raw,double value,bool stable)
    {
        Assert.True(new WeightParser(new ProtocolOptions()).TryParse(raw,out var reading));
        Assert.Equal((decimal)value,reading!.Weight);
        Assert.Equal(stable,reading.IsStable);
    }

    [Fact]
    public void RejectsMalformedFrame()
    {
        Assert.False(new WeightParser(new ProtocolOptions()).TryParse("invalid",out _));
    }

    [Fact]
    public void HandlesSplitAndMultipleFrames()
    {
        var decoder=new FrameDecoder("\r\n",20);
        Assert.Empty(decoder.Push(Encoding.ASCII.GetBytes("sg12")));
        Assert.Equal(new[]{"sg12kg","wg13kg"},decoder.Push(Encoding.ASCII.GetBytes("kg\r\nwg13kg\r\n")));
    }

    [Fact]
    public void RejectsOversizeFrame()
    {
        var decoder=new FrameDecoder("\n",3);
        Assert.Equal(new[]{"ok"},decoder.Push(Encoding.ASCII.GetBytes("123456\nok\n")));
    }

    [Fact]
    public async Task ConfigurationAndStorageRoundTrip()
    {
        var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var db=new SqliteDatabase(Path.Combine(folder,"test.db"));
            var sql=new SqliteConfigurationStore(db);
            await sql.InitializeAsync();
            foreach(IConfigurationStore store in new IConfigurationStore[]{new IniConfigurationStore(Path.Combine(folder,"settings.ini")),sql})
            {
                var service=new ConfigurationService(store);
                await service.SetAsync("text","特殊内容\r\n=%");
                Assert.Equal("特殊内容\r\n=%",await service.GetAsync("text",""));
                await service.RemoveAsync("text");
                Assert.Equal("fallback",await service.GetAsync("text","fallback"));
            }
            var records=new WeightRecordStore(db);
            await records.InitializeAsync();
            var r=new WeightRecord("id",DateTimeOffset.UtcNow,12.345678m,"kg",true,"sg12.345678kg");
            await records.InsertAsync(r);
            Assert.Equal(r.Weight,Assert.Single(await records.QueryRecentAsync()).Weight);
            await records.UpdateAsync(r with { Weight=1.25m });
            Assert.Equal(1.25m,Assert.Single(await records.QueryRecentAsync()).Weight);
            Assert.Equal(1,await records.CleanBeforeAsync(r.Timestamp.AddSeconds(1)));
            Assert.Empty(await records.QueryRecentAsync());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(folder,true);
        }
    }
}
