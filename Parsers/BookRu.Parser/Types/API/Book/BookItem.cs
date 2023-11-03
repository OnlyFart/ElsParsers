using Newtonsoft.Json;

namespace BookRu.Parser.Types.API.Book {
    public class BookItem {
        [JsonProperty("biblio_desc")]
        public string BiblioDesc;
        public string Author;
        public int? Pages;
        public string Year;
        [JsonProperty("pub_name")]
        public string Publisher;
        public string Name;
        public string ISBN;
    }
}
