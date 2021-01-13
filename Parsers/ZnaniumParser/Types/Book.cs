using Core.Types;

namespace ZnaniumParser.Types {
    /// <summary>
    /// 
    /// </summary>
    public class Book : BookBase{
        public Book() {

        }

        public string Name;
        public int Year;
        public string Article;
        public string Authors;
        public string Bib;
        public string ISBN;
        public string IsbnOnline;
        public int Pages;
        public string Publisher;
        public string DOI;
    }
}
