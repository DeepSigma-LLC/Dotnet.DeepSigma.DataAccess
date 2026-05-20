using System.Data;
using DeepSigma.DataAccess.Abstraction;
using Npgsql;

namespace DeepSigma.DataAccess.Postgres;

/// <summary>
/// Creates <see cref="NpgsqlConnection"/> instances for the supplied connection string.
/// </summary>
public class PostgresConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of <see cref="PostgresConnectionFactory"/>.
    /// </summary>
    public PostgresConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <inheritdoc />
    public IDbConnection Create() => new NpgsqlConnection(_connectionString);
}
