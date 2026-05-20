using DeepSigma.DataAccess.MongoDB;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DeepSigma.DataAccess.MongoDB.Tests.Models;

public class DataRequest(string name, string description, List<int> items, string id = "") : IMongoDocument
{
    public string Name { get; set; } = name;
    public string Description { get; set; } = description;
    public List<int> Items { get; set; } = items;

    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = id;
}
