using System;
using System.Collections.Generic;
using System.Linq;
using Book.Comparer.Logic.Configs;
using Book.Comparer.Logic.Types;
using Book.Comparer.Logic.Utils;
using Core.Types;

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
            var result = new BookComparerResult {
                Author = CheckLevensteinDiff(book1.Key.Authors, book2.Key.Authors, _bookComparerConfig.LevensteinBorder)
            };
            
            //Если авторы не совпадают, то проверку названия не делаем, что бы ускорить проверку
            result.Name = result.Author.Success ? CheckNameDiff(book1, book2) : new ComparerResult();
            
            return result;
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
                CheckIntersectDiff(book1.Key.NameWords, book2.Key.NameWords, _bookComparerConfig.IntersectBorder);
        }
        
        /// <summary>
        /// Определение расстояния левенштейна между двумя коллекциями.
        /// </summary>
        /// <param name="firstWord"></param>
        /// <param name="secondWord"></param>
        /// <param name="border"></param>
        /// <returns>Самое близкое расстояние между парами</returns>
        private static ComparerResult CheckLevensteinDiff(ICollection<string> firstWord, ICollection<string> secondWord, double border) {
            if (firstWord.Count <= 0 || secondWord.Count <= 0) {
                return new ComparerResult();
            }

            foreach (var word1 in firstWord) {
                foreach (var word2 in secondWord) {
                    var length = Math.Max(word1.Length, word2.Length);
                    var diff = (double)Math.Abs(word1.Length - word2.Length) / length;
                    if (diff > border) {
                        continue;
                    }
                    
                    diff = Levenstein.Distance(word1, word2) / length;

                    if (diff <= border) {
                        return new ComparerResult(diff, true);
                    }
                }
            }

            return new ComparerResult();
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
                return new ComparerResult();
            }
            
            var length = Math.Max(firstWord.Length, secondWord.Length);
            var diff = (double)Math.Abs(firstWord.Length - secondWord.Length) / length;
            if (diff > border) {
                return new ComparerResult(diff, false);
            }
            
            diff = Levenstein.Distance(firstWord, secondWord) / length;
            return new ComparerResult(diff, diff <= border);
        }
        
        /// <summary>
        /// Определение процента непересекающихся слов между двумя коллекциями. Процент определяется по самой короткой коллекции
        /// </summary>
        /// <param name="firstWords"></param>
        /// <param name="secondWords"></param>
        /// <param name="border"></param>
        /// <returns></returns>
        private static ComparerResult CheckIntersectDiff(ICollection<string> firstWords, ICollection<string> secondWords, double border) {
            if (firstWords.Count < 5 || secondWords.Count < 5) {
                return new ComparerResult();
            }

            var diff = 1 - (double) (firstWords.Count <= secondWords.Count ? 
                firstWords.Count(secondWords.Contains) : 
                secondWords.Count(firstWords.Contains)) / Math.Min(firstWords.Count, secondWords.Count);

            return new ComparerResult(diff, diff <= border);
        }
    }
}
