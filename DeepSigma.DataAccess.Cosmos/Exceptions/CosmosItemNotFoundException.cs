using Microsoft.Azure.Cosmos;

namespace DeepSigma.DataAccess.Cosmos.Exceptions;

/// <summary>
/// Thrown when an update or delete targets an item that does not exist (Cosmos returned HTTP 404 Not Found).
/// </summary>
public sealed class CosmosItemNotFoundException : Exception
{
    /// <summary>The id of the missing item.</summary>
    public string? ItemId { get; }

    /// <summary>The container that was queried.</summary>
    public string ContainerId { get; }

    /// <summary>Initializes a new instance of the exception.</summary>
    public CosmosItemNotFoundException(string containerId, string? itemId, CosmosException innerException)
        : base($"Item with id '{itemId}' was not found in container '{containerId}'.", innerException)
    {
        ContainerId = containerId;
        ItemId = itemId;
    }
}
