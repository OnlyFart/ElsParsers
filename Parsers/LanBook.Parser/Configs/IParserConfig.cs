using Parser.Core.Configs;

namespace LanBook.Parser.Configs {
    public interface IParserConfig : IParserConfigBase {
        int StartPage { get; set; }
    }
}
