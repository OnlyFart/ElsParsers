using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Book.Comparer.Logic.Comparers;
using Book.Comparer.Logic.Configs;
using Book.Comparer.Logic.Types;
using Book.Comparer.Logic.Utils;
using Book.Comparer.Types;
using Core.Configs;
using Core.Extensions;
using Core.Providers.Implementations;
using Core.Types;
using MongoDB.Driver;

namespace Sandbox {
    class Program {
        class MongoConfig : IMongoConfig {
            public string ConnectionString => "mongodb://localhost:27017/";
            public string DatabaseName => "ELS";
            public string CollectionName => "Books";
        }
        
        class ComparerConfig : IBookComparerConfig {
            public double LevensteinBorder { get; set; }
            public double IntersectBorder { get; set; }

            public ComparerConfig(double lb, double ib) {
                LevensteinBorder = lb;
                IntersectBorder = ib;
            }
        }

        private static NormalizerConfig _normalizerConfig = new NormalizerConfig {
                                                                Lists = new Lists {
                                                                            NonSingAuthorWords = new string[] {"под", "науч", "ред", "отв", "общ", "пер"}.ToList()
                                                                        },
                                                                Regexes = new Regexes {
                                                                              NonSignWords = "томах|том|часть|частях|части|учебно\\-методический комплекс|практикум|роман|методическ|практическ|сборник|художественн|литератур|научн|популярн|издание|публицистик|документальн|учебник|учебн|пособ|монограф",
                                                                              Vowels = "[_ёуейиыаоэяиюьъeuoai]"
                                                                          }
                                                            };

        private static BookComparer _bookComparer = new BookComparer(new ComparerConfig(0.3, 0.4));
        
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
        private static SaveResult FindSimilar(CompareBook thisBook, IReadOnlyDictionary<string, List<CompareBook>> wordToBooks) {
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
            }
            
            return result;
        }
        
        private static async Task<List<CompareBook>> GetBooks(MongoRepository<BookInfo> repo, Normalizer normalizer) {
            ProjectionDefinition<BookInfo, BookInfo> projection = Builders<BookInfo>
                .Projection
                .Exclude("_id")
                .Exclude(b => b.Bib);
            
            var filterDefinition = Builders<BookInfo>.Filter
                .Where(t => t.Name != null && t.Name.Length > 0 && t.Authors != null && t.Authors.Length > 0);

            var result = new List<CompareBook>();
            var books = await repo.Read(filterDefinition, projection);

            Parallel.ForEach(books, new ParallelOptions {MaxDegreeOfParallelism = 7}, book => {
                var compareBook = CompareBook.Create(book, normalizer);
                lock (result) {
                    result.Add(compareBook);
                }
            });

            return result;
        }
        
        private static Dictionary<string, List<CompareBook>> CreateWordToBooksMap(IEnumerable<CompareBook> books) {
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
            
            return result;
        }

        private static async Task Main(string[] args) {
            if (File.Exists("similar.txt")) {
                File.Delete("similar.txt");
            }
            
            var mongoRepository = new MongoRepository<BookInfo>(new MongoConfig());

            var normalizer = new Normalizer(_normalizerConfig);
            var books = await GetBooks(mongoRepository, normalizer);
            var map = CreateWordToBooksMap(books);
            
            var authors = GetAuthors(books.Select(t => t.BookInfo));
            var names = GetNames(books.Select(t => t.BookInfo));
            
            var publishers = GetPublishers(books.Select(t => t.BookInfo));
            var trash = GetFileLines("trash.txt");

            foreach (var bib in (await File.ReadAllLinesAsync("bibs.txt")).Select(RemoveNonChar)) {
                var bibBook = GetInfoFromBib(normalizer, authors, names, publishers, trash, bib.ToLowerInvariant());
                var res = FindSimilar(bibBook, map);

                var lines = new List<string>();
                lines.Add(bib);
                foreach (BookInfo simBook in res.SimilarBooks) {
                    lines.Add(new string[]{Norlalize(simBook.Authors), Norlalize(simBook.Name), simBook.ElsName, Norlalize(simBook.Publisher), simBook.ExternalId}.StrJoin("\t"));
                }
                lines.Add(string.Empty);
                
                await File.AppendAllLinesAsync("similar.txt", lines, Encoding.UTF8);
            }
            
            Console.WriteLine("Hello World!");
        }

        private static string RemoveNonChar(string str) {
            return " " + Regex.Replace(str, "[\\W_]", " ") + " ";
        }
        
        public static string Norlalize(string str) {
            return string.IsNullOrWhiteSpace(str) ? string.Empty : Regex.Replace(str, "\\t", " ");
        }

        private static CompareBook GetInfoFromBib(Normalizer normalizer, HashSet<string> authors, HashSet<string> names, HashSet<string> publishers,
            HashSet<string> trash, string bib) {
            var bibBook = new BookInfo(bib, "Custom");

            var bibAuthors = new List<string>();
            var bibName = new List<string>();
            var bibPublisher = new List<string>();
            
            foreach (var publisher in publishers) {
                if (bib.Contains(publisher)) {
                    bibPublisher.Add(publisher);
                    bib = bib.Replace(publisher, string.Empty);
                }
            }
            
            foreach (var token in GetTokens(bib)) {
                if (trash.Contains(token)) {
                    continue;
                }

                var isAuthor = authors.Contains(token);
                if (isAuthor) {
                    bibAuthors.Add(token);
                }

                if (!isAuthor || names.Contains(token)) {
                    bibName.Add(token);
                }
            }

            bibBook.Authors = bibAuthors.StrJoin(", ");
            bibBook.Name = bibName.StrJoin(" ");
            bibBook.Publisher = bibPublisher.StrJoin(", ");

            return CompareBook.Create(bibBook, normalizer);
        }

        private static IEnumerable<string> GetTokens(string bib) {
            return string.IsNullOrWhiteSpace(bib) ? 
                Enumerable.Empty<string>() : 
                Regex.Split(bib, "[\\W_]")
                    .Where(token => !string.IsNullOrWhiteSpace(token) && token.Length > 1 && !int.TryParse(token, out _))
                    .Select(token => token.ToLowerInvariant());
        }

        private static HashSet<string> GetFileLines(string name) {
            return File.ReadAllLines(name, Encoding.UTF8).ToHashSet();
        }
        
        private static HashSet<string> GetPublishers(IEnumerable<BookInfo> books) {
            var result = new HashSet<string>();
            
            foreach (var book in books.Where(b => !string.IsNullOrWhiteSpace(b.Publisher))) {
                result.Add(" " + book.Publisher.ToLowerInvariant() + " ");
            }

            return result;
        }
        
        private static HashSet<string> GetAuthors(IEnumerable<BookInfo> books) {
            var result = new HashSet<string>();
            
            foreach (var book in books) {
                foreach (var token in GetTokens(book.Authors)) {
                    result.Add(token);
                }
            }

            return result;
        }

        private static HashSet<string> GetNames(IEnumerable<BookInfo> books) {
            var result = new HashSet<string>();
            
            foreach (var book in books) {
                foreach (var token in GetTokens(book.Name)) {
                    result.Add(token);
                }
            }

            return result;
        }

        private static void Increment(Dictionary<string, int> dict, string token) {
            if (!dict.ContainsKey(token)) {
                dict[token] = 0;
            }

            dict[token]++;
        }
    }
}
