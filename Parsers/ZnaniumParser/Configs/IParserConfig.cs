using Core.Configs;

namespace ZnaniumParser.Configs {
    public interface IParserConfig : IParserConfigBase {
        int MaxThread { get; set; }
        
        int BatchSize { get; set; }
    }
}
