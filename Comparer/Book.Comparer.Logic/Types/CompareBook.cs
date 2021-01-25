using System;
using System.Collections.Generic;
using System.Linq;
using Book.Comparer.Logic.Extensions;
using Core.Types;

namespace Book.Comparer.Logic.Types {
    public class CompareBook {
        public readonly BookInfo BookInfo;
        public CompareBookKey Key;

        public CompareBook(BookInfo bookInfo) {
            BookInfo = bookInfo;
            Init();
        }

        /// <summary>
        /// Построение ключа по которому будет происходить построение
        /// </summary>
        private void Init() {
            // ГОСТ 5812-2014
            if (BookInfo.Similar == null) {
                BookInfo.Similar = new HashSet<BookInfo>();
            }
            
            Key = new CompareBookKey {
                ISBN = (string.IsNullOrEmpty(BookInfo.ISBN) ? BookInfo.ISSN ?? string.Empty : BookInfo.ISBN).OnlyDigits(),
                Year = (BookInfo.Year ?? string.Empty).OnlyDigits(),
                Publisher = (BookInfo.Publisher ?? string.Empty).ToLowerInvariant().RemoveNonSignWords().RemoveNonSignCharacters().RemoveNonCharacters().RemoveVowels(),
                Name = (BookInfo.Name ?? string.Empty).ToLowerInvariant().RemoveNonSignWords().RemoveNonSignCharacters().RemoveNonCharacters().RemoveVowels(),
                NameWords = (BookInfo.Name ?? string.Empty).ToLowerInvariant().RemoveNonSignWords().RemoveNonSignCharacters().RemoveVowels().SplitWords().Where(w => !string.IsNullOrWhiteSpace(w)).ToHashSet(),
                Authors = new HashSet<string>() 
            };

            if (string.IsNullOrWhiteSpace(BookInfo.Authors)) {
                return;
            }

            foreach (var author in BookInfo.Authors.ToLowerInvariant().Split(new []{ ",", ";", ":" }, StringSplitOptions.RemoveEmptyEntries)) {
                var split = author.SplitWords().Where(t => !string.IsNullOrWhiteSpace(t) && !StringExtensions.BadWords.Contains(t)).ToArray();

                // Если паттерн ФИО стандартный или перестановок будет очень много, то перестановки не генерим
                if (CheckFio(split, 2) || CheckFio(split, 3) || split.Length >= 5) {
                    Key.Authors.Add(split.FirstFullOtherFirst());
                } else {
                    foreach (var permutation in split.AllPermutations()) {
                        Key.Authors.Add(permutation.ToArray().FirstFullOtherFirst());
                    }
                }
            }
        }
        
        /// <summary>
        /// Проверка FIO на паттерн "Фамилия И. О."
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
