﻿using System.Threading.Tasks;
using CommandLine;
using Ninject;
using Urait.Parser.Configs;

namespace Urait.Parser {
    class Program {
        private static async Task Main(string[] args) {
            await CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsedAsync(async options => {
                    var kernel = new StandardKernel(new IoC.Ninject(options));
                    await kernel.Get<Logic.Parser>().Run();
                });
        }
    }
}