﻿using System.Threading.Tasks;
using Book.Comparer.Configs;
using CommandLine;
using Ninject;

namespace Book.Comparer {
    class Program {
        private static async Task Main(string[] args) {
            await Parser.Default.ParseArguments<Options>(args)
                .WithParsedAsync(async options => {
                    var kernel = new StandardKernel(new IoC.Ninject(options));
                    await kernel.Get<Logic.Comparer>().Run();
                });
        }
    }
}
