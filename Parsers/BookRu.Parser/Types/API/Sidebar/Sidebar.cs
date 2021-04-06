using System.Collections.Generic;
using Newtonsoft.Json;

namespace BookRu.Parser.Types.API.Sidebar {
    public class Sidebar {
        [JsonProperty("type_id_content")]
        public Dictionary<long, MenuItem[]> Content = new();
    }
}
