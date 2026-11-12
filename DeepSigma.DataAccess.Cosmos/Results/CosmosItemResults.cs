using Microsoft.Azure.Cosmos;

namespace DeepSigma.DataAccess.Cosmos.Results;

/// <summary>Result of creating an item.</summary>
public union CosmosInsertResult<T>(CosmosCreated<T>, CosmosConflict);

/// <summary>Result of replacing an item.</summary>
public union CosmosUpdateResult<T>(CosmosUpdated<T>, CosmosResourceNotFound);

/// <summary>Result of deleting an item.</summary>
public union CosmosDeleteResult(CosmosDeleted, CosmosResourceNotFound);

/// <summary>The item returned by a successful create operation.</summary>
/// <param name="Item">The item returned by Cosmos DB.</param>
public sealed record CosmosCreated<T>(T Item);

/// <summary>The item returned by a successful replace operation.</summary>
/// <param name="Item">The item returned by Cosmos DB.</param>
public sealed record CosmosUpdated<T>(T Item);

/// <summary>A successful delete operation.</summary>
public sealed record CosmosDeleted;

/// <summary>A Cosmos DB conflict response; its cause is not necessarily a duplicate item ID.</summary>
/// <param name="DatabaseId">The requested database.</param>
/// <param name="ContainerId">The requested container.</param>
/// <param name="ItemId">The requested item ID, if available.</param>
/// <param name="Exception">The original Cosmos exception, including diagnostics.</param>
public sealed record CosmosConflict(string DatabaseId, string ContainerId, string? ItemId, CosmosException Exception);

/// <summary>A Cosmos DB resource-not-found response, which may refer to a parent resource.</summary>
/// <param name="DatabaseId">The requested database.</param>
/// <param name="ContainerId">The requested container.</param>
/// <param name="ItemId">The requested item ID, if available.</param>
/// <param name="Exception">The original Cosmos exception, including diagnostics.</param>
public sealed record CosmosResourceNotFound(string DatabaseId, string ContainerId, string? ItemId, CosmosException Exception);
