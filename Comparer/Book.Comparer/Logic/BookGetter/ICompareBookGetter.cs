using System.Collections.Generic;
using System.Threading.Tasks;
using Book.Comparer.Logic.Types;

namespace Book.Comparer.Logic.BookGetter {
    public interface ICompareBookGetter {
        Task<IReadOnlyCollection<CompareBook>> Get();
    }
}
