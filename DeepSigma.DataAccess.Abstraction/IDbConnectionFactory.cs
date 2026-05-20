using System.Data;

namespace DeepSigma.DataAccess.Abstraction;

/// <summary>
/// Creates connections to a relational database. Provider packages
/// (SQL Server, Postgres, ...) supply a concrete implementation.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Creates a new database connection. The caller owns disposal.
    /// </summary>
    IDbConnection Create();
}
