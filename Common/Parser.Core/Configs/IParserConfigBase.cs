namespace Parser.Core.Configs {
    public interface IParserConfigBase {
        string Proxy { get; set; }
        
        int MaxThread { get; set; }
        
        int BatchSize { get; set; }
    }
}
