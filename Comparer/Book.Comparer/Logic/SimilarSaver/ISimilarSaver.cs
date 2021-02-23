using System.Threading.Tasks;
using Book.Comparer.Types;

namespace Book.Comparer.Logic.SimilarSaver {
    public interface ISimilarSaver {
        Task Save (SaveResult saveResult);
    }
}
