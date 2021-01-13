using Core.Configs;

namespace BiblioclubParser.Configs {
    public interface IParserConfig : IParserConfigBase {
        int StartIndex { get; set; }
        
        int EndIndex { get; set; }
    }
}
