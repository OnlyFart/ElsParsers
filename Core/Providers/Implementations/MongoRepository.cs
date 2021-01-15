using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Core.Configs;
using Core.Providers.Interfaces;
using MongoDB.Driver;
using NLog;

namespace Core.Providers.Implementations {
    public class MongoRepository<T> : IRepository<T> {
        private static readonly Logger _logger = LogManager.GetLogger(nameof(MongoRepository<T>));
        
        private readonly IMongoConfig _config;
        private readonly IMongoCollection<T> _collection;
        
        public MongoRepository(IMongoConfig config) {
            _config = config;
            _collection = new MongoClient(_config.ConnectionString)
                .GetDatabase(_config.DatabaseName)
                .GetCollection<T>(_config.CollectionName);
        }

        public async Task<IEnumerable<TValue>> ReadProjection<TValue>(Expression<Func<T, TValue>> projection) {
            _logger.Info($"Выполняю загрузку из {_config.DatabaseName}/{_config.CollectionName}");
            
            var listAsync = await _collection.Find(Builders<T>.Filter.Empty).Project(projection).ToListAsync();
            
            _logger.Info($"Загружено {listAsync.Count} записей");
            
            return listAsync;
        }

        public Task CreateMany(IEnumerable<T> items) {
            var toSave = items.Where(t => t != null).ToList();
            return toSave.Count > 0 ? _collection.InsertManyAsync(toSave) : Task.CompletedTask;
        }
    }
}
