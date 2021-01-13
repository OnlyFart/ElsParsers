using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Configs;
using Core.Providers.Interfaces;
using Core.Types;
using MongoDB.Driver;
using NLog;

namespace Core.Providers.Implementations {
    public class MongoBooksProvider<T> : IBooksProvider<T> where T : BookBase {
        private static readonly Logger _logger = LogManager.GetLogger(nameof(MongoBooksProvider<T>));
        
        private readonly IMongoConfig _config;
        private readonly IMongoCollection<T> _collection;
        
        public MongoBooksProvider(IMongoConfig config) {
            _config = config;
            _collection = new MongoClient(_config.ConnectionString)
                .GetDatabase(_config.DatabaseName)
                .GetCollection<T>(_config.CollectionName);
        }

        public async Task<IEnumerable<long>> GetProcessed() {
            _logger.Info($"Выполняю загрузку текущих книг из {_config.ConnectionString}");

            var listAsync = await _collection.Find(Builders<T>.Filter.Empty).Project(t => t.Id).ToListAsync();
            
            _logger.Info($"Загружено {listAsync.Count} записей");
            
            return listAsync;
        }

        public Task Save(IEnumerable<T> books) {
            var toSave = books.Where(t => t != null).ToList();
            return toSave.Count > 0 ? _collection.InsertManyAsync(toSave) : Task.CompletedTask;
        }
    }
}
