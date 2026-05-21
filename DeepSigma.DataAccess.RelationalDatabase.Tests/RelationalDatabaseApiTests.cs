using DeepSigma.DataAccess.RelationalDatabase.Tests.Infrastructure;
using Xunit;

namespace DeepSigma.DataAccess.RelationalDatabase.Tests;

public class RelationalDatabaseApiTests : IDisposable
{
    private readonly SqliteTestHarness _harness;
    private readonly RelationalDatabaseApi _db;

    public RelationalDatabaseApiTests()
    {
        // Per-test harness — each test gets its own isolated shared-memory database seeded
        // with the same 3 rows, so mutating tests can't pollute read-only ones.
        _harness = new SqliteTestHarness();
        _db = new RelationalDatabaseApi(_harness.Factory);
    }

    public void Dispose() => _harness.Dispose();

    private sealed record Item
    {
        public long Id { get; init; }
        public string Name { get; init; } = "";
        public double Price { get; init; }
    }

    [Fact]
    public async Task GetAllAsync_returns_all_rows()
    {
        var ct = TestContext.Current.CancellationToken;
        IEnumerable<Item> items = await _db.GetAllAsync<Item>(
            "SELECT id, name, price FROM items", cancellationToken: ct);

        Assert.Equal(3, items.Count());
    }

    [Fact]
    public async Task GetAllAsync_with_params_filters_rows()
    {
        var ct = TestContext.Current.CancellationToken;
        IEnumerable<Item> items = await _db.GetAllAsync<object, Item>(
            "SELECT id, name, price FROM items WHERE price >= @Min",
            new { Min = 1.0 }, cancellationToken: ct);

        Assert.Equal(2, items.Count());
        Assert.All(items, i => Assert.True(i.Price >= 1.0));
    }

