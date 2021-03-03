using System;
using System.Collections.Generic;
using System.Linq;
using Book.Comparer.Logic.Configs;
using Book.Comparer.Logic.Types;
using Core.Types;
using Quickenshtein;

namespace Book.Comparer.Logic.Comparers {
    public class BookComparer : IBookComparer {
        private readonly IBookComparerConfig _bookComparerConfig;

        public BookComparer(IBookComparerConfig bookComparerConfig) {
            _bookComparerConfig = bookComparerConfig;
        }

        /// <summary>
        /// Сравнение двух книг
        /// </summary>
        /// <param name="book1"></param>
        /// <param name="book2"></param>
        /// <returns></returns>
        public BookComparerResult Compare(CompareBook book1, CompareBook book2) {
            var author = CheckLevensteinDiff(book1.Key.Authors, book2.Key.Authors, _bookComparerConfig.LevensteinBorder);

            if (!author.Success) {
                return new BookComparerResult {
                    Success = false,
                    Coeff = 0
                };
            }

            var name = CheckNameDiff(book1, book2);
            return new BookComparerResult {
                Success = name.Success,
                Coeff = (author.Diff + name.Diff) / 2
            };
        }

        /// <summary>
        /// Определение разницы между кингами по названию
        /// </summary>
        /// <param name="book1"></param>
        /// <param name="book2"></param>
        /// <returns></returns>
        private ComparerResult CheckNameDiff(CompareBook book1, CompareBook book2) {
            var levensteinDiff = CheckLevensteinDiff(book1.Key.Name, book2.Key.Name, _bookComparerConfig.LevensteinBorder);
            
            // Если по левештейну строки ладеки друг от друга, то пытаемся поределить похожесть на основе пересечения слов
            return levensteinDiff.Success ? 
                levensteinDiff : 
                CheckIntersectDiff(book1.Key.NameTokens, book2.Key.NameTokens, _bookComparerConfig.IntersectBorder);
        }
        
        /// <summary>
        /// Определение расстояния левенштейна между двумя коллекциями.
        /// </summary>
        /// <param name="firstWord"></param>
        /// <param name="secondWord"></param>
        /// <param name="border"></param>
        /// <returns>Самое близкое расстояние между парами</returns>
        private static ComparerResult CheckLevensteinDiff(HashSet<string> firstWord, HashSet<string> secondWord, double border) {
            if (firstWord.Count <= 0 || secondWord.Count <= 0) {
                return default;
            }

            foreach (var word1 in firstWord) {
                foreach (var word2 in secondWord) {
                    var length = Math.Max(word1.Length, word2.Length);
                    var diff = (double)Math.Abs(word1.Length - word2.Length) / length;
                    if (diff > border) {
                        continue;
                    }
                    
                    diff = (double)Levenshtein.GetDistance(word1, word2) / length;

                    if (diff <= border) {
                        return new ComparerResult(diff, true);
                    }
                }
            }

            return default;
        }
        
        /// <summary>
        /// Определение расстояния левенштейна между двумя строками
        /// </summary>
        /// <param name="firstWord"></param>
        /// <param name="secondWord"></param>
        /// <param name="border"></param>
        /// <returns></returns>
        private static ComparerResult CheckLevensteinDiff(string firstWord, string secondWord, double border) {
            if (string.IsNullOrEmpty(firstWord) || string.IsNullOrEmpty(secondWord)) {
                return default;
            }
            
            var length = Math.Max(firstWord.Length, secondWord.Length);
            var diff = (double)Math.Abs(firstWord.Length - secondWord.Length) / length;
            if (diff > border) {
                return new ComparerResult(diff, false);
            }
            
            diff = (double)Levenshtein.GetDistance(firstWord, secondWord) / length;
            return new ComparerResult(diff, diff <= border);
        }
        
        /// <summary>
        /// Определение процента непересекающихся слов между двумя коллекциями. Процент определяется по самой короткой коллекции
        /// </summary>
        /// <param name="firstWords"></param>
        /// <param name="secondWords"></param>
        /// <param name="border"></param>
        /// <returns></returns>
        private static ComparerResult CheckIntersectDiff(HashSet<string> firstWords, HashSet<string> secondWords, double border) {
            if (firstWords.Count < 5 || secondWords.Count < 5) {
                return default;
            }

            var diff = 1 - (double) (firstWords.Count <= secondWords.Count ? 
                firstWords.Count(secondWords.Contains) : 
                secondWords.Count(firstWords.Contains)) / Math.Min(firstWords.Count, secondWords.Count);

            return new ComparerResult(diff, diff <= border);
        }
    }
}
