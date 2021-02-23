using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Book.Comparer.Logic.Types;
using Book.Comparer.Logic.Utils;
using Core.Providers.Interfaces;
using Core.Types;
using MongoDB.Driver;
using NLog;

namespace Book.Comparer.Logic.BookGetter {
    public class CompareBookGetter : ICompareBookGetter {
        private static readonly Logger _logger = LogManager.GetLogger(nameof(Comparer));
        private readonly IRepository<BookInfo> _repository;
        private readonly Normalizer _normalizer;

        public CompareBookGetter(IRepository<BookInfo> repository, Normalizer normalizer) {
            _repository = repository;
            _normalizer = normalizer;
        }

        public async Task<IReadOnlyCollection<CompareBook>> Get() {
            ProjectionDefinition<BookInfo, BookInfo> projection = Builders<BookInfo>
                .Projection
                .Exclude("_id")
                .Exclude(b => b.Bib);

            var filterDefinition = Builders<BookInfo>.Filter
                .Where(t => t.Name != null && t.Name.Length > 0 && t.Authors != null && t.Authors.Length > 0);

            var books = await _repository.Read(filterDefinition, projection);

            _logger.Info("Начинаю преобразование книг в сравниваемые");

            return books.Select(book => CompareBook.Create(book, _normalizer)).ToList();
        }
    }
}
