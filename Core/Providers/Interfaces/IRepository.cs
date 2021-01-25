using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Core.Providers.Interfaces {
    public interface IRepository<T> {
        Task<IEnumerable<TValue>> Read<TValue>(FilterDefinition<T> filter, ProjectionDefinition<T, TValue> projection);

        Task<IEnumerable<TValue>> Read<TValue>(FilterDefinition<T> filter, Expression<Func<T, TValue>> projection);

        Task Update(FilterDefinition<T> filter, UpdateDefinition<T> update);

        Task CreateMany(IEnumerable<T> items);
    }
}
