using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Book.Comparer.Logic.Configs;

namespace Book.Comparer.Logic.Utils {
    public class Normalizer {
        private readonly Regex _nonSignWords;
        private readonly Regex _vowels;
        private static readonly Regex _nonSignCharacters = new Regex("\\b\\w\\w?\\b|\\d", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _nonCharacter = new Regex("\\W", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _nonDigits = new Regex("\\D", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        public readonly string[] AuthorsSeparator = { ",", ";", ":" };
        public readonly HashSet<string> NonSingAuthorWords;

        public Normalizer(NormalizerConfig config) {
            _nonSignWords = new Regex(config.Regexes.NonSignWords, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            _vowels = new Regex(config.Regexes.Vowels, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            NonSingAuthorWords = new HashSet<string>(config.Lists.NonSingAuthorWords);
        }

        public string FullClean(string str) {
            return RemoveVowels(RemoveNonCharacters(ShortClean(str)));
        }
        
        public string ShortClean(string str) {
            return RemoveNonSignCharacters(RemoveNonSignWords(str));
        }
        
        /// <summary>
        /// Оставить в строке только цифры
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public string OnlyDigits(string str) {
            return RemoveRegex(str, _nonDigits);
        }

        /// <summary>
        /// Удалить из строки гласные
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public string RemoveVowels(string str) {
            return RemoveRegex(str, _vowels);
        }
        
        /// <summary>
        /// Удалить из строки не значимые слова
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public string RemoveNonSignWords(string str) {
            return RemoveRegex(str, _nonSignWords);
        }
        
        /// <summary>
        /// Удалить из строки не значимые символы (предлоги, одиночные/двойные слова, цифры)
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public string RemoveNonSignCharacters(string str) {
            return RemoveRegex(str, _nonSignCharacters);
        }
        
        /// <summary>
        /// Удалить из строки не словарные символы (кавычки, дефисы, тире, пробелы...)
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public string RemoveNonCharacters(string str) {
            return RemoveRegex(str, _nonCharacter);
        }

        /// <summary>
        /// Разбить строку по несловарным символам
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public IEnumerable<string> SplitWords(string str) {
            return string.IsNullOrWhiteSpace(str) ? new string[] { } : _nonCharacter.Split(str);
        }

        /// <summary>
        /// Удалить из строки все, что попадает по Regex
        /// </summary>
        /// <param name="str"></param>
        /// <param name="regex"></param>
        /// <returns></returns>
        private string RemoveRegex(string str, Regex regex) {
            return string.IsNullOrWhiteSpace(str) ? string.Empty : regex.Replace(str, string.Empty);
        }
        
        /// <summary>
        /// Первое слово целиком, от остальных по первой букве
        /// </summary>
        /// <param name="split"></param>
        /// <returns></returns>
        public string FirstFullOtherFirst(string[] split) {
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