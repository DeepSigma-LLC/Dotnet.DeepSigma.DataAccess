using DeepSigma.DataAccess.RelationalDatabase.Tests.Infrastructure;
using Xunit;

namespace DeepSigma.DataAccess.RelationalDatabase.Tests;

public class TransactionScopeTests : IClassFixture<SqliteTestHarness>
{
    private readonly RelationalDatabaseApi _db;

    public TransactionScopeTests(SqliteTestHarness harness)
    {
        _db = new RelationalDatabaseApi(harness.Factory);
    }

    private async Task<long> CountAsync(CancellationToken cancellationToken)
    {
        long? count = await _db.ExecuteScalarAsync<long?>(
            "SELECT COUNT(*) FROM items", cancellationToken: cancellationToken);
        return count ?? 0;
    }

    [Fact]
    public async Task Commit_persists_inserted_rows()
    {
        var ct = TestContext.Current.CancellationToken;
        long before = await CountAsync(ct);

        await using (RelationalDatabaseTransactionScope tx = await _db.BeginTransactionAsync(cancellationToken: ct))
        {
            await tx.InsertAsync(
                "INSERT INTO items (name, price) VALUES (@Name, @Price)",
                new { Name = "committed_row", Price = 1.0 }, cancellationToken: ct);
            await tx.CommitAsync(ct);
        }

        long after = await CountAsync(ct);
        Assert.Equal(before + 1, after);
    }

    [Fact]
    public async Task Dispose_without_commit_rolls_back()
    {
        var ct = TestContext.Current.CancellationToken;
        long before = await CountAsync(ct);

        await using (RelationalDatabaseTransactionScope tx = await _db.BeginTransactionAsync(cancellationToken: ct))
        {
            await tx.InsertAsync(
                "INSERT INTO items (name, price) VALUES (@Name, @Price)",
                new { Name = "rolled_back_row", Price = 1.0 }, cancellationToken: ct);
            // No CommitAsync — scope disposes and should roll back.
        }

        long after = await CountAsync(ct);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Commit_is_idempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using RelationalDatabaseTransactionScope tx = await _db.BeginTransactionAsync(cancellationToken: ct);
        await tx.InsertAsync(
            "INSERT INTO items (name, price) VALUES (@Name, @Price)",
            new { Name = "idempotent_commit", Price = 1.0 }, cancellationToken: ct);

        await tx.CommitAsync(ct);
        await tx.CommitAsync(ct); // second call should be a no-op
        await tx.CommitAsync(ct); // and again
    }

    [Fact]
    public async Task GetAllAsync_inside_transaction_sees_uncommitted_writes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using RelationalDatabaseTransactionScope tx = await _db.BeginTransactionAsync(cancellationToken: ct);
        await tx.InsertAsync(
            "INSERT INTO items (name, price) VALUES (@Name, @Price)",
            new { Name = "tx_visible", Price = 7.0 }, cancellationToken: ct);

        IEnumerable<dynamic> rows = await tx.GetAllAsync<dynamic>(
            "SELECT name FROM items WHERE name = 'tx_visible'", cancellationToken: ct);

        Assert.Single(rows);
        // Intentionally do not commit; row vanishes after dispose.
    }
}
