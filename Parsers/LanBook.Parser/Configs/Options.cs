using CommandLine;
using Parser.Core.Configs;

namespace LanBook.Parser.Configs {
    public class Options : OptionsBase, IParserConfig {
        [Option("sp", Required = false, HelpText = "Стартовая страница для обхода", Default = 1)]
        public int StartPage { get; set; }
    }
}
