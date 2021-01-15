using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Utils.Helpers;
using LanBookParser.Configs;
using LanBookParser.Types;
using LanBookParser.Types.API;
using LanBookParser.Types.API.BooksExtend;
using LanBookParser.Types.API.BooksShort;
using Newtonsoft.Json;
using NLog;

namespace LanBookParser.Logic {
    public class Parser {
        private static readonly Logger _logger = LogManager.GetLogger(nameof(Parser));
        
        private readonly IParserConfig _config;
        private readonly IRepository<Book> _provider;

        public Parser(IParserConfig config, IRepository<Book> provider) {
            _config = config;
            _provider = provider;
        }
        
        private const int BOOKS_PER_PAGE = 1000;

        private static readonly string _allBooksUrlPattern = "https://e.lanbook.com/api/v2/catalog/books?category=0&limit=" + BOOKS_PER_PAGE  + "&page={0}";

        public async Task Parse() {
            var client = HttpClientHelper.GetClient(_config);
            
            var processed = _provider.ReadProjection(book => book.Id).ContinueWith(t => new HashSet<long>(t.Result));

            var getPageBlock = new TransformBlock<int, ApiResponse<BooksShortBody>>(async page => await GetSearchResponse(client, page));
            getPageBlock.CompleteMessage(_logger, "Обход всего каталога успешно завершен. Ждем получения всех книг.");
            
            var filterBlock = new TransformManyBlock<ApiResponse<BooksShortBody>, BookShort>(async reponse => Filter(reponse, await processed));
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
            
            for (var i = _config.StartPage; i <= pagesCount; i++) {
                await getPageBlock.SendAsync(i);
            }

            await DataflowExtension.WaitBlocks(getPageBlock, filterBlock, getBookBlock, batchBlock, saveBookBlock);
        }

        private static IEnumerable<BookShort> Filter(ApiResponse<BooksShortBody> response, IEnumerable<long> processed) {
            if (response == default) {
                return Enumerable.Empty<BookShort>();
            }
            
            var books = response.Body.Items.Where(t => !processed.Contains(t.Id));
            var extra = response.Body.Extra.Where(t => !processed.Contains(t.Id));
            return books.Union(extra);
        }

        /// <summary>
        /// Даже не пытался сделать этот метод понятным
        /// </summary>
        /// <param name="client"></param>
        /// <param name="bookShort"></param>
        /// <returns></returns>
        private static async Task<Book> GetBook(HttpClient client, BookShort bookShort) {
            if (bookShort == default) {
                return default;
            }
            
            var content = await HttpClientHelper.GetStringAsync(client, new Uri($"https://e.lanbook.com/api/v2/catalog/book/{bookShort.Id}"));
            var bookExtend = JsonConvert.DeserializeObject<ApiResponse<BookExtend>>(content);
            return string.IsNullOrEmpty(content) ? default : new Book(bookShort, bookExtend.Body);
        }

        /// <summary>
        /// Получение списка всех книг
        /// </summary>
        /// <param name="client">HttpClient</param>
        /// <param name="page">Страница</param>
        /// <returns></returns>
        private static async Task<ApiResponse<BooksShortBody>> GetSearchResponse(HttpClient client, int page) {
            _logger.Info($"Запрашиваем страницу {page}");
            
            var response = await HttpClientHelper.GetStringAsync(client, new Uri(string.Format(_allBooksUrlPattern, page)));
            return string.IsNullOrEmpty(response) ? default : JsonConvert.DeserializeObject<ApiResponse<BooksShortBody>>(response);
        }
    }
}