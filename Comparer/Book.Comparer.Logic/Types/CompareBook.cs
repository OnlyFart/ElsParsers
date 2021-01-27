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

        public CompareBook(BookInfo bookInfo) {
            BookInfo = bookInfo;
        }

        /// <summary>
        /// Построение ключа по которому будет происходить построение
        /// </summary>
        public void Init(Normalizer normalizer) {
            // ГОСТ 5812-2014
            if (BookInfo.Similar == null) {
                BookInfo.Similar = new HashSet<BookInfo>();
            }
            
            Key = new CompareBookKey {
                ISBN = normalizer.OnlyDigits(string.IsNullOrEmpty(BookInfo.ISBN) ? BookInfo.ISSN ?? string.Empty : BookInfo.ISBN),
                Year = normalizer.OnlyDigits(BookInfo.Year ?? string.Empty),
                Publisher = normalizer.FullClean((BookInfo.Publisher ?? string.Empty).ToLowerInvariant()),
                Name = normalizer.FullClean((BookInfo.Name ?? string.Empty).ToLowerInvariant()),
                NameWords = normalizer.SplitWords(normalizer.RemoveVowels(normalizer.ShortClean((BookInfo.Name ?? string.Empty).ToLowerInvariant()))).Where(w => !string.IsNullOrWhiteSpace(w)).ToHashSet(),
                Authors = new HashSet<string>() 
            };

            if (string.IsNullOrWhiteSpace(BookInfo.Authors)) {
                return;
            }

            foreach (var author in BookInfo.Authors.ToLowerInvariant().Split(new []{ ",", ";", ":" }, StringSplitOptions.RemoveEmptyEntries)) {
                var split = normalizer.SplitWords(author).Where(t => !string.IsNullOrWhiteSpace(t) && !normalizer.NonSingAuthorWords.Contains(t)).ToArray();

                // Если паттерн ФИО стандартный или перестановок будет очень много, то перестановки не генерим
                if (CheckFio(split, 2) || CheckFio(split, 3) || split.Length >= 5) {
                    Key.Authors.Add(normalizer.FirstFullOtherFirst(split));
                } else {
                    foreach (var permutation in split.AllPermutations()) {
                        Key.Authors.Add(normalizer.FirstFullOtherFirst(permutation.ToArray()));
                    }
                }
            }
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
                if (i == 0 && fio[i].Length == 1) {
                    return false;
                }

                if (i > 0 && fio[i].Length != 1) {
                    return false;
                }
            }

            return true;
        }
    }

    public class CompareBookKey {
        public string Year;
        public string ISBN;
        public HashSet<string> Authors;
        public string Name;
        public HashSet<string> NameWords;
        public string Publisher;
    }
}
