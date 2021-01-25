using System.Collections.Generic;
using Core.Types;

namespace Book.Comparer.Types {
    public class SaveResult {
        public BookInfo Book;
        public HashSet<BookInfo> SimilarBooks;
    }
}
