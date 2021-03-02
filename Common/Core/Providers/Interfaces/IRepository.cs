using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Core.Providers.Interfaces {
    public interface IRepository<T> {
        Task<IReadOnlyCollection<TValue>> Read<TValue>(FilterDefinition<T> filter, ProjectionDefinition<T, TValue> projection);

        Task<IReadOnlyCollection<TValue>> Read<TValue>(FilterDefinition<T> filter, Expression<Func<T, TValue>> projection);

        Task<bool> Update(FilterDefinition<T> filter, UpdateDefinition<T> update);

        Task<bool> UpdateMany(IReadOnlyCollection<WriteModel<T>> requests);

        Task CreateMany(IEnumerable<T> items);
    }
}
