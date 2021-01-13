using CommandLine;
using Core.Configs;

namespace UraitParser.Configs {
    public class Options : OptionsBase, IParserConfig, IMongoConfig {
        [Option("sp", Required = false, HelpText = "Стартовая страница для обхода", Default = 1)]
        public int StartPage { get; set; }

        [Option("cn", Required = false, HelpText = "Название коллекции", Default = "Urait")]
        public string CollectionName { get; set; }
    }
}
