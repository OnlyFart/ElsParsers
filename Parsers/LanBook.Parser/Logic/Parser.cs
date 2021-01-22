using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Core.Extensions;
using Core.Providers.Interfaces;
using LanBook.Parser.Configs;
using LanBook.Parser.Types.API;
using LanBook.Parser.Types.API.BooksExtend;
using LanBook.Parser.Types.API.BooksShort;
using Newtonsoft.Json;
using Parser.Core.Configs;
using Parser.Core.Extensions;
using Parser.Core.Logic;
using Parser.Core.Types;

namespace LanBook.Parser.Logic {
    public class Parser : ParserBase {
        protected override string ElsName => "LanBook";

        public Parser(IParserConfigBase config, IRepository<Book> provider) : base(config, provider) {
        }
        
        private const int BOOKS_PER_PAGE = 1000;

        private static readonly string _allBooksUrlPattern = "https://e.lanbook.com/api/v2/catalog/books?category=0&limit=" + BOOKS_PER_PAGE  + "&page={0}";

        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            var getPageBlock = new TransformBlock<int, ApiResponse<BooksShortBody>>(async page => await GetSearchResponse(client, page));
            getPageBlock.CompleteMessage(_logger, "Обход всего каталога успешно завершен. Ждем получения всех книг.");
            
            var filterBlock = new TransformManyBlock<ApiResponse<BooksShortBody>, BookShort>(apiResponse => Filter(apiResponse, processed), new ExecutionDataflowBlockOptions{MaxDegreeOfParallelism = 1});
            var getBookBlock = new TransformBlock<BookShort, Book>(async book => await GetBook(client, book), new ExecutionDataflowBlockOptions {MaxDegreeOfParallelism = _config.MaxThread, EnsureOrdered = false});
            getBookBlock.CompleteMessage(_logger, "Получение всех книг завершено. Ждем сохранения.");
            
            var batchBlock = new BatchBlock<Book>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<Book[]>(async books => await _provider.CreateMany(books));
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

        private static IEnumerable<BookShort> Filter(ApiResponse<BooksShortBody> response, ISet<string> processed) {
            if (response == default) {
                return Enumerable.Empty<BookShort>();
            }
            
            var books = response.Body.Items.Where(t => processed.Add(t.Id.ToString()));
            var extra = response.Body.Extra.Where(t => processed.Add(t.Id.ToString()));
            return books.Union(extra);
        }

        /// <summary>
        /// Даже не пытался сделать этот метод понятным
        /// </summary>
        /// <param name="client"></param>
        /// <param name="bookShort"></param>
        /// <returns></returns>
        private async Task<Book> GetBook(HttpClient client, BookShort bookShort) {
            if (bookShort == default) {
                return default;
            }
            
            var content = await client.GetStringWithTriesAsync(new Uri($"https://e.lanbook.com/api/v2/catalog/book/{bookShort.Id}"));
            var bookExtend = JsonConvert.DeserializeObject<ApiResponse<BookExtend>>(content);
            return string.IsNullOrEmpty(content) ? default : new Book(bookShort.Id.ToString(), ElsName) {
                Authors = bookExtend.Body.Authors,
                Bib = bookExtend.Body.BiblioRecord,
                ISBN = bookExtend.Body.ISBN,
                Name = bookExtend.Body.Name,
                Pages = bookExtend.Body.Pages ?? 0,
                Publisher = bookExtend.Body.PublisherName,
                Year = bookExtend.Body.Year?.ToString() ?? string.Empty
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