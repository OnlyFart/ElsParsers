using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Types {
    [BsonIgnoreExtraElements]
    public class SimilarInfo {
        public ObjectId BookId;
        public string ExternalId;
        public double Coeff;
        
        public SimilarInfo(BookInfo book, double coeff) {
            BookId = book.Id;
            ExternalId = book.ExternalId;
            Coeff = coeff;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return false;
            }

            if (ReferenceEquals(this, obj)) {
                return true;
            }

            return obj.GetType() == GetType() && BookId.Equals(((SimilarInfo) obj).BookId);
        }

        public override int GetHashCode() {
            return BookId.GetHashCode();
        }
    }
}
