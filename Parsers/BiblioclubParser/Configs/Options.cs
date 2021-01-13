using CommandLine;
using Core.Configs;

namespace BiblioclubParser.Configs {
    public class Options : OptionsBase, IParserConfig, IMongoConfig {
        [Option("si", Required = false, HelpText = "Начальный индекс для обхода", Default = 1)]
        public int StartIndex { get; set; }
        
        [Option("ei", Required = false, HelpText = "Конечный индекс для обхода", Default = 1000000)]
        public int EndIndex { get; set; }
        
        [Option("cn", Required = false, HelpText = "Название коллекции", Default = "BiblioClub")]
        public string CollectionName { get; set; }
    }
}
