using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace Core.Extensions {
    public static class HtmlNodeExtensions {
        public static IEnumerable<HtmlNode> GetByFilter(this HtmlNode node, string name, string className) {
            return node.Descendants().Where(t => t.Name == name && t.Attributes["class"]?.Value?.Contains(className) == true);
        }
        
        public static IEnumerable<HtmlNode> GetByFilter(this HtmlNode node, string name) {
            return node.Descendants().Where(t => t.Name == name);
        }
    }
}
