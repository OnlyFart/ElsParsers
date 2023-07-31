using System.Threading.Tasks;
using CommandLine;
using Ninject;
using Parser.Core.Configs;
using Parser.Core.IoC;
using Parser.Core.Logic;

namespace Parser.Core; 

public class DefaultRunner {
    public static async Task Run<TParser, TOptions>(string[] args) where TParser : ParserBase where TOptions : OptionsBase {
        await CommandLine.Parser.Default.ParseArguments<TOptions>(args)
            .WithParsedAsync(async options => {
                var kernel = new StandardKernel(new CoreNinjectModule(options));
                await kernel.Get<TParser>().Run();
            });
    }
}