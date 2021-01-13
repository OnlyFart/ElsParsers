using System.Text.RegularExpressions;
using Core.Types;

namespace IprBookShopParser.Types {
    /// <summary>
    /// 
    /// </summary>
    public class Book : BookBase {
        /// <summary>
        /// Авторы книги
        /// </summary>
        public string Authors;

        public string Name;

        public string Year;

        public string ISBN;
        
        public string ISSN;

        public string Type;

        public string Bib;

        public string Grif;

        public string Response;
        
        public string Publisher;

        public static string Normalize(string str) {
            return string.IsNullOrEmpty(str) ? string.Empty : Regex.Replace(str, @"\s+", " ").Trim();
        }
    }
}
