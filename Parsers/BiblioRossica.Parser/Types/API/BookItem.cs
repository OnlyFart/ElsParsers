using Newtonsoft.Json;

namespace BiblioRossica.Parser.Types.API {
    public class BookItem {
        [JsonProperty("id_book")]
        public string Id;
    }
}