    [Fact]
    public async Task GetByIdAsync_returns_row_by_int_id()
    {
        var ct = TestContext.Current.CancellationToken;
        Item? item = await _db.GetByIdAsync<Item>(
            "SELECT id, name, price FROM items WHERE id = @Id", id: 1, cancellationToken: ct);

        Assert.NotNull(item);
        Assert.Equal("apple", item!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_supports_string_id()
    {
        var ct = TestContext.Current.CancellationToken;
        // SQLite is dynamically typed, so a string id literal is accepted on a routinely-integer column.
        Item? item = await _db.GetByIdAsync<Item>(
            "SELECT id, name, price FROM items WHERE name = @Id", id: "banana", cancellationToken: ct);

        Assert.NotNull(item);
        Assert.Equal(0.5, item!.Price);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_no_match()
    {
        var ct = TestContext.Current.CancellationToken;
        Item? item = await _db.GetByIdAsync<Item>(
            "SELECT id, name, price FROM items WHERE id = @Id", id: 9999, cancellationToken: ct);

        Assert.Null(item);
    }

    [Fact]
    public async Task InsertAsync_returns_generated_id()
    {
        var ct = TestContext.Current.CancellationToken;
        int newId = await _db.InsertAsync(
            "INSERT INTO items (name, price) VALUES (@Name, @Price) RETURNING id",
            new { Name = "date", Price = 3.0 }, cancellationToken: ct);

        Assert.True(newId > 0);
    }

    [Fact]
    public async Task InsertAllAsync_returns_total_rows_affected_across_parameter_sets()
    {
        var ct = TestContext.Current.CancellationToken;
        var batch = new[]
        {
            new { Name = "elderberry", Price = 4.0 },
            new { Name = "fig",        Price = 5.0 },
            new { Name = "grape",      Price = 6.0 },
        };

        int affected = await _db.InsertAllAsync(
            "INSERT INTO items (name, price) VALUES (@Name, @Price)", batch, cancellationToken: ct);

        Assert.Equal(3, affected);
    }

    [Fact]
    public async Task UpdateAsync_returns_affected_row_count()
    {
        var ct = TestContext.Current.CancellationToken;
        int affected = await _db.UpdateAsync(
            "UPDATE items SET price = @Price WHERE name = @Name",
            new { Price = 99.0, Name = "apple" }, cancellationToken: ct);

        Assert.Equal(1, affected);
    }

    [Fact]
    public async Task UpdateAllAsync_returns_total_rows_affected()
    {
        var ct = TestContext.Current.CancellationToken;
        var bumps = new[]
        {
            new { Price = 100.0, Name = "apple" },
            new { Price = 101.0, Name = "banana" },
        };

        int affected = await _db.UpdateAllAsync(
            "UPDATE items SET price = @Price WHERE name = @Name", bumps, cancellationToken: ct);

        Assert.Equal(2, affected);
    }

    [Fact]
    public async Task ExecuteScalarAsync_returns_scalar()
    {
        var ct = TestContext.Current.CancellationToken;
        long? count = await _db.ExecuteScalarAsync<long?>(
            "SELECT COUNT(*) FROM items", cancellationToken: ct);

        Assert.True(count >= 3);
    }

    [Fact]
    public async Task QueryFirstOrDefaultAsync_returns_first_matching_row()
    {
        var ct = TestContext.Current.CancellationToken;
        Item? item = await _db.QueryFirstOrDefaultAsync<object, Item>(
            "SELECT id, name, price FROM items WHERE price >= @Min ORDER BY id",
            new { Min = 1.0 }, cancellationToken: ct);

        Assert.NotNull(item);
        Assert.Equal("apple", item!.Name);
    }

    [Fact]
    public async Task QueryFirstOrDefaultAsync_returns_default_when_no_match()
    {
        var ct = TestContext.Current.CancellationToken;
        Item? item = await _db.QueryFirstOrDefaultAsync<object, Item>(
            "SELECT id, name, price FROM items WHERE name = @Name",
            new { Name = "does_not_exist" }, cancellationToken: ct);

        Assert.Null(item);
    }

    [Fact]
    public async Task QueryFirstOrDefaultAsync_tolerates_multiple_matches()
    {
        var ct = TestContext.Current.CancellationToken;
        // Matches all rows; should not throw, should return the first.
        Item? item = await _db.QueryFirstOrDefaultAsync<Item>(
            "SELECT id, name, price FROM items ORDER BY id", cancellationToken: ct);

        Assert.NotNull(item);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_returns_match_when_exactly_one()
    {
        var ct = TestContext.Current.CancellationToken;
        Item? item = await _db.QuerySingleOrDefaultAsync<object, Item>(
            "SELECT id, name, price FROM items WHERE name = @Name",
            new { Name = "banana" }, cancellationToken: ct);

        Assert.NotNull(item);
        Assert.Equal(0.5, item!.Price);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_returns_default_when_zero_matches()
    {
        var ct = TestContext.Current.CancellationToken;
        Item? item = await _db.QuerySingleOrDefaultAsync<object, Item>(
            "SELECT id, name, price FROM items WHERE name = @Name",
            new { Name = "does_not_exist" }, cancellationToken: ct);

        Assert.Null(item);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_throws_when_more_than_one_match()
    {
        var ct = TestContext.Current.CancellationToken;
        // The items table has multiple rows, so this should throw.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _db.QuerySingleOrDefaultAsync<Item>(
                "SELECT id, name, price FROM items", cancellationToken: ct));
    }

    [Fact]
    public async Task UpdateAsync_no_params_overload_runs_ddl()
    {
        var ct = TestContext.Current.CancellationToken;
        // DDL — many providers report -1 rows-affected. We don't assert on the value,
        // only that the call did not throw and returned an int.
        int affected = await _db.UpdateAsync(
            "CREATE TABLE IF NOT EXISTS _ddl_test (id INTEGER PRIMARY KEY)", cancellationToken: ct);

        Assert.True(affected == -1 || affected >= 0);
    }

    [Fact]
    public async Task QueryStreamAsync_yields_rows_lazily_and_completes()
    {
        var ct = TestContext.Current.CancellationToken;
        var collected = new List<Item>();
        await foreach (Item item in _db.QueryStreamAsync<Item>(
            "SELECT id, name, price FROM items ORDER BY id",
            cancellationToken: ct))
        {
            collected.Add(item);
        }

        Assert.Equal(3, collected.Count);
        Assert.Equal("apple", collected[0].Name);
    }

    [Fact]
    public async Task QueryStreamAsync_honors_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // The cancellation should surface either as OperationCanceledException or
        // a derived exception (TaskCanceledException). xUnit's Assert.ThrowsAny accepts both.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (Item _ in _db.QueryStreamAsync<Item>(
                "SELECT id, name, price FROM items",
                cancellationToken: cts.Token))
            {
                // intentionally empty — iteration should throw before producing
            }
        });
    }
}
