using Parser.Core.Configs;

namespace AcademiaMoscow.Parser.Configs {
    public interface IParserConfig : IParserConfigBase {
        int StartIndex { get; set; }
        
        int EndIndex { get; set; }
    }
}
