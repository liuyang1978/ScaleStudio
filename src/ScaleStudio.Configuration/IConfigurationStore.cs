using System.Text.Json;

namespace ScaleStudio.Configuration;

public interface IConfigurationStore
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}

public sealed class ConfigurationService(IConfigurationStore store)
{
    public async Task<T> GetAsync<T>(string key, T fallback, CancellationToken ct = default)
    {
        var value = await store.GetAsync(key, ct);
        return value is null ? fallback : JsonSerializer.Deserialize<T>(value) ?? fallback;
    }
    public Task SetAsync<T>(string key, T value, CancellationToken ct = default) =>
        store.SetAsync(key, JsonSerializer.Serialize(value), ct);
    public Task RemoveAsync(string key, CancellationToken ct = default) => store.RemoveAsync(key, ct);
}
