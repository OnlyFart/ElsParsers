using Core.Types;

namespace UraitParser.Types {
    /// <summary>
    /// 
    /// </summary>
    public class Book : BookBase {
        public Book() {

        }
        
        public string Name;
        public int Year;
        public string Authors;
        public string Bib;
        public string ISBN;
        public int Pages;
    }
}
