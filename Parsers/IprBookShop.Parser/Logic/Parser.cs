using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Types;
using HtmlAgilityPack;
using IprBookShop.Parser.Configs;
using IprBookShop.Parser.Types;
using Newtonsoft.Json;
using Parser.Core.Configs;
using Parser.Core.Extensions;
using Parser.Core.Logic;

namespace IprBookShop.Parser.Logic {
    public class Parser : ParserBase {
        protected override string ElsName => "IprBookShop";

        public Parser(IParserConfigBase config, IRepository<BookInfo> provider) : base(config, provider) {
        }

        private static readonly Uri _apiUrl = new Uri("http://www.iprbookshop.ru/78575");
        
        private const int BOOKS_PER_PAGE = 20;

        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            var getPageBlock = new TransformBlock<int, SearchResponseData>(async page => await GetSearchResponse(client, page));
            getPageBlock.CompleteMessage(_logger, "Обход всех страниц успешно завершен. Ждем получения всех книг.");
            
            var filterBlock = new TransformManyBlock<SearchResponseData, SearchData>(page => page.Data.Where(t => processed.Add(t.Id.ToString())));
            var getBookBlock = new TransformBlock<SearchData, BookInfo>(async book => await GetBook(client, book.Id), GetParserOptions());
            getBookBlock.CompleteMessage(_logger, "Получение всех книг завершено. Ждем сохранения.");
            
            var batchBlock = new BatchBlock<BookInfo>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<BookInfo[]>(async books => await _provider.CreateMany(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранения завершено. Работа программы завершена.");

            getPageBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(getBookBlock);
            getBookBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);

            var response = await GetSearchResponse(client, 1);
            var pagesCount = response.Count / BOOKS_PER_PAGE + 1;
            
            _logger.Info($"Всего книг в магазине {response.Count}. Страниц для обхода {pagesCount}");
            
            for (var i = ((IParserConfig)_config).StartPage; i <= pagesCount; i++) {
                await getPageBlock.SendAsync(i);
            }

            return new IDataflowBlock[] {getPageBlock, filterBlock, getBookBlock, batchBlock, saveBookBlock};
        }

        private static string Normalize(string str) {
            return string.IsNullOrEmpty(str) ? string.Empty : Regex.Replace(str, @"\s+", " ").Trim();
        }

        /// <summary>
        /// Даже не пытался сделать этот метод понятным
        /// </summary>
        /// <param name="client"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private async Task<BookInfo> GetBook(HttpClient client, long id) {
            var url = new Uri($"http://www.iprbookshop.ru/{id}.html");

            var content = await client.GetStringWithTriesAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var bookInfoBlock = doc.DocumentNode.GetByFilterFirst("div", "book-information");

            var book = new BookInfo(id.ToString(), ElsName) {
                Name = Normalize(bookInfoBlock?.GetByFilterFirst("h4", "header-orange")?.InnerText),
                Bib = Normalize(bookInfoBlock?.GetByFilterFirst("h3", "header-green")?.NextSibling.NextSibling.InnerText)
            };

            var bookDescriptionBlock = bookInfoBlock.GetByFilterFirst("div", "col-sm-10");
            foreach (var row in bookDescriptionBlock.GetByFilter("div", "row")) {
                var strong = row.Descendants().FirstOrDefault(t => t.Name == "strong");
                if (strong == default || string.IsNullOrWhiteSpace(strong.InnerText)) {
                    continue;
                }

                var description = Normalize(GetDescription(row));
                if (strong.InnerText.Contains("Издательство")) {
                    book.Publisher = description;
                } else if (strong.InnerText.Contains("Авторы")) {
                    book.Authors = description;
                } else if (strong.InnerText.Contains("Год издания")) {
                    book.Year = description;
                } else if (strong.InnerText.Contains("ISBN")) {
                    book.ISBN = description;
                }
            }

            return book;
        }

        private static string GetDescription(HtmlNode node) {
            return node.GetByFilterFirst("div", "col-sm-9")?.InnerText.Trim();
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

            var content = await client.PostWithTriesAsync(_apiUrl, new FormUrlEncodedContent(values));
            return string.IsNullOrEmpty(content) ? new SearchResponseData() : JsonConvert.DeserializeObject<SearchResponseData>(content);
        }
    }
}