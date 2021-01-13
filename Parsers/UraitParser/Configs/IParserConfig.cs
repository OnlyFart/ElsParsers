using Core.Configs;

namespace UraitParser.Configs {
    public interface IParserConfig : IParserConfigBase{
        int MaxThread { get; set; }
        
        int BatchSize { get; set; }
        
        int StartPage { get; set; }
    }
}
