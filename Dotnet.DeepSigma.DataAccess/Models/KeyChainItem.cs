namespace DeepSigma.DataAccess.Models
{
    /// <summary>
    /// Represents an item in a key chain with a display name and a key.
    /// </summary>
    /// <param name="Name">The display name of the item.</param>
    /// <param name="Key">The key of the item.</param>
    public record class KeyChainItem(string Name, string Key);
}
