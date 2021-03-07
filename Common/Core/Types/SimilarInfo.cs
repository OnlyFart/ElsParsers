using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Types {
    [BsonIgnoreExtraElements]
    public record SimilarInfo(ObjectId BookId, string ExternalId, double Coeff);
}
