namespace Asms.Api.Configuration;

public sealed class DatabaseOptions
{
    public string Provider { get; set; } = string.Empty;

    public string ConnectionString { get; set; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Provider))
        {
            throw new InvalidOperationException("Database:Provider is required.");
        }

        if (Provider is not ("Sqlite" or "PostgreSql"))
        {
            throw new InvalidOperationException("Database:Provider must be Sqlite or PostgreSql.");
        }

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Default is required.");
        }
    }
}
