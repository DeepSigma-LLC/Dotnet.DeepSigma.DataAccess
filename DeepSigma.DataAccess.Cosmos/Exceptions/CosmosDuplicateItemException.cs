using Microsoft.Azure.Cosmos;

namespace DeepSigma.DataAccess.Cosmos.Exceptions;

/// <summary>
/// Thrown when an insert would create a duplicate item (Cosmos returned HTTP 409 Conflict).
/// </summary>
public sealed class CosmosDuplicateItemException : Exception
{
    /// <summary>The id of the item that conflicted.</summary>
    public string? ItemId { get; }

    /// <summary>The container the conflict occurred in.</summary>
    public string ContainerId { get; }

    /// <summary>Initializes a new instance of the exception.</summary>
    public CosmosDuplicateItemException(string containerId, string? itemId, CosmosException innerException)
        : base($"Item with id '{itemId}' already exists in container '{containerId}'.", innerException)
    {
        ContainerId = containerId;
        ItemId = itemId;
    }
}
