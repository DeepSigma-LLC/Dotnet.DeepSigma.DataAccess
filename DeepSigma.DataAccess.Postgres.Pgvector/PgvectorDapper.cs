using Dapper;
using Pgvector.Dapper;

namespace DeepSigma.DataAccess.Postgres.Pgvector;

/// <summary>
/// Dapper plumbing for the <c>pgvector</c> <c>Vector</c> type.
/// </summary>
public static class PgvectorDapper
{
    private static int _registered;

    /// <summary>
    /// Registers the <c>Pgvector.Dapper.VectorTypeHandler</c> with Dapper so
    /// that <c>Vector</c> values can be bound to / read from queries. Safe to
    /// call from multiple call sites — only the first call per AppDomain has
    /// any effect.
    /// </summary>
    public static void RegisterTypeHandler()
    {
        if (Interlocked.CompareExchange(ref _registered, 1, 0) == 0)
        {
            SqlMapper.AddTypeHandler(new VectorTypeHandler());
        }
    }
}
