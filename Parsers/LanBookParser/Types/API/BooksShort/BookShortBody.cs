using System.Collections.Generic;

namespace LanBookParser.Types.API.BooksShort {
    public class BooksShortBody {
        public List<BookShort> Items;
        
        public List<BookShort> Extra;

        public long Total;
    }
}
