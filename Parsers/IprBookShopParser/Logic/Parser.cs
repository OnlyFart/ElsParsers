using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Utils.Helpers;
using HtmlAgilityPack;
using IprBookShopParser.Configs;
using IprBookShopParser.Types;
using Newtonsoft.Json;
using NLog;

namespace IprBookShopParser.Logic {
    public class Parser {
        private static readonly Logger _logger = LogManager.GetLogger(nameof(Parser));
        
        private readonly IParserConfig _config;
        private readonly IBooksProvider<Book> _provider;

        public Parser(IParserConfig config, IBooksProvider<Book> provider) {
            _config = config;
            _provider = provider;
        }

        private static readonly Uri _apiUrl = new Uri("http://www.iprbookshop.ru/78575");
        
        private const int BOOKS_PER_PAGE = 20;

        public async Task Parse() {
            var client = HttpClientHelper.GetClient(_config);
            
            var processed = new HashSet<long>(await _provider.GetProcessed());

            var getPageBlock = new TransformBlock<int, SearchResponseData>(async page => await GetSearchResponse(client, page));
            getPageBlock.CompleteMessage(_logger, "Обход всех страниц успешно завершен. Ждем получения всех книг.");
            
            var filterBlock = new TransformManyBlock<SearchResponseData, SearchData>(page => page.Data.Where(t => !processed.Contains(t.Id)));
            var getBookBlock = new TransformBlock<SearchData, Book>(async book => await GetBook(client, book.Id), new ExecutionDataflowBlockOptions {MaxDegreeOfParallelism = _config.MaxThread, EnsureOrdered = false});
            getBookBlock.CompleteMessage(_logger, "Получение всех книг завершено. Ждем сохранения.");
            
            var batchBlock = new BatchBlock<Book>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<Book[]>(async books => await _provider.Save(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранения завершено. Работа программы завершена.");

            getPageBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(getBookBlock);
            getBookBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);

            var response = await GetSearchResponse(client, 1);
            var pagesCount = response.Count / BOOKS_PER_PAGE + 1;
            
            _logger.Info($"Всего книг в магазине {response.Count}. Страниц для обхода {pagesCount}");
            
            for (var i = _config.StartPage; i <= pagesCount; i++) {
                await getPageBlock.SendAsync(i);
            }

            await DataflowExtension.WaitBlocks(getPageBlock, filterBlock, getBookBlock, batchBlock, saveBookBlock);
        }

        /// <summary>
        /// Даже не пытался сделать этот метод понятным
        /// </summary>
        /// <param name="client"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private static async Task<Book> GetBook(HttpClient client, long id) {
            var url = new Uri($"http://www.iprbookshop.ru/{id}.html");

            var content = await HttpClientHelper.GetStringAsync(client, url);

            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var bookInfoBlock = doc.DocumentNode.GetByFilter("div", "book-information").FirstOrDefault();

            var book = new Book {
                Id = id,
                Name = Book.Normalize(bookInfoBlock?.GetByFilter("h4", "header-orange")?.FirstOrDefault()?.InnerText),
                Bib = Book.Normalize(bookInfoBlock?.GetByFilter("h3", "header-green")?.FirstOrDefault()?.NextSibling.NextSibling.InnerText)
            };

            var bookDescriptionBlock = bookInfoBlock.GetByFilter("div", "col-sm-10").FirstOrDefault();
            foreach (var row in bookDescriptionBlock.GetByFilter("div", "row")) {
                var strong = row.Descendants().FirstOrDefault(t => t.Name == "strong");
                if (strong == default || string.IsNullOrWhiteSpace(strong.InnerText)) {
                    continue;
                }

                var description = Book.Normalize(GetDescription(row));
                if (strong.InnerText.Contains("Издательство")) {
                    book.Publisher = description;
                } else if (strong.InnerText.Contains("Авторы")) {
                    book.Authors = description;
                } else if (strong.InnerText.Contains("Год издания")) {
                    book.Year = description;
                } else if (strong.InnerText.Contains("ISBN")) {
                    book.ISBN = description;
                } else if (strong.InnerText.Contains("Тип издания")) {
                    book.Type = description;
                } else if (strong.InnerText.Contains("ISSN")) {
                    book.ISSN = description;
                } else if (strong.InnerText.Contains("Гриф")) {
                    book.Grif = description;
                } else if (strong.InnerText.Contains("ответственности")) {
                    book.Response = description;
                } else {
                    _logger.Warn($"Появилось новое поле {strong.InnerText}, которое не сохраняется в базу");
                }
            }

            return book;
        }

        private static string GetDescription(HtmlNode node) {
            return node.GetByFilter("div", "col-sm-9").FirstOrDefault()?.InnerText.Trim();
        }

        /// <summary>
        /// Получение результатов поискового запроса
        /// </summary>
        /// <param name="client">HttpClient</param>
        /// <param name="page">Страница</param>
        /// <returns></returns>
        private static async Task<SearchResponseData> GetSearchResponse(HttpClient client, int page) {
            _logger.Info($"Запрашиваем страницу {page}");

            var values = new[] {
                new KeyValuePair<string, string>("profile_id", ""),
                new KeyValuePair<string, string>("ugs", ""),
                new KeyValuePair<string, string>("subpubhouse", ""),
                new KeyValuePair<string, string>("title", ""),
                new KeyValuePair<string, string>("pubhouse", ""),
                new KeyValuePair<string, string>("author", ""),
                new KeyValuePair<string, string>("yearleft", ""),
                new KeyValuePair<string, string>("yearright", ""),
                new KeyValuePair<string, string>("isbn", ""),
                new KeyValuePair<string, string>("additparams[]", "3"),
                new KeyValuePair<string, string>("options", ""),
                new KeyValuePair<string, string>("action", "getList"),
                new KeyValuePair<string, string>("page", $"{page}"),
                new KeyValuePair<string, string>("available", "1")
            };

            var content = await HttpClientHelper.PostAsync(client, _apiUrl, new FormUrlEncodedContent(values));
            return string.IsNullOrEmpty(content) ? new SearchResponseData() : JsonConvert.DeserializeObject<SearchResponseData>(content);
        }
    }
}