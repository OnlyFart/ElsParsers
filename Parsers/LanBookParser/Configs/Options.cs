using CommandLine;
using Core.Configs;

namespace LanBookParser.Configs {
    public class Options : OptionsBase, IParserConfig, IMongoConfig {
        [Option("sp", Required = false, HelpText = "Стартовая страница для обхода", Default = 1)]
        public int StartPage { get; set; }

        [Option("cn", Required = false, HelpText = "Название коллекции", Default = "LanBook")]
        public string CollectionName { get; set; }
    }
}
