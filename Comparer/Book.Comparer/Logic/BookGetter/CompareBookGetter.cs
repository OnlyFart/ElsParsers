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

        private void ParseBibBooks(IReadOnlyCollection<BookInfo> books) {
            var bibToParse = books.Where(b => !b.Compared && b.ElsName == Const.BIB_ELS).ToList();
            var noAuthors = books.Where(b => !b.Compared && b.ElsName != Const.BIB_ELS && string.IsNullOrWhiteSpace(b.Authors) && !string.IsNullOrWhiteSpace(b.Bib)).ToList();

            if (bibToParse.Count <= 0 && noAuthors.Count <= 0) {
                return;
            }

            _logger.Info($"Обнаружено {bibToParse.Count} БЗ, которые необходимо распарсить.");
            var parser = GetBibParser(books);
            _logger.Info("Создание парсера закончено. Начинаю парсинг.");
                
            foreach (var bibBook in bibToParse) {
                var (authors, name, publisher) = parser.Parse(bibBook.Bib);

                bibBook.Authors = authors;
                bibBook.Name = name;
                bibBook.Publisher = publisher;
            }

            
            if (noAuthors.Count > 0) {
                _logger.Info($"Обнаружено {noAuthors.Count} книг без авторов. Пытаемся получить авторов из БЗ");
                
                foreach (var noAuthor in noAuthors) {
                    var (authors, _, _) = parser.Parse(noAuthor.Bib);

                    noAuthor.Authors = authors;
                }
            }
                
            _logger.Info("Парсинг закончен.");
        }
        
        public async Task<IReadOnlyCollection<CompareBook>> Get() {
            var books = await GetBooks();
            ParseBibBooks(books);

            _logger.Info("Начинаю преобразование книг в сравниваемые");

            return books
                .AsParallel()
                .Select(book => CompareBook.Create(book, _normalizer))
                .ToList();
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
