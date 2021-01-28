using CommandLine;
using Core.Configs;
using Parser.Core.Configs;

namespace BiblioClub.Parser.Configs {
    public class Options : OptionsBase, IParserConfig {
        [Option("si", Required = false, HelpText = "Начальный индекс для обхода", Default = 1)]
        public int StartIndex { get; set; }
        
        [Option("ei", Required = false, HelpText = "Конечный индекс для обхода", Default = 1000000)]
        public int EndIndex { get; set; }
    }
}
