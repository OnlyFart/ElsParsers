using CommandLine;
using Core.Configs;

namespace Parser.Core.Configs {
    public abstract class OptionsBase : IMongoConfig {
        [Option("th", Required = false, HelpText = "Максимальное число потоков для обращения к сервису", Default = 1)]
        public int MaxThread { get; set; }
        
        [Option("proxy", Required = false, HelpText = "Прокси в формате <host>:<port>", Default = "")]
        public string Proxy { get; set; }
        
        [Option("bs", Required = false, HelpText = "Размер пачки для сохранения", Default = 100)]
        public int BatchSize { get; set; }
        
        [Option("cs", Required = true, HelpText = "Строка подключения в MongoDb")]
        public string ConnectionString { get; set; }
        
        [Option("db", Required = false, HelpText = "Название базы данных", Default = "ELS")]
        public string DatabaseName { get; set; }
        
        [Option("cn", Required = false, HelpText = "Название коллекции", Default = "Books")]
        public string CollectionName { get; set; }
    }
}
