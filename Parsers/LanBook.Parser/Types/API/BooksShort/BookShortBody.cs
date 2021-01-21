using System.Collections.Generic;

namespace LanBook.Parser.Types.API.BooksShort {
    public class BooksShortBody {
        public List<BookShort> Items;
        
        public List<BookShort> Extra;

        public long Total;
    }
}
