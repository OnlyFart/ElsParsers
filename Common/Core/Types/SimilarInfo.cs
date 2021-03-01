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

        protected bool Equals(SimilarInfo other) {
            return BookId.Equals(other.BookId);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SimilarInfo) obj);
        }

        public override int GetHashCode() {
            return BookId.GetHashCode();
        }
    }
}
