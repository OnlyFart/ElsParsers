using CommandLine;
using Core.Configs;

namespace UraitParser.Configs {
    public class Options : OptionsBase, IParserConfig, IMongoConfig {
        [Option("cn", Required = false, HelpText = "Название коллекции", Default = "Urait")]
        public string CollectionName { get; set; }
    }
}
