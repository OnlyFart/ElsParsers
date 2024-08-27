using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using BookRu.Parser.Types.API.Book;
using BookRu.Parser.Types.API.Categories;
using BookRu.Parser.Types.API.Sidebar;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Types;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Parser.Core.Configs;
using Parser.Core.Logic;

namespace BookRu.Parser.Logic {
    public class Parser : ParserBase {
        public Parser(IParserConfigBase config, IRepository<BookInfo> provider) : base(config, provider) { }

        protected override string ElsName => "BookRu";

        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            var nextData = await GetNextData(client);
            var buildId = nextData.RootElement.GetProperty("buildId").GetString();
            
            var getBookIdsBlock = new TransformBlock<MenuItem, IEnumerable<string>>(async categoryId => await GetBookIds(client, buildId, categoryId));
            getBookIdsBlock.CompleteMessage(_logger, "Получение каталогов книг закончено. Ждем получения книг.");

            var filterBlock = new TransformManyBlock<IEnumerable<string>, string>(bookIds => Filter(bookIds, processed));
            var getBooksBlock = new TransformBlock<string, BookInfo>(async bookId => await GetBook(client, buildId, bookId), GetParserOptions());
            getBooksBlock.CompleteMessage(_logger, "Получение книг закончено. Ждем сохранения.");

            var batchBlock = new BatchBlock<BookInfo>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<BookInfo[]>(async books => await SaveBooks(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранение завершено.");

            getBookIdsBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(getBooksBlock);
            getBooksBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);

            foreach (var categoryId in GetCategoryIds(nextData)) {
                await getBookIdsBlock.SendAsync(categoryId);
            }
            
            return [getBookIdsBlock, filterBlock, getBooksBlock, batchBlock, saveBookBlock];
        }
        
        private async Task<JsonDocument> GetNextData(HttpClient client) {
            var response = await client.GetStringAsync(new Uri("https://www.book.ru/book"));
            var doc = new HtmlDocument();
            doc.LoadHtml(response);

            var json = doc.GetElementbyId("__NEXT_DATA__").InnerText;
            return JsonDocument.Parse(json);
        }

        private static IEnumerable<string> Filter(IEnumerable<string> bookIds, ISet<string> processed) {
            return bookIds.Where(processed.Add);
        }

        private static IEnumerable<MenuItem> GetCategoryIds(JsonDocument nextData) {
            var sidebar = JsonConvert.DeserializeObject<Sidebar[]>(nextData
                .RootElement.GetProperty("props")
                .GetProperty("pageProps")
                .GetProperty("serverMenu")
                .GetRawText());
            
            return sidebar.SelectMany(s => s.Menu);
        }

        private static async Task<IEnumerable<string>> GetBookIds(HttpClient client, string buildId, MenuItem menuItem) {
            if (menuItem.Id == "new") {
                return Enumerable.Empty<string>();
            }
            
            _logger.Info($"Получаем каталог с ID = {menuItem.Id}, Name = {menuItem.Name}");
            
            var response = await client.GetStringAsync(new Uri($"https://book.ru/_next/data/{buildId}/cat/{menuItem.Id}.json"));
            var json = JsonDocument.Parse(response);

            return JsonConvert.DeserializeObject<CategoryContent>(json.RootElement.GetProperty("pageProps").GetProperty("allServerData").GetRawText()).Item.Select(i => i.Id.ToString());
        }

        private async Task<BookInfo> GetBook(HttpClient client, string buildId, string id) {
            var response = await client.GetAsync(new Uri($"https://book.ru/_next/data/{buildId}/book/{id}.json?"));
            if (response.StatusCode != HttpStatusCode.OK) {
                return default;
            }
            
            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var items = JsonConvert.DeserializeObject<BookItem[]>(json.RootElement.GetProperty("pageProps").GetProperty("serverDataBook").GetProperty("item").GetRawText());

            if (items.Length == 0) {
                return default;
            }

            var book = items[0];
            return new BookInfo(id, ElsName) {
                Authors = book.Author,
                Bib = book.BiblioDesc,
                ISBN = book.ISBN,
                Name = book.Name,
                Pages = book.Pages ?? 0,
                Year = book.Year,
                Publisher = book.Publisher
            };
        }
    }
}
