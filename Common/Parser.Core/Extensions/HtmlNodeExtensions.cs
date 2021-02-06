using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace Parser.Core.Extensions {
    /// <summary>
    /// Расширения для работы с HtmlNode
    /// </summary>
    public static class HtmlNodeExtensions {
        public static HtmlNode GetByFilterFirst(this HtmlNode node, string name, string className) {
            return GetByFilter(node, name, className).FirstOrDefault();
        }
        
        public static HtmlNode GetByFilterFirst(this HtmlNode node, string name) {
            return GetByFilter(node, name).FirstOrDefault();
        }
        
        public static IEnumerable<HtmlNode> GetByFilter(this HtmlNode node, string name, string className) {
            return node.Descendants().Where(t => t.Name == name && t.Attributes["class"]?.Value?.Contains(className) == true);
        }
        
        public static IEnumerable<HtmlNode> GetByFilterEq(this HtmlNode node, string name, string className) {
            return node.Descendants().Where(t => t.Name == name && t.Attributes["class"]?.Value == className);
        }
        
        public static IEnumerable<HtmlNode> GetByFilter(this HtmlNode node, string name) {
            return node.Descendants().Where(t => t.Name == name);
        }
    }
}
