using System.Collections.Generic;

namespace IprBookShop.Parser.Types {
    public class SearchResponseData {
        public long Count;
        
        public List<SearchData> Data = new();
    }
}
