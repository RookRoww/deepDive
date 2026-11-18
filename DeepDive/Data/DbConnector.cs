using Microsoft.Data.SqlClient;

namespace DeepDive;

public sealed class DbConnector
{
    private readonly string connectionString;

    public DbConnector(string dbName = "DeepDive")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbName);
        connectionString = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            InitialCatalog = dbName,
            IntegratedSecurity = true,
            Encrypt = false,
            ConnectTimeout = 5
        }.ConnectionString;
    }

    public SqlConnection Create() => new(connectionString);
}
