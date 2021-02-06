using Newtonsoft.Json;

namespace BookRu.Parser.Types.API.Book {
    public class BookItem {
        [JsonProperty("biblio_desc_2")]
        public string Bib;
        public string Author;
        public int? Pages;
        [JsonProperty("year_norm")]
        public string Year;
        [JsonProperty("pub_name")]
        public string Publisher;
        public string Name;
        public string ISBN;
    }
}
