using Core.Configs;

namespace LanBookParser.Configs {
    public interface IParserConfig : IParserConfigBase {
        int StartPage { get; set; }
    }
}
