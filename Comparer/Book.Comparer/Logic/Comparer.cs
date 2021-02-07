using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Book.Comparer.Configs;
using Book.Comparer.Logic.Comparers;
using Book.Comparer.Logic.Types;
using Book.Comparer.Logic.Utils;
using Book.Comparer.Types;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Types;
using MongoDB.Driver;
using NLog;

namespace Book.Comparer.Logic {
    public class Comparer {
        private static readonly Logger _logger = LogManager.GetLogger(nameof(Comparer));
        
        private readonly IRepository<BookInfo> _bookRepository;
        private readonly Normalizer _normalizer;
        private readonly IBookComparer _bookComparer;
        private readonly IComparerConfig _comparerConfig;

        public Comparer(IRepository<BookInfo> bookRepository, Normalizer normalizer, IBookComparer bookComparer, IComparerConfig comparerConfig) {
            _bookRepository = bookRepository;
            _normalizer = normalizer;
            _bookComparer = bookComparer;
            _comparerConfig = comparerConfig;
        }

        /// <summary>
        /// Получение уникального списка потенциально похожих книг
        /// </summary>
        /// <param name="thisBook">Книга, для которой ищем похожие</param>
        /// <param name="wordToBooks">Обратный индекс "слово из названия" -> "список книг"</param>
        /// <returns></returns>
        private static IEnumerable<CompareBook> GetOtherBooks(CompareBook thisBook, IReadOnlyDictionary<string, List<CompareBook>> wordToBooks) {
            var set = new HashSet<CompareBook>();

            foreach (var word in thisBook.Key.NameWords) {
                if (wordToBooks.TryGetValue(word, out var otherBooks)) {
                    foreach (var otherBook in otherBooks.Where(otherBook => set.Add(otherBook))) {
                        yield return otherBook;
                    }
                }
            }
        }

        /// <summary>
        /// Поиск похожих книг
        /// </summary>
        /// <param name="thisBook">Книга, для которой ищем похожие</param>
        /// <param name="wordToBooks">Обратный индекс "слово из названия" -> "список книг"</param>
        /// <returns></returns>
        private SaveResult FindSimilar(CompareBook thisBook, IReadOnlyDictionary<string, List<CompareBook>> wordToBooks) {
            var result = new SaveResult {
                Book = thisBook.BookInfo,
                SimilarBooks = new HashSet<BookInfo>()
            };
            
            foreach (var otherBook in GetOtherBooks(thisBook, wordToBooks)) {
                if (thisBook.BookInfo.Equals(otherBook.BookInfo)) {
                    continue;
                }

                if (!thisBook.BookInfo.Similar.IsNullOrEmpty() && thisBook.BookInfo.Similar.Contains(otherBook.BookInfo)) {
                    continue;
                }

                var comparerResult = _bookComparer.Compare(thisBook, otherBook);
                if (!comparerResult.Author.Success || !comparerResult.Name.Success) {
                    continue;
                }

                thisBook.BookInfo.AddSimilar(otherBook.BookInfo, comparerResult);
                otherBook.BookInfo.AddSimilar(thisBook.BookInfo, comparerResult);

                result.SimilarBooks.Add(otherBook.BookInfo);

                _logger.Info($"{thisBook.BookInfo.Name} -> {thisBook.Key.Name}");
                _logger.Info($"{otherBook.BookInfo.Name} -> {otherBook.Key.Name}");
                _logger.Info($"{comparerResult.Name.Diff:0.00}");
                _logger.Info(string.Empty);
                _logger.Info($"{thisBook.BookInfo.Authors}");
                _logger.Info($"{otherBook.BookInfo.Authors}");
                _logger.Info($"{comparerResult.Author.Diff:0.00}");
                _logger.Info(string.Empty);
            }
            
            return result;
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
            
            await _bookRepository.Update(GetEqualsFilter(book), update);
        }

        /// <summary>
        /// Сохранение результатов поиска похожести для книги
        /// </summary>
        /// <param name="saveResult"></param>
        /// <returns></returns>
        private void UpdateSimilarBooks(SaveResult saveResult) {
            var compared = true;
            
            foreach (var similarBook in saveResult.SimilarBooks) {
                lock (similarBook.Similar) {
                    var update = Builders<BookInfo>.Update.Set(t => t.Similar, similarBook.Similar);
                    compared &= _bookRepository.Update(GetEqualsFilter(similarBook), update).Result;
                }
            }
            
            lock (saveResult.Book.Similar) {
                UpdateProcessedBook(saveResult.Book, compared).Wait();
            }
        }

        /// <summary>
        /// Построение обратного индекса "слово из названия" -> "список книг"
        /// </summary>
        /// <param name="books"></param>
        /// <returns></returns>
        private static Dictionary<string, List<CompareBook>> CreateWordToBooksMap(IEnumerable<CompareBook> books) {
            _logger.Info("Создание обратного индекса.");
            var result = new Dictionary<string, List<CompareBook>>();

            foreach (var book in books) {
                foreach (var word in book.Key.NameWords) {
                    if (!result.TryGetValue(word, out var res)) {
                        res = new List<CompareBook>();
                        result[word] = res;
                    }
                    
                    res.Add(book);
                }
            }

            _logger.Info($"Обратный индекс построен. В индексе {result.Count} слов.");
            return result;
        }

        /// <summary>
        /// Получение их базы всех книг, у которых указаны имя и авторы
        /// </summary>
        /// <returns></returns>
        private async Task<List<CompareBook>> GetBooks() {
            ProjectionDefinition<BookInfo, BookInfo> projection = Builders<BookInfo>
                .Projection
                .Exclude("_id")
                .Exclude(b => b.Bib);
            
            var filterDefinition = Builders<BookInfo>.Filter
                .Where(t => t.Name != null && t.Name.Length > 0 && t.Authors != null && t.Authors.Length > 0);

            var result = new List<CompareBook>();
            var books = await _bookRepository.Read(filterDefinition, projection);

            _logger.Info("Начинаю преобразование книг в сравниваемые");
            
            foreach (var book in books) {
                var compareBook = new CompareBook(book);
                compareBook.Init(_normalizer);
                result.Add(compareBook);
            }

            return result;
        }

        /// <summary>
        /// Запуск процесса поиска "похожих" книг
        /// </summary>
        /// <returns></returns>
        public async Task Run() {
            var books = await GetBooks();
            var wordToBooks = CreateWordToBooksMap(books);
            
            var findSimilarBlock = new TransformBlock<CompareBook, SaveResult>(book => FindSimilar(book, wordToBooks), new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = _comparerConfig.MaxThread });
            findSimilarBlock.CompleteMessage(_logger, "Закончили сравнение всех книг. Ждем сохранения.");
            
            var updateBooks = new ActionBlock<SaveResult>(UpdateSimilarBooks);
            updateBooks.CompleteMessage(_logger, "Сохранение завершено.");

            findSimilarBlock.LinkTo(updateBooks);
            foreach (var book in books.Where(b => !b.BookInfo.Compared)) {
                findSimilarBlock.Post(book);
            }

            await DataflowExtension.WaitBlocks(findSimilarBlock, updateBooks);
        }
    }
}