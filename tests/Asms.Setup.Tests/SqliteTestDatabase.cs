using Microsoft.Data.Sqlite;

namespace Asms.Setup.Tests;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private SqliteTestDatabase(string path)
    {
        Path = path;
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            ForeignKeys = true
        }.ToString();
    }

    public string Path { get; }

    public string ConnectionString { get; }

    public static SqliteTestDatabase Create()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"asms_setup_{Guid.NewGuid():N}.db");
        return new SqliteTestDatabase(path);
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }

        return ValueTask.CompletedTask;
    }
}
