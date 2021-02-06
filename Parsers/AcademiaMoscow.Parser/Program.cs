using System.Threading.Tasks;
using CommandLine;
using Ninject;
using Parser.Core.IoC;
using Parser.Core.Configs;

namespace AcademiaMoscow.Parser {
    class Program {
        private static async Task Main(string[] args) {
            await CommandLine.Parser.Default.ParseArguments<OptionsBase>(args)
                .WithParsedAsync(async options => {
                    var kernel = new StandardKernel(new CoreNinjectModule(options));
                    await kernel.Get<Logic.Parser>().Run();
                });
        }
    }
}