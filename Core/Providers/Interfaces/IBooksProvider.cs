using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Providers.Interfaces {
    public interface IBooksProvider<in T> {
        Task<IEnumerable<long>> GetProcessed();

        Task Save(IEnumerable<T> books);
    }
}
