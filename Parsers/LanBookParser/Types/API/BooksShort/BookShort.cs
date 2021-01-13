using Newtonsoft.Json;

namespace LanBookParser.Types.API.BooksShort {
    public class BookShort {
        [JsonProperty("type_name")]
        public string TypeName;

        public long Id;
    }
}
