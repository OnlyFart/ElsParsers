using BiblioclubParser.Types.API;
using Core.Types;

namespace BiblioclubParser.Types {
    /// <summary>
    /// 
    /// </summary>
    public class Book : BookBase {
        public Book(ShortInfo shortInfo) {
            Id = shortInfo.Id;
            Year = shortInfo.Year;
            Name = shortInfo.Name;
            ISBN = shortInfo.ISBN;
            Publisher = shortInfo.Publisher;
            Authors = shortInfo.Author;
            Disciple = shortInfo.Disciplini;
            Pages = shortInfo.Pages ?? 0;
        }

        public string Disciple;
        public string Authors;
        public string ISBN;
        public string Publisher;
        public string Name;
        public string Year;
        public string Bib;
        public int Pages;
    }
}
