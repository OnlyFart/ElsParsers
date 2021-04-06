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
using Parser.Core.Configs;
using Parser.Core.Extensions;
using Parser.Core.Logic;

namespace IprBookShop.Parser.Logic {
    public class Parser : ParserBase {
        protected override string ElsName => "IprBookShop";

        public Parser(IParserConfigBase config, IRepository<BookInfo> provider) : base(config, provider) {
        }

        private static readonly Uri _apiUrl = new("https://www.iprbookshop.ru/78575");
        
        private const int BOOKS_PER_PAGE = 20;

        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            var getPageBlock = new TransformBlock<int, SearchResponseData>(async page => await GetSearchResponse(client, page));
            getPageBlock.CompleteMessage(_logger, "Обход всех страниц успешно завершен. Ждем получения всех книг.");
            
            var filterBlock = new TransformManyBlock<SearchResponseData, SearchData>(page => page.Data.Where(t => processed.Add(t.Id.ToString())));
            var getBookBlock = new TransformBlock<SearchData, BookInfo>(async book => await GetBook(client, book.Id), GetParserOptions());
            getBookBlock.CompleteMessage(_logger, "Получение всех книг завершено. Ждем сохранения.");
            
            var batchBlock = new BatchBlock<BookInfo>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<BookInfo[]>(async books => await SaveBooks(books));
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

        private async Task<BookInfo> GetBook(HttpClient client, long id) {
            var doc = await client.GetHtmlDoc(new Uri($"https://www.iprbookshop.ru/{id}.html"));
            if (doc == default) {
                return default;
            }

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

            var values = new KeyValuePair<string, string>[] {
                new("profile_id", ""),
                new("ugs", ""),
                new("subpubhouse", ""),
                new("title", ""),
                new("pubhouse", ""),
                new("author", ""),
                new("yearleft", ""),
                new("yearright", ""),
                new("isbn", ""),
                new("additparams[]", "3"),
                new("options", ""),
                new("action", "getList"),
                new("page", $"{page}"),
                new("available", "1")
            };

            return await client.PostJson<SearchResponseData>(_apiUrl, new FormUrlEncodedContent(values)) ?? new SearchResponseData();
        }
    }
}