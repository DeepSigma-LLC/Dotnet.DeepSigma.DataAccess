using Npgsql;
using Pgvector;

namespace DeepSigma.DataAccess.Postgres.Pgvector;

/// <summary>
/// Extensions on <see cref="NpgsqlDataSourceBuilder"/> that wire pgvector
/// support. Use when you're constructing a data source by hand and want
/// fine-grained control over the build pipeline.
/// </summary>
public static class NpgsqlDataSourceBuilderExtensions
{
    /// <summary>
    /// Registers pgvector type mappings on the builder by delegating to the
    /// <c>UseVector()</c> extension from the upstream <c>Pgvector</c> package.
    /// Surfaced here so consumers don't need to know which extension method
    /// ships in which pgvector library.
    /// </summary>
    public static NpgsqlDataSourceBuilder UsePgvector(this NpgsqlDataSourceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseVector();
        return builder;
    }
}
