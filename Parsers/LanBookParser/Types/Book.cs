using System.Collections.Generic;
using Core.Types;
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
            Year = bookExtend.Year ?? -1;
            Authors = bookExtend.Authors;
            BookTitleInfo = bookExtend.BookTitleInfo;
            BiblioRecord = bookExtend.BiblioRecord;
            CoverImage = bookExtend.CoverImage;
            Edition = bookExtend.Edition;
            EducationLevels = bookExtend.EducationLevels;
            ISBN = bookExtend.ISBN;
            Pages = bookExtend.Pages ?? -1;
            PublisherName = bookExtend.PublisherName;
            TypeName = bookShort.TypeName;
        }
        
        public readonly string Name;
        public readonly string Description;
        public readonly int Year;
        public readonly string Authors;
        public readonly string BookTitleInfo;
        public readonly string BiblioRecord;
        public readonly string CoverImage;
        public readonly string Edition;
        public readonly List<string> EducationLevels;
        public readonly string ISBN;
        public readonly int Pages;
        public readonly string PublisherName;
        public readonly string TypeName;
    }
}
