using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProfSpo.Parser.Types.API {
    public class Info {
        [JsonProperty("last_page")]
        public int LastPage;
        public List<Book> Data = new();
    }
}
