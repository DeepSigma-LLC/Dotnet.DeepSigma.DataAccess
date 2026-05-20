using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DeepSigma.DataAccess.MongoDB;

/// <summary>
/// Marker for documents stored in MongoDB with a string Id mapped to ObjectId.
/// </summary>
public interface IMongoDocument
{
    /// <summary>
    /// The document identifier (mapped to ObjectId).
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
}
