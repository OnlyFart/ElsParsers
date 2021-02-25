using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Book.Comparer.Logic;
using Book.Comparer.Logic.Comparers;
using Book.Comparer.Logic.Configs;
using Book.Comparer.Logic.Utils;
using Core.Configs;
using Core.Providers.Implementations;
using Core.Types;
using Newtonsoft.Json;

namespace Sandbox {
    class Program {
        private class MongoConfig : IMongoConfig {
            public string ConnectionString => "mongodb://localhost:27017/";
            public string DatabaseName => "ELS";
            public string CollectionName => "Books";
        }

        private class ComparerConfig : IBookComparerConfig {
            public double LevensteinBorder { get; set; }
            public double IntersectBorder { get; set; }

            public ComparerConfig(double lb, double ib) {
                LevensteinBorder = lb;
                IntersectBorder = ib;
            }
        }

        private static async Task Main(string[] args) {
            if (File.Exists("similar.txt")) {
                File.Delete("similar.txt");
            }

            var appConfig = JsonConvert.DeserializeObject<Dictionary<string, object>>(await File.ReadAllTextAsync("appsettings.json"));
            
            var mongoRepository = new MongoRepository<BookInfo>(new MongoConfig());
            
            var normalizerConfig = JsonConvert.DeserializeObject<NormalizerConfig>(appConfig["NormalizerConfig"].ToString());
            var normalizer = new Normalizer(normalizerConfig);

            var compareGetter = new CompareGetter(mongoRepository, normalizer);
            var bookComparer = new BookComparer(new ComparerConfig(0.3, 0.4));
            var saver = new Saver();
            var comparerConfig = new Sandbox.ComparerConfig(7);

            await new Comparer(compareGetter, saver, bookComparer, comparerConfig).Run();

            Console.WriteLine("Hello World!");
        }
    }
}
