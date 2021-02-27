using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Book.Comparer.Logic.BookGetter;
using Book.Comparer.Logic.Types;
using Book.Comparer.Logic.Utils;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Types;
using MongoDB.Driver;

namespace Sandbox {
    public class CompareGetter : ICompareBookGetter {
        private readonly IRepository<BookInfo> _repository;
        private readonly Normalizer _normalizer;

        public CompareGetter(IRepository<BookInfo> repository, Normalizer normalizer) {
            _repository = repository;
            _normalizer = normalizer;
        }
        
        private async Task<IReadOnlyCollection<BookInfo>> GetBooks() {
            ProjectionDefinition<BookInfo, BookInfo> projection = Builders<BookInfo>
                .Projection
                .Exclude("_id")
                .Include(b => b.Authors)
                .Include(b => b.Publisher);

            var filterDefinition = Builders<BookInfo>.Filter
                .Where(t => t.Name != null && t.Name.Length > 0 && t.Authors != null && t.Authors.Length > 0);
            
            return await _repository.Read(filterDefinition, projection);
        }
        
        public async Task<IReadOnlyCollection<CompareBook>> Get() {
            var books = GetBooks();
            var trash = GetFileLines("trash.txt").ContinueWith(t => t.Result.ToHashSet(StringComparer.InvariantCultureIgnoreCase));
            var bibs = GetFileLines("bibs.txt");

            var authors = books.ContinueWith(t => GetAuthors(t.Result, _normalizer));
            var publishers = books.ContinueWith(t => GetPublishers(t.Result));
            
            var bibParserConfig = new BibParserConfig(await authors, await publishers, await trash);
            var bibParser = new BibParser(_normalizer, bibParserConfig);
            
            var result = new ConcurrentQueue<CompareBook>();
            Parallel.ForEach(await bibs, new ParallelOptions { MaxDegreeOfParallelism = 7 }, bib => {
                var bookInfo = bibParser.Parse(bib);
                if (string.IsNullOrWhiteSpace(bookInfo.Authors) || string.IsNullOrWhiteSpace(bookInfo.Name)) {
                    return;
                }
                
                var book = CompareBook.Create(bookInfo, _normalizer);
                if (book.Key.Authors.Count > 0 && book.Key.Name.Length > 0) {
                    result.Enqueue(book);
                }
            });
            
            return result;
        }

        private static IEnumerable<string> GetTokens(string bib, int minLength = 1) {
            return string.IsNullOrWhiteSpace(bib) ? 
                Enumerable.Empty<string>() : 
                Regex.Split(bib, "[\\W_]")
                    .Where(token => !string.IsNullOrWhiteSpace(token) && token.Length > minLength && !int.TryParse(token, out _));
        }

        private static async Task<string[]> GetFileLines(string name) {
            return await File.ReadAllLinesAsync(name, Encoding.UTF8);
        }
        
        private static HashSet<string> GetPublishers(IEnumerable<BookInfo> books) {
            return books.Where(book => !string.IsNullOrWhiteSpace(book.Publisher))
                .Select(book => book.Publisher.Clean().ToLowerInvariant())
                .ToHashSet();
        }
        
        private static HashSet<string> GetAuthors(IEnumerable<BookInfo> books, Normalizer normalizer) {
            var result = new HashSet<string>();
            foreach (var book in books.Where(b => !string.IsNullOrWhiteSpace(b.Authors))) {
                var authors = book.Authors.Split(normalizer.AuthorsSeparator, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var author in authors) {
                    var tokens = GetTokens(author, 2)
                        .Where(t => char.IsUpper(t[0]) && !normalizer.NonSingAuthorWords.Contains(t, StringComparer.InvariantCultureIgnoreCase))
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
    }
}
