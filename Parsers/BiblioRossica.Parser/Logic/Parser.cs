using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using BiblioRossica.Parser.Types.API;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Types;
using Parser.Core.Configs;
using Parser.Core.Extensions;
using Parser.Core.Logic;

namespace BiblioRossica.Parser.Logic {
    public class Parser : ParserBase {
        private const int BOOKS_PER_PAGE = 100;
        
        private static readonly Uri _baseUrl = new Uri("http://www.bibliorossica.com");
        
        public Parser(IParserConfigBase config, IRepository<BookInfo> provider) : base(config, provider) { }
        
        protected override string ElsName => "BiblioRossica";
        
        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            var getPageBlock = new TransformBlock<int, BookItems>(async catalogItem => await GetBookItems(client, catalogItem));
            getPageBlock.CompleteMessage(_logger, "Обход всех страниц успешно завершен. Ждем получения всех книг.");
            
            var filterBlock = new TransformManyBlock<BookItems, string>(bookItems => Filter(bookItems, processed));
            var getBookBlock = new TransformBlock<string, BookInfo>(async id => await GetBook(client, id), GetParserOptions());
            getBookBlock.CompleteMessage(_logger, "Получение всех книг завершено. Ждем сохранения.");
            
            var batchBlock = new BatchBlock<BookInfo>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<BookInfo[]>(async books => await SaveBooks(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранения завершено. Работа программы завершена.");

            getPageBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(getBookBlock);
            getBookBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);

            var response = await GetBookItems(client, 1);
            var pagesCount = response.Count / BOOKS_PER_PAGE + 1;
            
            _logger.Info($"Всего книг в магазине {response.Count}. Страниц для обхода {pagesCount}");

            for (var i = 1; i <= pagesCount; i++) {
                await getPageBlock.SendAsync(i);
            }

            return new IDataflowBlock[]{getPageBlock, filterBlock, getBookBlock, batchBlock, saveBookBlock};
        }
        
        private static IEnumerable<string> Filter(BookItems bookItems, ISet<string> processed) {
            return bookItems?.Books?.Where(bookItem => processed.Add(bookItem.Id)).Select(bookItem => bookItem.Id) ?? Enumerable.Empty<string>();
        }

        private static async Task<BookItems> GetBookItems(HttpClient client, int page) {
            _logger.Info($"Загружаем страницу {page}");
            
            return await client.GetJson<BookItems>(new Uri(_baseUrl, $"/catalog/perPage/{BOOKS_PER_PAGE}/page/{page}/lang/all"));
        }

        private async Task<BookInfo> GetBook(HttpClient client, string id) {
            var doc = await client.GetHtmlDoc(new Uri(_baseUrl, $"book.html?currBookId={id}"));
            if (doc == default) {
                return default;
            }

            var exploreBook = doc.GetElementbyId("explore_book");
            var bookTitleBlock = exploreBook.GetByFilterFirst("div", "book_book_title");
            var book = new BookInfo(id, ElsName) {
                Authors = bookTitleBlock.GetByFilterFirst("strong")?.InnerText?.Trim() ?? string.Empty,
                Name = bookTitleBlock.GetByFilterFirst("span")?.InnerText?.Trim() ?? string.Empty,
                Bib = exploreBook.GetByFilterFirst("div", "bibliocard_info")?.InnerText?.Trim() ?? string.Empty
            };

            var table = exploreBook.GetByFilterFirst("table");
            foreach (var tds in table.GetByFilter("tr").Select(t => t.GetByFilter("td").ToList()).Where(tds => tds.Count == 2)) {
                var name = tds[0].InnerText.Trim();
                var value = tds[1].InnerText.Trim();

                if (name.Contains("Издатель")) {
                    book.Publisher = value;
                } else if (name.Contains("Публикация")) {
                    var year = Regex.Match(value, @"\d+");
                    if (year.Success) {
                        book.Year = year.Value;
                    }
                } else if (name.Contains("Страниц")) {
                    int.TryParse(value, out book.Pages);
                } else if (name.Contains("ISBN")) {
                    book.ISBN = tds[1].Attributes["title"]?.Value ?? value;
                }
            }

            return book;
        }
    }
}
