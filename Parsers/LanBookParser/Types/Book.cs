using System.Collections.Generic;
using BookCore.Types;
using LanBookParser.Types.API.BooksExtend;
using LanBookParser.Types.API.BooksShort;

namespace LanBookParser.Types {
    /// <summary>
    /// 
    /// </summary>
    public class Book : BookBase {
        public Book(BookShort bookShort, BookExtend bookExtend) {
            Id = bookExtend.Id;
            Name = bookExtend.Name;
            Description = bookExtend.Description;
            Year = (bookExtend.Year ?? -1).ToString();
            Authors = bookExtend.Authors;
            BookTitleInfo = bookExtend.BookTitleInfo;
            Bib = bookExtend.BiblioRecord;
            CoverImage = bookExtend.CoverImage;
            Edition = bookExtend.Edition;
            EducationLevels = bookExtend.EducationLevels;
            ISBN = bookExtend.ISBN;
            Pages = bookExtend.Pages ?? -1;
            Publisher = bookExtend.PublisherName;
            TypeName = bookShort.TypeName;
        }
        
        public readonly string Description;
        public readonly string BookTitleInfo;
        public readonly string CoverImage;
        public readonly string Edition;
        public readonly List<string> EducationLevels;
        public readonly string TypeName;
    }
}
