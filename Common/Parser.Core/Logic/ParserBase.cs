using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Types;
using MongoDB.Driver;
using NLog;
using Parser.Core.Configs;
using Parser.Core.Extensions;

namespace Parser.Core.Logic {
    public abstract class ParserBase {
        protected static readonly Logger _logger = LogManager.GetLogger(nameof(Parser));

        protected readonly IParserConfigBase _config;
        private readonly IRepository<BookInfo> _provider;
        
        protected ParserBase(IParserConfigBase config, IRepository<BookInfo> provider) {
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

        protected ExecutionDataflowBlockOptions GetParserOptions() {
            return new() { MaxDegreeOfParallelism = _config.MaxThread, EnsureOrdered = false };
        }

        /// <summary>
        /// Загрузка полученных книг из библиотеки
        /// </summary>
        /// <returns></returns>
        private Task<HashSet<string>> GetProcessed() {
            return _provider.Read(Builders<BookInfo>.Filter.Eq(t => t.ElsName, ElsName), book => book.ExternalId).ContinueWith(t => t.Result.ToHashSet());
        }

        /// <summary>
        /// Сохранение книг в базу
        /// </summary>
        /// <param name="books">Книги</param>
        /// <returns></returns>
        protected Task SaveBooks(IEnumerable<BookInfo> books) {
            return _provider.CreateMany(books);
        }
    }
}
