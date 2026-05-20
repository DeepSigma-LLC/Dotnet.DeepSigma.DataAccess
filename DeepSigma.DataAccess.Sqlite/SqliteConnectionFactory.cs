using System.Data;
using DeepSigma.DataAccess.Abstraction;
using Microsoft.Data.Sqlite;

namespace DeepSigma.DataAccess.Sqlite;

/// <summary>
/// Creates <see cref="SqliteConnection"/> instances for the supplied connection string.
/// Connection-string examples:
/// <list type="bullet">
/// <item><description><c>Data Source=app.db</c> — file-based.</description></item>
/// <item><description><c>Data Source=:memory:</c> — private in-memory database (per-connection; useful for unit tests).</description></item>
/// <item><description><c>Data Source=file:test?mode=memory&amp;cache=shared</c> — shared in-memory database (multiple connections see the same data).</description></item>
/// </list>
/// </summary>
public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of <see cref="SqliteConnectionFactory"/>.
    /// </summary>
    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <inheritdoc />
    public IDbConnection Create() => new SqliteConnection(_connectionString);
}
