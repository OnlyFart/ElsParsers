using System.Collections.Generic;

namespace IprBookShopParser.Types {
    public class SearchResponseData {
        public long Count;
        
        public List<SearchData> Data = new List<SearchData>();
    }
}
