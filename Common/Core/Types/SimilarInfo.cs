using MongoDB.Bson;

namespace Core.Types {
    public class SimilarInfo {
        public ObjectId BookId;
        public string ElsName;
        public string ExternalId;
        public BookComparerResult ComparerResult;
        
        public SimilarInfo(BookInfo book, BookComparerResult comparerResult) {
            BookId = book.Id;
            ElsName = book.ElsName;
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
