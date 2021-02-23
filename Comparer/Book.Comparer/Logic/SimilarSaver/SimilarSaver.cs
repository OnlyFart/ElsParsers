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
        
        public Task Save(SaveResult saveResult) {
            var compared = true;
            
            foreach (var similarBook in saveResult.SimilarBooks) {
                lock (similarBook.Similar) {
                    var update = Builders<BookInfo>.Update.Set(t => t.Similar, similarBook.Similar);
                    compared &= _repository.Update(GetEqualsFilter(similarBook), update).Result;
                }
            }
            
            lock (saveResult.Book.Similar) {
                UpdateProcessedBook(saveResult.Book, compared).Wait();
            }

            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Получение фильтра для апдейта книги
        /// </summary>
        /// <param name="book"></param>
        /// <returns></returns>
        private static FilterDefinition<BookInfo> GetEqualsFilter(BookInfo book) {
            var elsEqual = Builders<BookInfo>.Filter.Eq(t => t.ElsName, book.ElsName);
            var externalIdEqual = Builders<BookInfo>.Filter.Eq(t => t.ExternalId, book.ExternalId);

            return Builders<BookInfo>.Filter.And(elsEqual, externalIdEqual);
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
                .Set(t => t.Similar, book.Similar);
            
            await _repository.Update(GetEqualsFilter(book), update);
        }
    }
}
