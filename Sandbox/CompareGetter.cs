using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Book.Comparer.Logic.BookGetter;
using Book.Comparer.Logic.Configs;
using Book.Comparer.Logic.Types;
using Book.Comparer.Logic.Utils;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Types;
using MongoDB.Driver;

namespace Sandbox {
    public class CompareGetter : ICompareBookGetter {
        private readonly IRepository<BookInfo> _repository;
        private readonly NormalizerConfig _config;

        public CompareGetter(IRepository<BookInfo> repository, NormalizerConfig config) {
            _repository = repository;
            _config = config;
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
            var normalizer = new Normalizer(_config);
            var books = await GetBooks();
            
            var authors = GetAuthors(books, normalizer);
            var publishers = GetPublishers(books);
            
            var trash = GetFileLines("trash.txt");
            
            var result = new ConcurrentQueue<CompareBook>();

            var bibs = await File.ReadAllLinesAsync("bibs.txt");
            Parallel.ForEach(bibs, new ParallelOptions {MaxDegreeOfParallelism = 7},                bib => {
                    var bibBook = GetInfoFromBib(normalizer, authors, publishers, trash, bib);
                    if (bibBook.Key.Authors.Count > 0 && bibBook.Key.Name.Length > 0) {
                        result.Enqueue(bibBook);
                    }
            });
            
            return result;
        }
        
        private static string RemoveNonChar(string str) {
            var sb = new StringBuilder();
            foreach (var c in str) {
                if (char.IsLetter(c) || char.IsWhiteSpace(c) || c == '.') {
                    sb.Append(c);
                } else {
                    sb.Append(" ");
                }
            }
            
            return " " + Regex.Replace(sb.ToString().Trim(), "\\s+", " ") + " ";
        }

        private static CompareBook GetInfoFromBib(Normalizer normalizer, HashSet<string> authors, List<string> publishers, HashSet<string> trash, string bib) {
            var bibBook = new BookInfo(bib, "Custom");
            bibBook.Bib = bib;

            bib  = RemoveNonChar(bib);
            
            var bibAuthors = new HashSet<string>();
            var bibName = new HashSet<string>();
            var bibPublisher = new List<string>();
            
            foreach (var publisher in publishers) {
                if (bib.Contains(publisher, StringComparison.InvariantCultureIgnoreCase)) {
                    bibPublisher.Add(publisher.Trim());
                    bib = bib.Replace(publisher, " ", StringComparison.InvariantCultureIgnoreCase);
                }
            }
            
            foreach (var token in GetTokens(bib)) {
                if (trash.Contains(token, StringComparer.InvariantCultureIgnoreCase)) {
                    continue;
                }

                var isAuthor = authors.Contains(token);
                if (isAuthor) {
                    bibAuthors.Add(token);
                }

                if (!isAuthor) {
                    bibName.Add(token);
                }
            }

            bibBook.Authors = (bibAuthors.IsNullOrEmpty() ? GetAuthorsRgx(bib, normalizer) : bibAuthors).StrJoin(", ");
            bibBook.Name = bibName.StrJoin(" ");
            bibBook.Publisher = bibPublisher.StrJoin(", ");

            return CompareBook.Create(bibBook, normalizer);
        }

        private static HashSet<string> GetAuthorsRgx(string bib, Normalizer normalizer) {
            var authors = new HashSet<string>();
            
            foreach (var regex in _authorRegexs) {
                foreach (Match match in regex.Matches(bib)) {
                    var secondName = match.Groups["SecondName"].Value.Trim();
                    var io = match.Groups["io"].Captures.Select(t => t.Value.Trim()).ToList();
                    
                    if (secondName.Length > 2 && char.IsUpper(secondName[0]) && !normalizer.NonSingAuthorWords.Contains(secondName, StringComparer.InvariantCultureIgnoreCase) && io.All(c => char.IsUpper(c[0]))) {
                        authors.Add(secondName);
                    }
                }
            }

            return authors;
        }

        private static IEnumerable<string> GetTokens(string bib, int minLength = 1) {
            return string.IsNullOrWhiteSpace(bib) ? 
                Enumerable.Empty<string>() : 
                Regex.Split(bib, "[\\W_]")
                    .Where(token => !string.IsNullOrWhiteSpace(token) && token.Length > minLength && !int.TryParse(token, out _));
        }

        private static HashSet<string> GetFileLines(string name) {
            return File.ReadAllLines(name, Encoding.UTF8).ToHashSet();
        }
        
        private static List<string> GetPublishers(IEnumerable<BookInfo> books) {
            var result = new HashSet<string>();
            
            foreach (var book in books.Where(b => !string.IsNullOrWhiteSpace(b.Publisher))) {
                result.Add(RemoveNonChar(book.Publisher).ToLowerInvariant());
            }

            return result.OrderByDescending(t => t.Length).ToList();
        }

        private static readonly Regex[] _authorRegexs = {
                                                            new Regex(@"(\b|\s)(?<SecondName>\w+)(?<io>\s\w\.){1,2}(\b|\s)", RegexOptions.Compiled),
                                                            new Regex(@"(\b|\s)(?<io>\w\.\s){1,2}(?<SecondName>\w+)(\b|\s)", RegexOptions.Compiled)
                                                        };
        
        private static HashSet<string> GetAuthors(IEnumerable<BookInfo> books, Normalizer normalizer) {
            var result = new HashSet<string>();
            var separator = new[] {",", ";", ":"};
            foreach (var book in books.Where(b => !string.IsNullOrWhiteSpace(b.Authors))) {
                var authors = book.Authors
                    .Split(separator, StringSplitOptions.RemoveEmptyEntries);
                
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
