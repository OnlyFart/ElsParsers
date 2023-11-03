using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Types;
using MongoDB.Driver;
using NLog;
using Parser.Core.Configs;

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
            var blocks = await RunInternal(GetClient(), processed);
            await DataflowExtension.WaitBlocks(blocks);
        }
        

        protected virtual HttpClient FillClient(HttpClient client) {
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            client.DefaultRequestHeaders.Add("User-Agent" ,"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.88 Safari/537.36");

            return client;
        }

        protected virtual HttpClient GetBaseClient(IParserConfigBase config) {
            var handler = new HttpClientHandler {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                UseProxy = false,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            };
            
            if (!string.IsNullOrEmpty(config.Proxy)) {
                handler.Proxy = new WebProxy(config.Proxy);
                handler.UseProxy = true;
            }
            
            return new HttpClient(handler);
        }

        private HttpClient GetClient() {
            return FillClient(GetBaseClient(_config));
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
