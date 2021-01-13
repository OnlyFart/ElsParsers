using System.Text.RegularExpressions;
using Core.Types;

namespace IprBookShopParser.Types {
    /// <summary>
    /// 
    /// </summary>
    public class Book : BookBase {
        public string Type;
        public string Grif;
        public string Response;

        public static string Normalize(string str) {
            return string.IsNullOrEmpty(str) ? string.Empty : Regex.Replace(str, @"\s+", " ").Trim();
        }
    }
}
