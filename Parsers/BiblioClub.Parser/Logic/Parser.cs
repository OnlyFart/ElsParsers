using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using BiblioClub.Parser.Configs;
using BiblioClub.Parser.Types;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Types;
using Parser.Core.Configs;
using Parser.Core.Extensions;
using Parser.Core.Logic;

namespace BiblioClub.Parser.Logic {
    public class Parser : ParserBase {
        protected override string ElsName => "BiblioClub";

        private static readonly Uri _apiUrl = new Uri("https://biblioclub.ru/services/service.php?page=books&m=GetShortInfo_S&parse&out=json");

        public Parser(IParserConfigBase config, IRepository<BookInfo> provider) : base(config, provider) {

        }

        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            var batchBlock1 = new BatchBlock<long>(1000);
            var getPageBlock = new TransformBlock<long[], IEnumerable<BookShortInfo>>(async ids => await GetShortInfo(client, _apiUrl, ids));
            getPageBlock.CompleteMessage(_logger, "Получение краткой информации по всем книгам завершено. Ждем получения библиографического описания.");
            
            var filterBlock = new TransformManyBlock<IEnumerable<BookShortInfo>, BookShortInfo>(shortInfos => Filter(shortInfos, processed));
            var batchBlock2 = new BatchBlock<BookShortInfo>(50);
            var getBibBlock = new TransformManyBlock<BookShortInfo[], BookInfo>(async books => await GetBib(client, books), GetParserOptions());
            getBibBlock.CompleteMessage(_logger, "Получения библиографического описания по всем книгам завершено. Ждем сохранения.");
            
            var batchBlock3 = new BatchBlock<BookInfo>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<BookInfo[]>(async books => await SaveBooks(books));
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

        private static IEnumerable<BookShortInfo> Filter(IEnumerable<BookShortInfo> shortInfos, ISet<string> processed) {
            return shortInfos?.Where(shortInfo => processed.Add(shortInfo.Id.ToString()));
        }

        private static async Task<IEnumerable<BookShortInfo>> GetShortInfo(HttpClient client, Uri url, ICollection<long> ids) {
            var pairs = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("books_sort", "1"),
            };
            
            _logger.Info($"Запрашиваем книги с индексами [{ids.Min()}, {ids.Max()}]");
            
            pairs.AddRange(ids.Select(id => new KeyValuePair<string, string>("books_ids[]", id.ToString())));

            var dataContent = new FormUrlEncodedContent(pairs.ToArray());
            return await client.PostJson<IEnumerable<BookShortInfo>>(url, dataContent) ?? Enumerable.Empty<BookShortInfo>();
        }
        

        private async Task<IEnumerable<BookInfo>> GetBib(HttpClient client, BookShortInfo[] shortInfos) {
            if (shortInfos == default) {
                return Enumerable.Empty<BookInfo>();
            }
            
            var dict = await client.GetJson<Dictionary<string, string>>(new Uri("https://biblioclub.ru/index.php?action=blocks&list=" + shortInfos.Select(s => "biblio:" + s.Id).StrJoin(",")));
            if (dict == default) {
                return Enumerable.Empty<BookInfo>();
            }
            
            return shortInfos.Select(shortInfo => new BookInfo(shortInfo.Id.ToString(), ElsName) {
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