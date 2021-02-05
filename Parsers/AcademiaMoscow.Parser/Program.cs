using System;
using System.Threading.Tasks;
using CommandLine;
using Ninject;
using Parser.Core.IoC;
using AcademiaMoscow.Parser.Configs;


namespace AcademiaMoscow.Parser {
    class Program {
        private static async Task Main(string[] args) {
            args = new string [] { "--cs",  "mongodb+srv://library123:library123@cluster0.sng0y.mongodb.net/library?retryWrites=true&w=majority",  "--th", "10"};

            // var options = CommandLine.Parser.Default.ParseArguments<Options>(args);
            // var kernel = new StandardKernel(new CoreNinjectModule(options));
            // var parser = new Logic.Parser(new Options(), null);
            
            //--cs "mongodb+srv://library123:library123@Test?retryWrites=true&w=majority" --th 10
            await CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsedAsync(async options => {
                    var kernel = new StandardKernel(new CoreNinjectModule(options));
                    await kernel.Get<Logic.Parser>().Run();
                });
        }
    }
}