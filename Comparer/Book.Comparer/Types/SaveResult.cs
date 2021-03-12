using System.Collections.Generic;
using Core.Types;

namespace Book.Comparer.Types {
    public class SaveResult {
        public readonly BookInfo Book;
        public readonly List<BookInfo> SimilarBooks;

        public SaveResult(BookInfo book) {
            Book = book;
            SimilarBooks = new List<BookInfo>();
        }
    }
}
