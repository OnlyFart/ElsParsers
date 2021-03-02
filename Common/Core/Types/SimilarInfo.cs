using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Types {
    [BsonIgnoreExtraElements]
    public class SimilarInfo {
        public ObjectId BookId;
        public string ExternalId;
        public BookComparerResult ComparerResult;
        
        public SimilarInfo(BookInfo book, BookComparerResult comparerResult) {
            BookId = book.Id;
            ExternalId = book.ExternalId;
            ComparerResult = comparerResult;
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
