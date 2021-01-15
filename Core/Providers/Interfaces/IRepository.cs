using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Core.Providers.Interfaces {
    public interface IRepository<T> {
        Task<IEnumerable<TValue>> ReadProjection<TValue>(Expression<Func<T, TValue>> projection);

        Task CreateMany(IEnumerable<T> items);
    }
}
