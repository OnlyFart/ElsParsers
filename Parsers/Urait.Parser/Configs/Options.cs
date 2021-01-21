using CommandLine;
using Core.Configs;

namespace Urait.Parser.Configs {
    public class Options : OptionsBase, IParserConfig, IMongoConfig {
        [Option("cn", Required = false, HelpText = "Название коллекции", Default = "Urait")]
        public string CollectionName { get; set; }
    }
}
