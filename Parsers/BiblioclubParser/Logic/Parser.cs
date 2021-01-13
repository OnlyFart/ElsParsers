using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using BiblioclubParser.Configs;
using BiblioclubParser.Types;
using BiblioclubParser.Types.API;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Utils.Helpers;
using Newtonsoft.Json;
using NLog;

namespace BiblioclubParser.Logic {
    public class Parser {
        private static readonly Logger _logger = LogManager.GetLogger(nameof(Parser));
        
        private readonly IParserConfig _config;
        private readonly IBooksProvider<Book> _provider;

        private static readonly Uri _apiUrl = new Uri("https://biblioclub.ru/services/service.php?page=books&m=GetShortInfo_S&parse&out=json");

        public Parser(IParserConfig config, IBooksProvider<Book> provider) {
            _config = config;
            _provider = provider;
        }

        public async Task Parse() {
            var client = HttpClientHelper.GetClient(_config);

            var processed = _provider.GetProcessed().ContinueWith(t => new HashSet<long>(t.Result));

            var batchBlock1 = new BatchBlock<long>(1000);
            var getPageBlock = new TransformBlock<long[], IEnumerable<ShortInfo>>(async ids => await GetShortInfo(client, _apiUrl, ids));
            getPageBlock.CompleteMessage(_logger, "Получение краткой информации по всем книгам завершено. Ждем получения библиографического описания.");
            
            var filterBlock = new TransformManyBlock<IEnumerable<ShortInfo>, ShortInfo>(async shortInfos => Filter(shortInfos, await processed));
            var batchBlock2 = new BatchBlock<ShortInfo>(50);
            var getBibBlock = new TransformManyBlock<ShortInfo[], Book>(async books => await GetBib(client, books), new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = _config.MaxThread, EnsureOrdered = false });
            getBibBlock.CompleteMessage(_logger, "Получения библиографического описания по всем книгам завершено. Ждем сохранения.");
            
            var batchBlock3 = new BatchBlock<Book>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<Book[]>(async books => await _provider.Save(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранения завершено. Работа программы завершена.");

            batchBlock1.LinkTo(getPageBlock);
            getPageBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(batchBlock2);
            batchBlock2.LinkTo(getBibBlock);
            getBibBlock.LinkTo(batchBlock3);
            batchBlock3.LinkTo(saveBookBlock);

            for (var i = _config.StartIndex; i < _config.EndIndex; i++) {
                await batchBlock1.SendAsync(i);
            }

            await DataflowExtension.WaitBlocks(batchBlock1, getPageBlock, filterBlock, batchBlock2, getBibBlock, batchBlock3, saveBookBlock);
        }

        private static IEnumerable<ShortInfo> Filter(IEnumerable<ShortInfo> shortInfos, ICollection<long> processed) {
            return shortInfos?.Where(shortInfo => !processed.Contains(shortInfo.Id));
        }

        private static async Task<IEnumerable<ShortInfo>> GetShortInfo(HttpClient client, Uri url, IEnumerable<long> ids) {
            var pairs = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("books_sort", "1"),
            };
            
            pairs.AddRange(ids.Select(id => new KeyValuePair<string, string>("books_ids[]", id.ToString())));

            var dataContent = new FormUrlEncodedContent(pairs.ToArray());
            var content = await HttpClientHelper.PostAsync(client, url, dataContent);
            return string.IsNullOrEmpty(content) ? null : JsonConvert.DeserializeObject<IEnumerable<ShortInfo>>(content);
        }
        

        private static async Task<IEnumerable<Book>> GetBib(HttpClient client, ShortInfo[] shortInfos) {
            if (shortInfos == null) {
                return null;
            }
            
            var resp = await HttpClientHelper.GetStringAsync(client, new Uri("https://biblioclub.ru/index.php?action=blocks&list=" + string.Join(",", shortInfos.Select(s => "biblio:" + s.Id))));
            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(resp);

            return shortInfos.Select(shortInfo => new Book(shortInfo) {Bib = dict["biblio:" + shortInfo.Id]});
        }
    }
}