using Newtonsoft.Json;

namespace RuCont.Parser.Types.API {
    public class FullInfo {
        public string[] Authors;
        [JsonProperty("efd_id")]
        public string Id;
        [JsonProperty("bip_71_2003")]
        public string Bib;
        [JsonProperty("isbN_ISSN")]
        public string ISBN;
        [JsonProperty("page_count")]
        public int? Pages;
        public string Publisher;
        public string Title;
        public string Year;
        [JsonProperty("efd_type")]
        public int Type;
    }
}
