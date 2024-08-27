using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Book.Comparer.Logic.Configs;
using Book.Comparer.Logic.Types;
using Book.Comparer.Logic.Utils;
using Core.Extensions;

namespace Book.Comparer.Logic.Logic {
    public class BibParser {
        private readonly Normalizer _normalizer;
        private readonly BibParserConfig _config;

        private static readonly Regex[] AuthorRegexs = [
            new(@"(\b|\s)(?<SecondName>\w+)(?<io>\s\w\.){1,2}(\b|\s)", RegexOptions.Compiled),
            new(@"(\b|\s)(?<io>\w\.\s){1,2}(?<SecondName>\w+)(\b|\s)", RegexOptions.Compiled)
        ];

        public BibParser(Normalizer normalizer, BibParserConfig config) {
            _normalizer = normalizer;
            _config = config;
        }

        private IEnumerable<string> GetPublishers(string[] tokens) {
            for (var i = tokens.Length; i > 0; i--) {
                for (var j = 0; j <= i; j++) {
                    var subStr = tokens[j..i].StrJoin(" ");
                    if (_config.Publishers.Contains(subStr)) {
                        yield return subStr;
                    }
                }
            }
        }

        public BibParseResult Parse(string bib) {
            var cleanBib = HttpUtility.HtmlDecode(bib ?? string.Empty).Clean();
            
            var bibAuthors = new HashSet<string>();
            var bibName = new List<string>();
            var bibPublisher = new HashSet<string>();
            
            foreach (var candidate in GetPublishers(GetTokens(cleanBib.ToLowerInvariant()).ToArray()).OrderByDescending(t => t.Length)) {
                if (cleanBib.Contains(candidate, StringComparison.InvariantCultureIgnoreCase)) {
                    cleanBib = cleanBib.Replace(candidate, " ", StringComparison.InvariantCultureIgnoreCase);
                    bibPublisher.Add(candidate);
                }
            }

            foreach (var token in GetTokens(cleanBib).Where(token => !_normalizer.NonSignBibWords.Contains(token))) {
                if (_config.Authors.Contains(token)) {
                    bibAuthors.Add(token);
                } else {
                    bibName.Add(token);
                }
            }

            if (bibAuthors.IsNullOrEmpty()) {
                bibAuthors = GetAuthorsRgx(cleanBib, _normalizer);
                bibName = bibName.Except(bibAuthors, StringComparer.InvariantCultureIgnoreCase).ToList();
            }
            
            var authors = bibAuthors.StrJoin(", ");
            var name = bibName.StrJoin(" ");
            var publisher = bibPublisher.StrJoin(", ");

            return new BibParseResult(authors, name, publisher);
        }

        private static HashSet<string> GetAuthorsRgx(string bib, Normalizer normalizer) {
            var authors = new HashSet<string>();
            
            foreach (var regex in AuthorRegexs) {
                foreach (Match match in regex.Matches(bib)) {
                    var secondName = match.Groups["SecondName"].Value.Trim();
                    var io = match.Groups["io"].Captures.Select(t => t.Value.Trim()).ToList();
                    
                    if (secondName.Length > 2 && char.IsUpper(secondName[0]) && !normalizer.NonSingAuthorWords.Contains(secondName) && io.All(c => char.IsUpper(c[0]))) {
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
