using System.Threading.Tasks;
using CommandLine;
using Ninject;
using ZnaniumParser.Configs;

namespace ZnaniumParser {
    class Program {
        private static async Task Main(string[] args) {
            await Parser.Default.ParseArguments<Options>(args)
                .WithParsedAsync(async options => {
                    var kernel = new StandardKernel(new IoC.Ninject(options));
                    await kernel.Get<Logic.Parser>().Parse();
                });
        }
    }
}
