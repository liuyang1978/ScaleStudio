using System.Globalization;

namespace ScaleStudio.Storage;

public sealed record WeightRecord(string Id, DateTimeOffset Timestamp, decimal Weight, string Unit, bool? IsStable, string Raw);

public sealed class WeightRecordStore(SqliteDatabase database)
{
    private static string Time(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    public Task<int> InitializeAsync(CancellationToken ct = default) => database.ExecuteAsync("""
        CREATE TABLE IF NOT EXISTS weight_records (
            id TEXT PRIMARY KEY, timestamp TEXT NOT NULL, weight TEXT NOT NULL,
            unit TEXT NOT NULL, stable INTEGER NULL, raw TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS ix_weight_records_timestamp ON weight_records(timestamp);
        """, ct: ct);

    public Task<int> InsertAsync(WeightRecord record, CancellationToken ct = default) =>
        database.ExecuteAsync("""
            INSERT INTO weight_records(id,timestamp,weight,unit,stable,raw)
            VALUES($id,$time,$weight,$unit,$stable,$raw)
            """, Parameters(record), ct);

    public Task<int> UpdateAsync(WeightRecord record, CancellationToken ct = default) =>
        database.ExecuteAsync("""
            UPDATE weight_records SET timestamp=$time,weight=$weight,unit=$unit,stable=$stable,raw=$raw
            WHERE id=$id
            """, Parameters(record), ct);

    public Task<int> DeleteAsync(string id, CancellationToken ct = default) =>
        database.ExecuteAsync("DELETE FROM weight_records WHERE id=$id", new Dictionary<string,object?> { ["$id"]=id }, ct);

    public Task<int> CleanBeforeAsync(DateTimeOffset cutoff, CancellationToken ct = default) =>
        database.ExecuteAsync("DELETE FROM weight_records WHERE timestamp < $cutoff",
            new Dictionary<string,object?> { ["$cutoff"]=Time(cutoff) }, ct);

    public Task<List<WeightRecord>> QueryRecentAsync(int limit = 100, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return database.QueryAsync("""
            SELECT id,timestamp,weight,unit,stable,raw FROM weight_records
            ORDER BY timestamp DESC, id DESC LIMIT $limit
            """, r => new WeightRecord(r.GetString(0),
                DateTimeOffset.Parse(r.GetString(1), CultureInfo.InvariantCulture),
                decimal.Parse(r.GetString(2), CultureInfo.InvariantCulture), r.GetString(3),
                r.IsDBNull(4) ? null : r.GetInt64(4) != 0, r.GetString(5)),
            new Dictionary<string,object?> { ["$limit"]=limit }, ct);
    }

    private static Dictionary<string,object?> Parameters(WeightRecord r) => new()
    {
        ["$id"]=r.Id, ["$time"]=Time(r.Timestamp), ["$weight"]=r.Weight.ToString(CultureInfo.InvariantCulture),
        ["$unit"]=r.Unit, ["$stable"]=r.IsStable.HasValue ? (r.IsStable.Value ? 1 : 0) : null, ["$raw"]=r.Raw
    };
}
