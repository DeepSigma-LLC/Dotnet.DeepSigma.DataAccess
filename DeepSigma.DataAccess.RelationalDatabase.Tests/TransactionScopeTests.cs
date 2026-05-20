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

    private async Task<long> CountAsync()
    {
        long? count = await _db.ExecuteAsync<object, long?>("SELECT COUNT(*) FROM items", parameters: null);
        return count ?? 0;
    }

    [Fact]
    public async Task Commit_persists_inserted_rows()
    {
        long before = await CountAsync();

        await using (RelationalDatabaseTransactionScope tx = await _db.BeginTransactionAsync())
        {
            await tx.InsertAsync(
                "INSERT INTO items (name, price) VALUES (@Name, @Price)",
                new { Name = "committed_row", Price = 1.0 });
            await tx.CommitAsync();
        }

        long after = await CountAsync();
        Assert.Equal(before + 1, after);
    }

    [Fact]
    public async Task Dispose_without_commit_rolls_back()
    {
        long before = await CountAsync();

        await using (RelationalDatabaseTransactionScope tx = await _db.BeginTransactionAsync())
        {
            await tx.InsertAsync(
                "INSERT INTO items (name, price) VALUES (@Name, @Price)",
                new { Name = "rolled_back_row", Price = 1.0 });
            // No CommitAsync — scope disposes and should roll back.
        }

        long after = await CountAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Commit_is_idempotent()
    {
        await using RelationalDatabaseTransactionScope tx = await _db.BeginTransactionAsync();
        await tx.InsertAsync(
            "INSERT INTO items (name, price) VALUES (@Name, @Price)",
            new { Name = "idempotent_commit", Price = 1.0 });

        await tx.CommitAsync();
        await tx.CommitAsync(); // second call should be a no-op
        await tx.CommitAsync(); // and again
    }

    [Fact]
    public async Task GetAllAsync_inside_transaction_sees_uncommitted_writes()
    {
        await using RelationalDatabaseTransactionScope tx = await _db.BeginTransactionAsync();
        await tx.InsertAsync(
            "INSERT INTO items (name, price) VALUES (@Name, @Price)",
            new { Name = "tx_visible", Price = 7.0 });

        IEnumerable<dynamic> rows = await tx.GetAllAsync<dynamic>(
            "SELECT name FROM items WHERE name = 'tx_visible'");

        Assert.Single(rows);
        // Intentionally do not commit; row vanishes after dispose.
    }
}
