using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Types {
    public class BookInfo {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="externalId">Идентификатор книги в библиотеке</param>
        /// <param name="elsName">Название библиотеки</param>
        public BookInfo(string externalId, string elsName) {
            ExternalId = externalId;
            ElsName = elsName;
        }

        [BsonIgnoreIfDefault, BsonIgnoreIfNull]
        public object Id;

        /// <summary>
        /// Идентификатор книги
        /// </summary>
        public string ExternalId;

        /// <summary>
        /// Название библиотеки
        /// </summary>
        public string ElsName;

        /// <summary>
        /// Авторы кники
        /// </summary>
        public string Authors;

        /// <summary>
        /// ISBN
        /// </summary>
        public string ISBN;

        /// <summary>
        /// ISSN
        /// </summary>
        public string ISSN;

        /// <summary>
        /// Издательство
        /// </summary>
        public string Publisher;

        /// <summary>
        /// Название
        /// </summary>
        public string Name;

        /// <summary>
        /// Год издания
        /// </summary>
        public string Year;

        /// <summary>
        /// Библиографическое описание
        /// </summary>
        public string Bib;

        /// <summary>
        /// Кол-во страниц
        /// </summary>
        public int Pages;

        public BookComparerResult ComparerResult;

        public HashSet<BookInfo> SimilarBooks;
        public HashSet<BookInfo> SimilarBibs;

        public bool Compared;

        private BookInfo Clone() {
            return new(ExternalId, ElsName) {
                Authors = Authors,
                ISBN = ISBN,
                ISSN = ISSN,
                Publisher = Publisher,
                Name = Name,
                Year = Year,
                Bib = Bib,
                Pages = Pages,
                ComparerResult = ComparerResult
            };
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return false;
            }

            if (ReferenceEquals(this, obj)) {
                return true;
            }

            var other = (BookInfo) obj;
            return obj.GetType() == GetType() && Id == other.Id;
        }

        public override int GetHashCode() {
            return HashCode.Combine(ExternalId, ElsName);
        }

        public void AddSimilar(BookInfo book, BookComparerResult compareResult) {
            var clone = book.Clone();
            clone.ComparerResult = compareResult;

            if (book.ElsName == Const.BIB_ELS) {
                lock (SimilarBibs) {
                    SimilarBibs.Add(clone);
                }
            } else {
                lock (SimilarBooks) {
                    SimilarBooks.Add(clone);
                }
            }
        }
    }
}