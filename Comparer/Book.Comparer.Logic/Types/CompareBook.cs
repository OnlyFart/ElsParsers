using System;
using System.Collections.Generic;
using System.Linq;
using Book.Comparer.Logic.Utils;
using Core.Extensions;
using Core.Types;

namespace Book.Comparer.Logic.Types {
    public class CompareBook {
        public readonly BookInfo BookInfo;
        public CompareBookKey Key;

        public bool IsComparedBook =>
            !string.IsNullOrWhiteSpace(BookInfo.Authors) && !string.IsNullOrWhiteSpace(BookInfo.Name);

        /// <summary>
        /// Создание "сравниваемой" книги
        /// </summary>
        /// <param name="bookInfo"></param>
        /// <param name="normalizer"></param>
        /// <returns></returns>
        public static CompareBook Create(BookInfo bookInfo, Normalizer normalizer) {
            var result = new CompareBook(bookInfo);

            if (result.BookInfo.SimilarBooks == null || !result.BookInfo.Compared) {
                result.BookInfo.SimilarBooks = new Dictionary<string, HashSet<SimilarInfo>>();
            }

            result.BookInfo.Bib = null;

            result.Key = new CompareBookKey()
                .WithName(bookInfo.Name, normalizer)
                .WithAuthors(bookInfo.Authors, normalizer);

            return result;
        }
        
        private CompareBook(BookInfo bookInfo) {
            BookInfo = bookInfo;
        }
    }

    public class CompareBookKey {
        public HashSet<string> Authors { get; private set; }
        public string Name { get; private set; }
        public HashSet<string> NameTokens { get; private set; }

        public CompareBookKey WithName(string name, Normalizer normalizer) {
            Name = normalizer.FullClean((name ?? string.Empty).ToLowerInvariant());
            NameTokens = normalizer
                .SplitWords(normalizer.RemoveVowels(normalizer.ShortClean((name ?? string.Empty).ToLowerInvariant())))
                .Where(w => !string.IsNullOrWhiteSpace(w)).ToHashSet();
            return this;
        }
        
        public CompareBookKey WithAuthors(string authors, Normalizer normalizer) {
            Authors = new HashSet<string>();
            
            if (string.IsNullOrWhiteSpace(authors)) {
                return this;
            }

            foreach (var author in authors.ToLowerInvariant().Split(normalizer.AuthorsSeparator, StringSplitOptions.RemoveEmptyEntries)) {
                var split = normalizer.SplitWords(author).Where(t => !string.IsNullOrWhiteSpace(t) && !normalizer.NonSingAuthorWords.Contains(t)).ToArray();

                // Если паттерн ФИО стандартный или перестановок будет очень много, то перестановки не генерим
                if (CheckFio(split, 2) || CheckFio(split, 3) || split.Length >= 5) {
                    Authors.Add(Normalizer.FirstFullOtherFirst(split));
                } else {
                    foreach (var permutation in split.AllPermutations()) {
                        Authors.Add(Normalizer.FirstFullOtherFirst(permutation.ToArray()));
                    }
                }
            }

            return this;
        }
        
        /// <summary>
        /// Проверка ФИО на паттерн "Фамилия И. О."
        /// </summary>
        /// <param name="fio"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private static bool CheckFio(IReadOnlyList<string> fio, int length) {
            if (fio.Count != length) {
                return false;
            }

            for (var i = 0; i < length; i++) {
                switch (i) {
                    case 0 when fio[i].Length == 1:
                    case > 0 when fio[i].Length != 1:
                        return false;
                }
            }

            return true;
        }
    }
}
