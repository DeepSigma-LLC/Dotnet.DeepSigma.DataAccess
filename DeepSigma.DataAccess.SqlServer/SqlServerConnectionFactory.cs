using System.Data;
using DeepSigma.DataAccess.Abstraction;
using Microsoft.Data.SqlClient;

namespace DeepSigma.DataAccess.SqlServer;

/// <summary>
/// Creates <see cref="SqlConnection"/> instances for the supplied connection string.
/// </summary>
public class SqlServerConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of <see cref="SqlServerConnectionFactory"/>.
    /// </summary>
    public SqlServerConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <inheritdoc />
    public IDbConnection Create() => new SqlConnection(_connectionString);
}
