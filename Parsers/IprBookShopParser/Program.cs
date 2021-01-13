using System.Threading.Tasks;
using CommandLine;
using IprBookShopParser.Configs;
using IprBookShopParser.Logic;
using Ninject;
using Parser = IprBookShopParser.Logic.Parser;

namespace IprBookShopParser {
    class Program {
        private static async Task Main(string[] args) {
            await CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsedAsync(async options => {
                    var kernel = new StandardKernel(new IoC.Ninject(options));
                    await kernel.Get<Parser>().Parse();
                });
        }
    }
}
