using System.Text;
using System.Text.RegularExpressions;

namespace Core.Extensions {
    public static class StringExtensions {
        public static string Cover(this string self, string cover) {
            return $"{cover}{self}{cover}";
        }
        
        public static string Clean(this string str) {
            var sb = new StringBuilder();
            foreach (var c in str) {
                if (char.IsLetter(c) || char.IsWhiteSpace(c) || c == '.') {
                    sb.Append(c);
                } else {
                    sb.Append(" ");
                }
            }
            
            return Regex.Replace(sb.ToString().Trim(), "\\s+", " ");
        }
    }
}
