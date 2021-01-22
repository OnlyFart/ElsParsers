using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using BiblioClub.Parser.Configs;
using BiblioClub.Parser.Types.API;
using Core.Extensions;
using Core.Providers.Interfaces;
using Newtonsoft.Json;
using Parser.Core.Configs;
using Parser.Core.Extensions;
using Parser.Core.Logic;
using Parser.Core.Types;

namespace BiblioClub.Parser.Logic {
    public class Parser : ParserBase {
        protected override string ElsName => "BiblioClub";

        private static readonly Uri _apiUrl = new Uri("https://biblioclub.ru/services/service.php?page=books&m=GetShortInfo_S&parse&out=json");

        public Parser(IParserConfigBase config, IRepository<Book> provider) : base(config, provider) {

        }

        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            var batchBlock1 = new BatchBlock<long>(1000);
            var getPageBlock = new TransformBlock<long[], IEnumerable<ShortInfo>>(async ids => await GetShortInfo(client, _apiUrl, ids));
            getPageBlock.CompleteMessage(_logger, "Получение краткой информации по всем книгам завершено. Ждем получения библиографического описания.");
            
            var filterBlock = new TransformManyBlock<IEnumerable<ShortInfo>, ShortInfo>(shortInfos => Filter(shortInfos, processed), new ExecutionDataflowBlockOptions{MaxDegreeOfParallelism = 1});
            var batchBlock2 = new BatchBlock<ShortInfo>(50);
            var getBibBlock = new TransformManyBlock<ShortInfo[], Book>(async books => await GetBib(client, books), new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = _config.MaxThread, EnsureOrdered = false });
            getBibBlock.CompleteMessage(_logger, "Получения библиографического описания по всем книгам завершено. Ждем сохранения.");
            
            var batchBlock3 = new BatchBlock<Book>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<Book[]>(async books => await _provider.CreateMany(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранения завершено. Работа программы завершена.");

            batchBlock1.LinkTo(getPageBlock);
            getPageBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(batchBlock2);
            batchBlock2.LinkTo(getBibBlock);
            getBibBlock.LinkTo(batchBlock3);
            batchBlock3.LinkTo(saveBookBlock);

            for (var i = ((IParserConfig)_config).StartIndex; i < ((IParserConfig)_config).EndIndex; i++) {
                await batchBlock1.SendAsync(i);
            }

            return new IDataflowBlock[] {batchBlock1, getPageBlock, filterBlock, batchBlock2, getBibBlock, batchBlock3, saveBookBlock};
        }

        private static IEnumerable<ShortInfo> Filter(IEnumerable<ShortInfo> shortInfos, ISet<string> processed) {
            return shortInfos?.Where(shortInfo => processed.Add(shortInfo.Id.ToString()));
        }

        private static async Task<IEnumerable<ShortInfo>> GetShortInfo(HttpClient client, Uri url, IEnumerable<long> ids) {
            var pairs = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("books_sort", "1"),
            };
            
            pairs.AddRange(ids.Select(id => new KeyValuePair<string, string>("books_ids[]", id.ToString())));

            var dataContent = new FormUrlEncodedContent(pairs.ToArray());
            var content = await client.PostWithTriesAsync(url, dataContent);
            return string.IsNullOrEmpty(content) ? new ShortInfo[]{} : JsonConvert.DeserializeObject<IEnumerable<ShortInfo>>(content);
        }
        

        private async Task<IEnumerable<Book>> GetBib(HttpClient client, ShortInfo[] shortInfos) {
            if (shortInfos == default) {
                return Enumerable.Empty<Book>();
            }
            
            var resp = await client.GetStringWithTriesAsync(new Uri("https://biblioclub.ru/index.php?action=blocks&list=" + string.Join(",", shortInfos.Select(s => "biblio:" + s.Id))));
            if (string.IsNullOrEmpty(resp)) {
                return Enumerable.Empty<Book>();
            }
            
            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(resp);
            return shortInfos.Select(shortInfo => new Book(shortInfo.Id.ToString(), ElsName) {
                Authors = shortInfo.Author,
                Bib = dict["biblio:" + shortInfo.Id],
                ISBN = shortInfo.ISBN,
                Name = shortInfo.Name,
                Pages = shortInfo.Pages ?? 0,
                Publisher = shortInfo.Publisher,
                Year = shortInfo.Year
            });
        }
    }
}