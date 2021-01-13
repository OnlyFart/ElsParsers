using Core.Configs;

namespace LanBookParser.Configs {
    public interface IParserConfig : IParserConfigBase {
        int MaxThread { get; set; }
        
        int BatchSize { get; set; }
        
        int StartPage { get; set; }
    }
}
