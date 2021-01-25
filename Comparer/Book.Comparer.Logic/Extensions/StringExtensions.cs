using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Book.Comparer.Logic.Extensions {
    public static class StringExtensions {
        private static readonly Regex _nonSignWords = new Regex("томах|том|часть|частях|части|учебно\\-методический комплекс|практикум|роман|методическ|практическ|сборник|художественн|литератур|научн|популярн|издание|публицистик|документальн|учебник|учебн|пособ|монограф", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex _vowels = new Regex("[_ёуейиыаоэяиюьъeuoai]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex _nonSignCharacters = new Regex("\\b\\w\\w?\\b|\\d", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _nonCharacter = new Regex("\\W", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _nonDigits = new Regex("\\D", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static HashSet<string> BadWords = new HashSet<string> {
            "под", "науч", "ред", "отв", "общ", "пер"
        };  
        
        /// <summary>
        /// Оставить в строке только цифры
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string OnlyDigits(this string str) {
            return RemoveRegex(str, _nonDigits);
        }

        /// <summary>
        /// Удалить из строки гласные
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveVowels(this string str) {
            return RemoveRegex(str, _vowels);
        }
        
        /// <summary>
        /// Удалить из строки не значимые слова
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveNonSignWords(this string str) {
            return RemoveRegex(str, _nonSignWords);
        }
        
        /// <summary>
        /// Удалить из строки не значимые символы (предлоги, одиночные/двойные слова, цифры)
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveNonSignCharacters(this string str) {
            return RemoveRegex(str, _nonSignCharacters);
        }
        
        /// <summary>
        /// Удалить из строки не словарные символы (кавычки, дефисы, тире, пробелы...)
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveNonCharacters(this string str) {
            return RemoveRegex(str, _nonCharacter);
        }

        /// <summary>
        /// Разбить строку по несловарным символам
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static IEnumerable<string> SplitWords(this string str) {
            return string.IsNullOrWhiteSpace(str) ? new string[] { } : _nonCharacter.Split(str);
        }

        private static string RemoveRegex(string str, Regex regex) {
            return string.IsNullOrWhiteSpace(str) ? string.Empty : regex.Replace(str, string.Empty);
        }
        
        /// <summary>
        /// Первое слово целиком, от остальных по первой букве
        /// </summary>
        /// <param name="split"></param>
        /// <returns></returns>
        public static string FirstFullOtherFirst(this string[] split) {
            if (split.Length == 0) {
                return string.Empty;
            }
            
            var result = string.Empty;
            for (var i = 0; i < split.Length; i++) {
                if (i == 0) {
                    result += split[i];
                    continue;
                }

                result += split[i].FirstOrDefault();
            }

            return result;
        }
    }
}