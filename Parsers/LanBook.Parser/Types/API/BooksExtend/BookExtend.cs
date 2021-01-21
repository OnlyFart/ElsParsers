using System.Collections.Generic;

namespace LanBook.Parser.Types.API.BooksExtend {
    public class BookExtend {
        public long Id;
        public string Name;
        public string Description;
        public int? Year;
        public string Authors;
        public string BookTitleInfo;
        public string BiblioRecord;
        public string CoverImage;
        public string Edition;
        public List<string> EducationLevels;
        public string ISBN;
        public int? Pages;
        public string PublisherName;
    }
}
