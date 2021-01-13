using CommandLine;
using Core.Configs;

namespace ZnaniumParser.Configs {
    public class Options : OptionsBase, IParserConfig, IMongoConfig {
        [Option("cn", Required = false, HelpText = "Название коллекции", Default = "Znarium")]
        public string CollectionName { get; set; }
    }
}
