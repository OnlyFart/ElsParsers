using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Book.Comparer.Types;
using Core.Providers.Interfaces;
using Core.Types;
using MongoDB.Driver;

namespace Book.Comparer.Logic.SimilarSaver {
    public class SimilarSaver : ISimilarSaver {
        private readonly IRepository<BookInfo> _repository;

        public SimilarSaver(IRepository<BookInfo> repository) {
            _repository = repository;
        }
        
        public async Task Save(SaveResult saveResult) {
            var compared = true;
            
            if (saveResult.SimilarBooks.Count > 0) {
                var updates = new List<WriteModel<BookInfo>>(saveResult.SimilarBooks.Count);

                foreach (var similarBook in saveResult.SimilarBooks) {
                    lock (similarBook.SimilarBooks) {
                        var clone = new Dictionary<string, HashSet<SimilarInfo>>(similarBook.SimilarBooks);
                        var update = Builders<BookInfo>.Update.Set(t => t.SimilarBooks, clone);
                        updates.Add(new UpdateManyModel<BookInfo>(GetEqualsFilter(similarBook), update));
                    }
                }

                compared = await _repository.UpdateMany(updates);
            }
            
            await UpdateProcessedBook(saveResult.Book, compared);
        }
        
        /// <summary>
        /// Получение фильтра для апдейта книги
        /// </summary>
        /// <param name="book"></param>
        /// <returns></returns>
        private static FilterDefinition<BookInfo> GetEqualsFilter(BookInfo book) {
            return Builders<BookInfo>.Filter.Eq(t => t.Id, book.Id);
        }

        /// <summary>
        /// Сохранение результатов поиска похожести для книги
        /// </summary>
        /// <param name="book"></param>
        /// <param name="compared">Можно ли пометить книгу, как успешно сравненную</param>
        /// <returns></returns>
        private async Task UpdateProcessedBook(BookInfo book, bool compared) {
            var update = Builders<BookInfo>.Update
                .Set(t => t.Compared, compared)
                .Set(t => t.Authors, book.Authors)
                .Set(t => t.Publisher, book.Publisher)
                .Set(t => t.Name, book.Name)
                .Set(t => t.SimilarBooks, book.SimilarBooks);

            await _repository.Update(GetEqualsFilter(book), update);
        }
    }
}
