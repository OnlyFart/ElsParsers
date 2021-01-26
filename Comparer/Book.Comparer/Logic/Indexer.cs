using System.Linq;
using System.Threading.Tasks;
using Core.Configs;
using Core.Types;
using MongoDB.Driver;
using NLog;

namespace Book.Comparer.Logic {
    public class Indexer {
        private static readonly Logger _logger = LogManager.GetLogger(nameof(Indexer));
        
        private readonly IMongoCollection<BookInfo> _collection;
        private const string INDEX_NAME = "IX_ElsName_ExternalId";

        public Indexer(IMongoConfig config) {
            _collection = new MongoClient(config.ConnectionString)
                .GetDatabase(config.DatabaseName)
                .GetCollection<BookInfo>(config.CollectionName);
        }

        public async Task CreateIndex() {
            var currentIndexes = await _collection.Indexes.ListAsync().ContinueWith(t => t.Result.ToList());
            if (currentIndexes.Any(t => t.Names.Contains("name") && t.GetElement("name").Value.ToString() == INDEX_NAME)) {
                _logger.Info("Создание индекса не требуется");
                return;
            }
            
            _logger.Info($"Создаю индекс {INDEX_NAME}");
            var index = Builders<BookInfo>.IndexKeys.Ascending(t => t.ElsName).Ascending(t => t.ExternalId);
            await _collection.Indexes.CreateOneAsync(new CreateIndexModel<BookInfo>(index, new CreateIndexOptions{ Name = INDEX_NAME }));
            _logger.Info($"Индекс {INDEX_NAME} успешно создан");
        }
    }
}
