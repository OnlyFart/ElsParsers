using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Book.Comparer.Configs;
using Book.Comparer.Logic.BookGetter;
using Book.Comparer.Logic.Comparers;
using Book.Comparer.Logic.SimilarSaver;
using Book.Comparer.Logic.Types;
using Book.Comparer.Types;
using Core.Extensions;
using Core.Types;
using NLog;

namespace Book.Comparer.Logic {
    public class Comparer {
        private static readonly Logger _logger = LogManager.GetLogger(nameof(Comparer));
        
        private readonly ICompareBookGetter _compareBookGetter;
        private readonly ISimilarSaver _similarSaver;
        private readonly IBookComparer _bookComparer;
        private readonly IComparerConfig _comparerConfig;

        public Comparer(ICompareBookGetter compareCompareBookGetter, ISimilarSaver similarSaver, IBookComparer bookComparer, IComparerConfig comparerConfig) {
            _compareBookGetter = compareCompareBookGetter;
            _similarSaver = similarSaver;
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
            foreach (var word in thisBook.Key.NameTokens) {
                if (!wordToBooks.TryGetValue(word, out var otherBooks)) {
                    continue;
                }
                
                foreach (var otherBook in otherBooks.Where(o => o.IsComparedBook && !thisBook.BookInfo.Equals(o.BookInfo))) {
                    yield return otherBook;
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

            if (!thisBook.IsComparedBook) {
                return result;
            }

            foreach (var otherBook in GetOtherBooks(thisBook, wordToBooks)) {
                var comparerResult = _bookComparer.Compare(thisBook, otherBook);
                if (!comparerResult.Author.Success || !comparerResult.Name.Success) {
                    continue;
                }

                thisBook.BookInfo.AddSimilar(otherBook.BookInfo, comparerResult);
                otherBook.BookInfo.AddSimilar(thisBook.BookInfo, comparerResult);

                result.SimilarBooks.Add(otherBook.BookInfo);

                if (_logger.IsDebugEnabled) {
                    var sb = new StringBuilder();
                    sb.AppendLine($"{thisBook.BookInfo.Name} -> {thisBook.Key.Name}");
                    sb.AppendLine($"{otherBook.BookInfo.Name} -> {otherBook.Key.Name}");
                    sb.AppendLine($"{comparerResult.Name.Diff:0.00}");
                    sb.AppendLine();
                    sb.AppendLine($"{thisBook.BookInfo.Authors}");
                    sb.AppendLine($"{otherBook.BookInfo.Authors}");
                    sb.AppendLine($"{comparerResult.Author.Diff:0.00}");
                    sb.AppendLine();

                    _logger.Debug(sb.ToString);
                }
            }

            return result;
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
                foreach (var word in book.Key.NameTokens) {
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
        /// Запуск процесса поиска "похожих" книг
        /// </summary>
        /// <returns></returns>
        public async Task Run() {
            var books = await _compareBookGetter.Get();
            var wordToBooks = CreateWordToBooksMap(books);
            
            var findSimilarBlock = new TransformBlock<CompareBook, SaveResult>(book => FindSimilar(book, wordToBooks), new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = _comparerConfig.MaxThread, EnsureOrdered = false});
            findSimilarBlock.CompleteMessage(_logger, "Закончили сравнение всех книг. Ждем сохранения.");

            var i = books.Count(b => b.BookInfo.Compared);
            var updateBooks = new ActionBlock<SaveResult>(async t => {
                await _similarSaver.Save(t);
                _logger.Info($"{++i}/{books.Count}");
            });
            updateBooks.CompleteMessage(_logger, "Сохранение завершено.");

            findSimilarBlock.LinkTo(updateBooks);
            foreach (var book in books.Where(b => !b.BookInfo.Compared)) {
                findSimilarBlock.Post(book);
            }

            await DataflowExtension.WaitBlocks(findSimilarBlock, updateBooks);
        }
    }
}