using Core.Configs;

namespace BiblioclubParser.Configs {
    public interface IParserConfig : IParserConfigBase {
        int MaxThread { get; set; }
        
        int BatchSize { get; set; }
        
        int StartIndex { get; set; }
        
        int EndIndex { get; set; }
    }
}
