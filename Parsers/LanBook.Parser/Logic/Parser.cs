using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Types;
using LanBook.Parser.Configs;
using LanBook.Parser.Types.API;
using LanBook.Parser.Types.API.BooksExtend;
using LanBook.Parser.Types.API.BooksShort;
using Newtonsoft.Json;
using Parser.Core.Configs;
using Parser.Core.Extensions;
using Parser.Core.Logic;

namespace LanBook.Parser.Logic {
    public class Parser : ParserBase {
        protected override string ElsName => "LanBook";

        public Parser(IParserConfigBase config, IRepository<BookInfo> provider) : base(config, provider) {
        }
        
        private const int BOOKS_PER_PAGE = 1000;

        private static readonly string _allBooksUrlPattern = "https://e.lanbook.com/api/v2/catalog/books?category=0&limit=" + BOOKS_PER_PAGE  + "&page={0}";

        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            var getPageBlock = new TransformBlock<int, ApiResponse<BooksShortBody>>(async page => await GetSearchResponse(client, page));
            getPageBlock.CompleteMessage(_logger, "Обход всего каталога успешно завершен. Ждем получения всех книг.");
            
            var filterBlock = new TransformManyBlock<ApiResponse<BooksShortBody>, long>(apiResponse => Filter(apiResponse, processed));
            var getBookBlock = new TransformBlock<long, BookInfo>(async id => await GetBook(client, id), GetParserOptions());
            getBookBlock.CompleteMessage(_logger, "Получение всех книг завершено. Ждем сохранения.");
            
            var batchBlock = new BatchBlock<BookInfo>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<BookInfo[]>(async books => await _provider.CreateMany(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранения завершено. Работа программы завершена.");

            getPageBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(getBookBlock);
            getBookBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);

            var response = await GetSearchResponse(client, 1);
            var pagesCount = response.Body.Total / BOOKS_PER_PAGE + 1;
            
            _logger.Info($"Всего книг в магазине {response.Body.Total}. Страниц для обхода {pagesCount}");
            
            for (var i = ((IParserConfig)_config).StartPage; i <= pagesCount; i++) {
                await getPageBlock.SendAsync(i);
            }

            return new IDataflowBlock[] {getPageBlock, filterBlock, getBookBlock, batchBlock, saveBookBlock};
        }

        private static IEnumerable<long> Filter(ApiResponse<BooksShortBody> response, ISet<string> processed) {
            if (response == default) {
                return Enumerable.Empty<long>();
            }
            
            var books = response.Body.Items.Where(t => processed.Add(t.Id.ToString()));
            var extra = response.Body.Extra.Where(t => processed.Add(t.Id.ToString()));
            return books.Union(extra).Select(t => t.Id);
        }

        private async Task<BookInfo> GetBook(HttpClient client, long id) {
            var content = await client.GetStringWithTriesAsync(new Uri($"https://e.lanbook.com/api/v2/catalog/book/{id}"));
            var bookExtend = JsonConvert.DeserializeObject<ApiResponse<BookExtend>>(content).Body;
            
            return string.IsNullOrEmpty(content) ? default : new BookInfo(id.ToString(), ElsName) {
                Authors = bookExtend.Authors,
                Bib = bookExtend.BiblioRecord,
                ISBN = bookExtend.ISBN,
                Name = bookExtend.Name,
                Pages = bookExtend.Pages ?? 0,
                Publisher = bookExtend.PublisherName,
                Year = bookExtend.Year?.ToString() ?? string.Empty
            };
        }

        /// <summary>
        /// Получение списка всех книг
        /// </summary>
        /// <param name="client">HttpClient</param>
        /// <param name="page">Страница</param>
        /// <returns></returns>
        private static async Task<ApiResponse<BooksShortBody>> GetSearchResponse(HttpClient client, int page) {
            _logger.Info($"Запрашиваем страницу {page}");
            
            var response = await client.GetStringWithTriesAsync(new Uri(string.Format(_allBooksUrlPattern, page)));
            return string.IsNullOrEmpty(response) ? default : JsonConvert.DeserializeObject<ApiResponse<BooksShortBody>>(response);
        }
    }
}