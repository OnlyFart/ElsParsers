using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Book.Comparer.Logic.Configs;
using Book.Comparer.Logic.Logic;
using Book.Comparer.Logic.Types;
using Book.Comparer.Logic.Utils;
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

        private Task<IReadOnlyCollection<BookInfo>> GetBooks() {
            ProjectionDefinition<BookInfo, BookInfo> bookProj = Builders<BookInfo>.Projection
                .Exclude(b => b.Bib)
                .Exclude(b => b.Year)
                .Exclude(b => b.Pages);

            var bookFilter = Builders<BookInfo>.Filter
                .Where(t => t.Name != null && 
                    t.Name.Length > 0 && 
                    t.Authors != null && 
                    t.Authors.Length > 0 && 
                    t.ElsName != Const.BIB_ELS);

            return _repository.Read(bookFilter, bookProj);  
        }

        private Task<IReadOnlyCollection<BookInfo>> GetBibBooks() {
            var bibBookProj = Builders<BookInfo>.Projection.Expression(t => t);
            var bibBookFilter = Builders<BookInfo>.Filter.Where(t => t.ElsName == Const.BIB_ELS);
            return _repository.Read(bibBookFilter, bibBookProj);
        }

        private void ParseBibBooks(IReadOnlyCollection<BookInfo> books, IReadOnlyCollection<BookInfo> bibBooks) {
            var toParse = bibBooks.Where(b => !b.Compared).ToList();
            if (toParse.Count <= 0) {
                return;
            }
            
            _logger.Info($"Обнаружено {toParse.Count} книг, для которых необходимо распарсить БЗ.");
            var parser = GetBibParser(books);
            _logger.Info("Создание парсера закончено. Начинаю парсинг.");
                
            foreach (var bibBook in toParse) {
                var (authors, name, publisher) = parser.Parse(bibBook.Bib);

                bibBook.Authors = authors;
                bibBook.Name = name;
                bibBook.Publisher = publisher;
            }
                
            _logger.Info("Парсинг БЗ закончен.");
        }
        
        public async Task<IReadOnlyCollection<CompareBook>> Get() {
            var books = GetBooks();
            var bibBooks = GetBibBooks();

            ParseBibBooks(await books, await bibBooks);

            _logger.Info("Начинаю преобразование книг в сравниваемые");

            return (await books).Union(await bibBooks).AsParallel().Select(book => CompareBook.Create(book, _normalizer)).ToList();
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
