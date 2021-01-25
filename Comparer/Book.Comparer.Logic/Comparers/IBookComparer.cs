using Book.Comparer.Logic.Types;
using Core.Types;

namespace Book.Comparer.Logic.Comparers {
    public interface IBookComparer {
        BookComparerResult Compare(CompareBook book1, CompareBook book2);
    }
}
