using System.Threading.Tasks;
using CommandLine;
using IprBookShopParser.Configs;
using IprBookShopParser.Logic;
using Ninject;

namespace IprBookShopParser {
    class Program {
        private static async Task Main(string[] args) {
            await Parser.Default.ParseArguments<Options>(args)
                .WithParsedAsync(async options => {
                    var kernel = new StandardKernel(new IoC.Ninject(options));
                    await kernel.Get<IprParser>().Parse();
                });
        }
    }
}
