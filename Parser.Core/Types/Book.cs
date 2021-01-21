namespace Parser.Core.Types {
    public class Book {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="externalId">Идентификатор книги в библиотеке</param>
        /// <param name="elsName">Название библиотеки</param>
        public Book(string externalId, string elsName) {
            ExternalId = externalId;
            ElsName = elsName;
        }

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
    }
}
