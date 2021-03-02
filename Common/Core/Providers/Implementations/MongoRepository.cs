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

        public async Task<IReadOnlyCollection<TValue>> Read<TValue>(FilterDefinition<T> filter, Expression<Func<T, TValue>> projection) {
            _logger.Info($"Выполняю загрузку из {_config.DatabaseName}/{_config.CollectionName}");
            
            var listAsync = await _collection.Find(filter).Project(projection).ToListAsync();
            
            _logger.Info($"Из {_config.DatabaseName}/{_config.CollectionName} {listAsync.Count} записей");
            
            return listAsync;
        }

        public async Task<bool> Update(FilterDefinition<T> filter, UpdateDefinition<T> update) {
            try {
                 await _collection.UpdateOneAsync(filter, update);
                 return true;
            } catch (Exception ex) {
                _logger.Error(ex);
            }

            return false;
        }
        
        public async Task<bool> UpdateMany(IReadOnlyCollection<WriteModel<T>> requests) {
            if (requests.Count == 0) {
                return true;
            }
            
            try {
                await _collection.BulkWriteAsync(requests);
                return true;
            } catch (Exception ex) {
                _logger.Error(ex);
            }

            return false;
        }

        public async Task<IReadOnlyCollection<TValue>> Read<TValue>(FilterDefinition<T> filter, ProjectionDefinition<T, TValue> projection) {
            _logger.Info($"Выполняю загрузку из {_config.DatabaseName}/{_config.CollectionName}");

            var listAsync = await _collection.Find(filter).Project(projection).ToListAsync();

            _logger.Info($"Загружено {listAsync.Count} записей");

            return listAsync;
        }

        public async Task CreateMany(IEnumerable<T> items) {
            var toSave = items.Where(t => t != null).ToList();

            if (toSave.Count > 0) {
                _logger.Info($"Сохраняем {toSave.Count} объектов");
                
                try {
                    await _collection.InsertManyAsync(toSave);
                } catch (Exception ex) {
                    _logger.Error(ex);
                }
            }
        }
    }
}
