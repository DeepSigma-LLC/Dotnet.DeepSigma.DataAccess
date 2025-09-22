using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DeepSigma.DataAccess.Models
{
    public interface IDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
    }
}
