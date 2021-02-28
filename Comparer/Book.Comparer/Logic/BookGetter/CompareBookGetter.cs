using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Book.Comparer.Logic.Configs;
using Book.Comparer.Logic.Logic;
using Book.Comparer.Logic.Types;
using Book.Comparer.Logic.Utils;
using Book.Comparer.Types;
using Core.Extensions;
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
            var projection = Builders<BookInfo>.Projection.Expression(t => t);

            var filterDefinition = Builders<BookInfo>.Filter
                .Where(t => t.Name != null && 
                    t.Name.Length > 0 && 
                    t.Authors != null && 
                    t.Authors.Length > 0 || 
                    t.ElsName == Const.BIB_ELS);

            var books = await _repository.Read(filterDefinition, projection);
            var bibBooks = books.Where(t => t.ElsName == Const.BIB_ELS && !t.Compared).ToList();
            
            if (bibBooks.Count > 0) {
                _logger.Info($"Обнаружено {bibBooks.Count} книг, для которых необходимо распарсить БЗ.");
                var bibParser = GetBibParser(books);
                _logger.Info("Создание парсера закончено. Начинаю парсинг.");
                
                foreach (var bibBook in bibBooks) {
                    var (authors, name, publisher) = bibParser.Parse(bibBook.Bib);

                    bibBook.Authors = authors;
                    bibBook.Name = name;
                    bibBook.Publisher = publisher;
                }
                
                _logger.Info("Парсинг БЗ закончен.");
            }

            _logger.Info("Начинаю преобразование книг в сравниваемые");

            return books.AsParallel().Select(book => CompareBook.Create(book, _normalizer)).ToList();
        }

        private BibParser GetBibParser(IReadOnlyCollection<BookInfo> books) {
            var authors = GetAuthors(books, _normalizer);
            var publishers = GetPublishers(books);
            
            var config = new BibParserConfig(authors, publishers);
            return new BibParser(_normalizer, config);
        }
        
        private static HashSet<string> GetAuthors(IEnumerable<BookInfo> books, Normalizer normalizer) {
            var result = new HashSet<string>();
            foreach (var book in books.Where(b => !string.IsNullOrWhiteSpace(b.Authors))) {
                var authors = book.Authors.Split(normalizer.AuthorsSeparator, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var author in authors) {
                    var tokens = GetTokens(author, 2)
                        .Where(t => char.IsUpper(t[0]) && !normalizer.NonSingAuthorWords.Contains(t))
                        .ToList();

                    // Отсекаю всякую дичь, которая иногда проскакивает в авторах
                    if (tokens.Count <= 5) {
                        foreach (var token in tokens) {
                            result.Add(token);
                        }
                    }
                }
            }

            return result;
        }
        
        private static HashSet<string> GetPublishers(IEnumerable<BookInfo> books) {
            return books.Where(book => !string.IsNullOrWhiteSpace(book.Publisher))
                .Select(book => book.Publisher.Clean().ToLowerInvariant())
                .ToHashSet();
        }
        
        private static IEnumerable<string> GetTokens(string bib, int minLength = 1) {
            return string.IsNullOrWhiteSpace(bib) ? 
                Enumerable.Empty<string>() : 
                Regex.Split(bib, "[\\W_]")
                    .Where(token => !string.IsNullOrWhiteSpace(token) && token.Length > minLength && !int.TryParse(token, out _));
        }
    }
}
