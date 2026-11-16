using Microsoft.Data.Sqlite;

namespace ScaleStudio.Storage;

/// <summary>Each operation owns its connection; connection pooling is handled by SQLite.</summary>
public sealed class SqliteDatabase
{
    private readonly string _connectionString;
    public SqliteDatabase(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath, Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 10, ForeignKeys = true
        }.ToString();
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new SqliteConnection(_connectionString);
        try { await connection.OpenAsync(ct); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async Task<int> ExecuteAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, sql, parameters);
        return await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<T>> QueryAsync<T>(string sql, Func<SqliteDataReader, T> map,
        IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<T>();
        while (await reader.ReadAsync(ct)) result.Add(map(reader));
        return result;
    }

    public async Task InTransactionAsync(Func<SqliteConnection, SqliteTransaction, CancellationToken, Task> action,
        CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        using var transaction = connection.BeginTransaction();
        try
        {
            await action(connection, transaction, ct);
            ct.ThrowIfCancellationRequested();
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public static SqliteCommand CreateCommand(SqliteConnection connection, string sql,
        IReadOnlyDictionary<string, object?>? parameters = null, SqliteTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        if (parameters != null)
            foreach (var entry in parameters)
                command.Parameters.AddWithValue(entry.Key, entry.Value ?? DBNull.Value);
        return command;
    }
}
