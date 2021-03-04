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
                .Exclude(b => b.ISBN)
                .Exclude(b => b.ISSN)
                .Exclude(b => b.Year)
                .Exclude(b => b.Pages);

            return _repository.Read(Builders<BookInfo>.Filter.Empty, bookProj);  
        }

        private static bool NeedParseBib(BookInfo book) {
            return !book.Compared &&
                (string.IsNullOrWhiteSpace(book.Authors) ||
                    string.IsNullOrWhiteSpace(book.Name) ||
                    string.IsNullOrWhiteSpace(book.Publisher)) &&
                !string.IsNullOrWhiteSpace(book.Bib);
        }

        private CompareBook CreateCompareBook(BookInfo book, BibParser parser) {
            if (NeedParseBib(book)) {
                var (authors, name, publisher) = parser.Parse(book.Bib);

                if (string.IsNullOrWhiteSpace(book.Authors)) {
                    book.Authors = authors;
                }

                if (string.IsNullOrWhiteSpace(book.Name)) {
                    book.Name = name;
                }

                if (string.IsNullOrWhiteSpace(book.Publisher)) {
                    book.Publisher = publisher;
                }
            }

            return CompareBook.Create(book, _normalizer);
        }

        public async Task<IReadOnlyCollection<CompareBook>> Get() {
            var books = await GetBooks();
            var parser = CreateBibParser(books);
            
            _logger.Info("Начинаю преобразование книг в сравниваемые");

            return books
                .AsParallel()
                .Select(book => CreateCompareBook(book, parser))
                .ToList();
        }

        private BibParser CreateBibParser(IReadOnlyCollection<BookInfo> books) {
            _logger.Info("Создаю парсер БЗ");
            
            var authors = GetAuthors(books, _normalizer);
            var publishers = GetPublishers(books);
            
            var config = new BibParserConfig(authors, publishers);
            
            _logger.Info("Парсер БЗ создан");
            _logger.Info($"Авторов {authors.Count}");
            _logger.Info($"Издательств {publishers.Count}");
            
            return new BibParser(_normalizer, config);
        }
        
        private static HashSet<string> GetAuthors(IEnumerable<BookInfo> books, Normalizer normalizer) {
            var result = new HashSet<string>();
            foreach (var book in books.Where(b => !string.IsNullOrWhiteSpace(b.Authors))) {
                var authors = book.Authors.Split(normalizer.AuthorsSeparator, StringSplitOptions.RemoveEmptyEntries);

                foreach (var author in authors) {
                    var tokens = GetTokens(author, 0)
                        .Where(t => char.IsUpper(t[0]) && !normalizer.NonSingAuthorWords.Contains(t))
                        .ToList();

                    // Отсекаю всякую дичь, которая иногда проскакивает в авторах
                    if (tokens.Count > 1 && tokens.Count < 5) {
                        foreach (var token in tokens.Where(t => t.Length > 2)) {
                            result.Add(token);
                        }
                    }
                }
            }

            return result;
        }
        
        private static HashSet<string> GetPublishers(IEnumerable<BookInfo> books) {
            return books
                .Select(book => book.Publisher.Clean().ToLowerInvariant())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        }
        
        private static IEnumerable<string> GetTokens(string bib, int minLength = 1) {
            return string.IsNullOrWhiteSpace(bib) ? 
                Enumerable.Empty<string>() : 
                Regex.Split(bib, "[\\W_]")
                    .Where(token => !string.IsNullOrWhiteSpace(token) && token.Length > minLength && !int.TryParse(token, out _));
        }
    }
}
