using CommandLine;
using Core.Configs;

namespace BiblioclubParser.Configs {
    public class Options : IParserConfig, IMongoConfig {
        [Option("proxy", Required = false, HelpText = "Прокси в формате <host>:<port>", Default = "")]
        public string Proxy { get; set; }
        
        [Option("th", Required = false, HelpText = "Максимальное число потоков для обращения к сервису", Default = 1)]
        public int MaxThread { get; set; }
        
        [Option("bs", Required = false, HelpText = "Размер пачки для сохранения", Default = 100)]
        public int BatchSize { get; set; }
        
        [Option("si", Required = false, HelpText = "Начальный индекс для обхода", Default = 1)]
        public int StartIndex { get; set; }
        
        [Option("ei", Required = false, HelpText = "Конечный индекс для обхода", Default = 1000000)]
        public int EndIndex { get; set; }
        
        [Option("cs", Required = true, HelpText = "Строка подключения в MongoDb")]
        public string ConnectionString { get; set; }
        
        [Option("db", Required = false, HelpText = "Название базы данных", Default = "Biblioclub")]
        public string DatabaseName { get; set; }
        
        [Option("cn", Required = false, HelpText = "Название коллекции", Default = "Books")]
        public string CollectionName { get; set; }
    }
}
