using Parser.Core.Configs;

namespace IprBookShop.Parser.Configs {
    public interface IParserConfig : IParserConfigBase {
        int StartPage { get; set; }
    }
}
