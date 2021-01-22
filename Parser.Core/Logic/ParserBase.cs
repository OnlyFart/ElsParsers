using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Core.Extensions;
using Core.Providers.Interfaces;
using MongoDB.Driver;
using NLog;
using Parser.Core.Configs;
using Parser.Core.Extensions;
using Parser.Core.Types;

namespace Parser.Core.Logic {
    public abstract class ParserBase {
        protected static readonly Logger _logger = LogManager.GetLogger(nameof(Parser));

        protected readonly IParserConfigBase _config;
        protected readonly IRepository<Book> _provider;
        
        protected ParserBase(IParserConfigBase config, IRepository<Book> provider) {
            _config = config;
            _provider = provider;
        }

        protected abstract string ElsName { get; }

        public async Task Run() {
            var processed = await GetProcessed();
            var blocks = await RunInternal(HttpClientExtensions.GetClient(_config), processed);
            await DataflowExtension.WaitBlocks(blocks);
        }

        protected abstract Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed);

        /// <summary>
        /// Загрузка полученных книг из библиотеки
        /// </summary>
        /// <returns></returns>
        protected Task<HashSet<string>> GetProcessed() {
            return _provider.Read(Builders<Book>.Filter.Eq(t => t.ElsName, ElsName), book => book.ExternalId).ContinueWith(t => new HashSet<string>(t.Result));
        }
    }
}
