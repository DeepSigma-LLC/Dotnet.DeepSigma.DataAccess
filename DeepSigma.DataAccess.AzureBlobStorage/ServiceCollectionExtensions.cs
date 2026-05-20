using DeepSigma.DataAccess.AzureBlobStorage;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration helpers for Azure Blob Storage.
/// </summary>
public static class DeepSigmaAzureBlobStorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="BlobStorageApi"/> as a singleton bound to the given container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">Azure Storage connection string.</param>
    /// <param name="containerName">Blob container name.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddDeepSigmaAzureBlobStorage(this IServiceCollection services, string connectionString, string containerName)
    {
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<BlobStorageApi>(sp, connectionString, containerName));
        return services;
    }
}
