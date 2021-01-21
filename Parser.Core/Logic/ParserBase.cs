using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Providers.Interfaces;
using MongoDB.Driver;
using NLog;
using Parser.Core.Types;

namespace Parser.Core.Logic {
    public abstract class ParserBase {
        protected static readonly Logger _logger = LogManager.GetLogger(nameof(Parser));
        
        protected readonly IRepository<Book> _provider;
        
        protected ParserBase(IRepository<Book> provider) {
            _provider = provider;
        }

        protected abstract string ElsName { get; }

        /// <summary>
        /// Загрузка полученных книг из библиотеки
        /// </summary>
        /// <returns></returns>
        protected Task<HashSet<string>> GetProcessed() {
            return _provider.Read(Builders<Book>.Filter.Eq(t => t.ElsName, ElsName), book => book.ExternalId).ContinueWith(t => new HashSet<string>(t.Result));
        }
    }
}
