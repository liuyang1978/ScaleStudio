using ScaleStudio.Storage;

namespace ScaleStudio.Configuration;

public sealed class SqliteConfigurationStore(SqliteDatabase database) : IConfigurationStore
{
    public Task<int> InitializeAsync(CancellationToken ct = default) =>
        database.ExecuteAsync("CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL)", ct: ct);
    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var values = await database.QueryAsync("SELECT value FROM settings WHERE key=$key",
            r=>r.GetString(0), new Dictionary<string,object?> { ["$key"]=key }, ct);
        return values.FirstOrDefault();
    }
    public Task SetAsync(string key, string value, CancellationToken ct = default) =>
        database.ExecuteAsync("""
            INSERT INTO settings(key,value) VALUES($key,$value)
            ON CONFLICT(key) DO UPDATE SET value=excluded.value
            """, new Dictionary<string,object?>{["$key"]=key,["$value"]=value},ct);
    public Task RemoveAsync(string key, CancellationToken ct = default) =>
        database.ExecuteAsync("DELETE FROM settings WHERE key=$key",
            new Dictionary<string,object?>{["$key"]=key},ct);
}
