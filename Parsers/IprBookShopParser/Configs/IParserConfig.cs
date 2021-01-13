using Core.Configs;

namespace IprBookShopParser.Configs {
    public interface IParserConfig : IParserConfigBase {
        int StartPage { get; set; }
    }
}
