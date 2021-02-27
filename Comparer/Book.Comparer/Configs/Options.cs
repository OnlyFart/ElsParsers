using Book.Comparer.Logic.Configs;
using CommandLine;
using Core.Configs;

namespace Book.Comparer.Configs {
    public class Options : IMongoConfig, IComparerConfig, IBookComparerConfig {
        [Option("th", Required = false, HelpText = "Максимальное число потоков обработки книг", Default = 1)]
        public int MaxThread { get; init; }
        
        [Option("lb", Required = false, HelpText = "Максимальная разница между двумя строками по левенштейну", Default = 0.3)]
        public double LevensteinBorder { get; init; }
        
        [Option("ib", Required = false, HelpText = "Максимальная разница между двумя строками по словам", Default = 0.4)]
        public double IntersectBorder { get; init; }

        [Option("cs", Required = true, HelpText = "Строка подключения в MongoDb")]
        public string ConnectionString { get; set; }
        
        [Option("db", Required = false, HelpText = "Название базы данных", Default = "ELS")]
        public string DatabaseName { get; set; }
        
        [Option("cn", Required = false, HelpText = "Название коллекции", Default = "Books")]
        public string CollectionName { get; set; }
    }
}
