using System.Threading.Tasks;
using CommandLine;
using IprBookShop.Parser.Configs;
using Ninject;
using Parser = IprBookShop.Parser.Logic.Parser;

namespace IprBookShop.Parser {
    class Program {
        private static async Task Main(string[] args) {
            await CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsedAsync(async options => {
                    var kernel = new StandardKernel(new IoC.Ninject(options));
                    await kernel.Get<Logic.Parser>().Parse();
                });
        }
    }
}
