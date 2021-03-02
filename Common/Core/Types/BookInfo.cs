using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Types {
    [BsonIgnoreExtraElements]
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
        public ObjectId Id;

        /// <summary>
        /// Идентификатор книги
        /// </summary>
        public string ExternalId;

        /// <summary>
        /// Название библиотеки
        /// </summary>
        public string ElsName;

        /// <summary>
        /// Авторы книги
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

        /// <summary>
        /// Коллекция "похожих" книг
        /// </summary>
        public Dictionary<string, HashSet<SimilarInfo>> SimilarBooks;

        public bool Compared;

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
            return Id.GetHashCode();
        }

        public bool AddSimilar(BookInfo book, BookComparerResult compareResult) {
            var similarInfo = new SimilarInfo(book, compareResult);

            lock (SimilarBooks) {
                if (!SimilarBooks.TryGetValue(book.ElsName, out var similar)) {
                    similar = new HashSet<SimilarInfo>();
                    SimilarBooks[book.ElsName] = similar;
                }
                
                return similar.Add(similarInfo);
            }
        }
    }
}