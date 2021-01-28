using System.Threading.Tasks;
using CommandLine;
using IprBookShop.Parser.Configs;
using Ninject;
using Parser.Core.IoC;

namespace IprBookShop.Parser {
    class Program {
        private static async Task Main(string[] args) {
            await CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsedAsync(async options => {
                    var kernel = new StandardKernel(new CoreNinjectModule(options));
                    await kernel.Get<Logic.Parser>().Run();
                });
        }
    }
}
