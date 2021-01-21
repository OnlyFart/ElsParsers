using Core.Configs;

namespace BiblioClub.Parser.Configs {
    public interface IParserConfig : IParserConfigBase {
        int StartIndex { get; set; }
        
        int EndIndex { get; set; }
    }
}
