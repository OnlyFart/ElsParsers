using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Book.Comparer.Logic.Utils;
using Core.Extensions;
using Core.Types;

namespace Sandbox {
    public class BibParser {
        private readonly Normalizer _normalizer;
        private readonly BibParserConfig _config;

        private static readonly Regex[] AuthorRegexs = {
            new Regex(@"(\b|\s)(?<SecondName>\w+)(?<io>\s\w\.){1,2}(\b|\s)", RegexOptions.Compiled),
            new Regex(@"(\b|\s)(?<io>\w\.\s){1,2}(?<SecondName>\w+)(\b|\s)", RegexOptions.Compiled)
        };

        public BibParser(Normalizer normalizer, BibParserConfig config) {
            _normalizer = normalizer;
            _config = config;
        }

        public BookInfo Parse(string bib) {
            var book = new BookInfo(bib, "Custom") {
                Bib = bib
            };

            var cleanBib = bib.Clean().Cover(" ");
            
            var bibAuthors = new HashSet<string>();
            var bibName = new HashSet<string>();
            var bibPublisher = new List<string>();
            
            foreach (var publisher in _config.Publishers.Where(publisher => bib.Contains(publisher, StringComparison.InvariantCultureIgnoreCase))) {
                bibPublisher.Add(publisher.Trim());
                cleanBib = cleanBib.Replace(publisher, " ", StringComparison.InvariantCultureIgnoreCase);
            }
            
            foreach (var token in GetTokens(cleanBib)) {
                if (_config.Trash.Contains(token, StringComparer.InvariantCultureIgnoreCase)) {
                    continue;
                }

                if (_config.Authors.Contains(token)) {
                    bibAuthors.Add(token);
                } else {
                    bibName.Add(token);
                }
            }

            book.Authors = (bibAuthors.IsNullOrEmpty() ? GetAuthorsRgx(cleanBib, _normalizer) : bibAuthors).StrJoin(", ");
            book.Name = bibName.StrJoin(" ");
            book.Publisher = bibPublisher.StrJoin(", ");

            return book;
        }

        private static HashSet<string> GetAuthorsRgx(string bib, Normalizer normalizer) {
            var authors = new HashSet<string>();
            
            foreach (var regex in AuthorRegexs) {
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
        
        private static IEnumerable<string> GetTokens(string str, int minLength = 1) {
            return string.IsNullOrWhiteSpace(str) ? 
                Enumerable.Empty<string>() : 
                Regex.Split(str, "[\\W_]")
                    .Where(token => !string.IsNullOrWhiteSpace(token) && token.Length > minLength && !int.TryParse(token, out _));
        }
    }
}
