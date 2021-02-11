﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Types;
using Parser.Core.Configs;
using Parser.Core.Extensions;
using Parser.Core.Logic;

namespace StudentLibrary.Parser.Logic {
    public class Parser : ParserBase {
        public Parser(IParserConfigBase config,
            IRepository<BookInfo> provider) : base(config, provider) { }
        
        protected override string ElsName => "StudentLibrary";
        
        private static Uri GetUrl(int page) => new Uri($"https://www.studentlibrary.ru/ru/catalogue/switch_kit/x-total/-esf2k2z11-year-dec-page-{page}.html");
        private static HttpRequestMessage GetMessageWithCookies(Uri uri, string cookies) {
            var message = new HttpRequestMessage(HttpMethod.Get, uri);
            message.Headers.Add("Cookie", cookies);

            return message;
        }

        private async Task<BookInfo> GetBook(HttpClient client,
            Uri uri, string cookies) {
            _logger.Info($"Получаем книгу {uri}");
            using var message = GetMessageWithCookies(uri, cookies);
            var doc = await client.GetHtmlDoc(message);

            if (doc == default) {
                return default;
            }

            var id = uri.Segments.Last().Split(".")[0];
            var detailedDescriptionBlock = doc.DocumentNode.GetByFilterFirst("div", "reader-info");
            var book = new BookInfo(id, ElsName) {
                Name = doc.DocumentNode.GetByFilterFirst("h2")
                    ?.InnerText,
                Bib = doc.DocumentNode.GetByFilterFirst("div", "wrap-annotation-sticker")?.GetByFilterFirst("span", "value")?.InnerText
            };

            foreach (var node in detailedDescriptionBlock.ChildNodes) {
                var nameBlock = node.GetByFilterFirst("span", "head");

                if (nameBlock == null) {
                    continue;
                }

                var name = nameBlock.InnerText;
                var value = nameBlock.NextSibling.InnerText.Trim();
               
                if (name.Contains("Авторы")) {
                    book.Authors = value;
                }
                else if (name.Contains("Для каталога")) {
                    int.TryParse(value.Split('-')
                        .FirstOrDefault(x => x.Contains(" с."))
                        ?.Trim()
                        .Split(' ')
                        .First(), out book.Pages);
                    if(value.Contains("ISBN")) {
                        book.ISBN = value.Split(new []{ "ISBN" }, StringSplitOptions.None)[1].Trim()
                            .Split(new []{ ". " }, StringSplitOptions.None)
                            .First();
                    }
                } else if (name.Contains("Издательство")) {
                    book.Publisher = value;
                }
                else if (name.Contains("Год издания")) {
                    book.Year = value;
                } 
            }

            return book;
        }

        private static async Task<IEnumerable<Uri>> GetBookLinks(HttpClient client,
            Uri uri, string cookies) {
            _logger.Info($"Получаем данные для {uri}");
            
            using var message = GetMessageWithCookies(uri, cookies);
            var doc = await client.GetHtmlDoc(message);
            
            return doc == default
                ? Enumerable.Empty<Uri>()
                : doc.DocumentNode.GetByFilter("div", "wrap-title-book-sengine")
                    .Select(div => div.GetByFilterFirst("a")
                        ?.Attributes["href"]
                        ?.Value)
                    .Select(href => new Uri(uri, href));
        }

        private static async Task<int> GetMaxPageCount(HttpClient client,
            Uri uri, string cookies) {

            using var message = GetMessageWithCookies(uri, cookies);
            var doc = await client.GetHtmlDoc(message);

            return doc == default
                ? 1
                : doc.DocumentNode.GetByFilterFirst("ul", "pagination-ros-num va-m")
                    ?.ChildNodes.Select(node => int.TryParse(node.InnerText, out var page)
                        ? page
                        : 1)
                    .Max() ?? 1;
        }

        private static IEnumerable<Uri> Filter(IEnumerable<Uri> uris,
            ISet<string> processed) {
            foreach (var uri in uris) {
                var idStr = uri.Segments.Last()
                    .Split(".")[0];

                if (processed.Add(idStr)) {
                    yield return uri;
                }
            }
        }
        
        private static KeyValuePair<string, string> TryParseCookies(string cookie) {
            if (cookie.Contains("rdsssr")) {
                return new KeyValuePair<string, string>("rdsssr", cookie.Split(new[] {
                        "; "
                    }, StringSplitOptions.None)
                    .First()
                    .Split("rdsssr=")
                    .Last()); 
            }

            if (cookie.Contains("rdsbwid")) {
                return new KeyValuePair<string,string>("rdsbwid", cookie.Split(new[] {
                        "; "
                    }, StringSplitOptions.None)
                    .First()
                    .Split("rdsbwid=")
                    .Last());
            }

            return new KeyValuePair<string, string>(cookie, cookie);
        }
        
        private static async Task<string> PrepareCookies(HttpClient client) {
            _logger.Info("Пытаемся получить куки и установить размер пачки на странице в 100 книг");
            var response = await client.GetAsync("https://www.studentlibrary.ru/cgi-bin/mb4x");
            var content = await response.Content.ReadAsStringAsync();
            IDictionary<string, string> cookies = new Dictionary<string, string>();
            var chfl = content.Split("CHFL")
                .Last()
                .Split("\"")
                .FirstOrDefault(x => x.Contains("._ux"));
            
            cookies.Add("_gid", "GA1.1.1111111111.1111111111");
            cookies.Add("_ga", "GA1.1.1111111111.1111111111");
            foreach (var c in response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value) {
                cookies.Add(TryParseCookies(c));
            }

            if (!cookies.Any()) {
                return string.Empty;
            }

            var cookie = $"rdsssr={cookies["rdsssr"]}; rdsbwid={cookies["rdsbwid"]}; _gid={cookies["_gid"]}; _ga={cookies["_ga"]}";
            
            var url = "https://www.studentlibrary.ru/cgi-bin/mb4x?SSr=" + cookies["rdsssr"] + 
                "&usr_data=FirstPg(sengine,list{null},)&clientWidth=693&scrollTop=0&search_only=" + 
                "everywhere&GoToPg=1&_l4=100&_l4vars=100&Page_Cur=0&BODYZONE=default&trg_page_type=sengine&trg_page_id=list{null}" + 
                "&_l1=ru&_l2=ru&_l3=ru&thispg_type=sengine&thispg_id=list{null}&CHFL=" 
                + chfl + "&CLPg0=0&CLPg1=0&CITM=-1%27";


            using var message = GetMessageWithCookies(new Uri(url), cookie);
            await client.SendAsync(message);
            return cookie;
        }

        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client,
            ISet<string> processed) {
            var cookies = await PrepareCookies(client);

            if (string.IsNullOrEmpty(cookies)) {
                _logger.Info("Не смогли получить cookies.");
            }

            var pagesCount = GetMaxPageCount(client, GetUrl(1), cookies);

            var getPageBlock = new TransformBlock<Uri, IEnumerable<Uri>>(async url => await GetBookLinks(client, url, cookies));
            getPageBlock.CompleteMessage(_logger, "Получение всех ссылок на книги успешно завершено. Ждем загрузки всех книг.");

            var filterBlock = new TransformManyBlock<IEnumerable<Uri>, Uri>(uris => Filter(uris, processed));

            var getBookBlock = new TransformBlock<Uri, BookInfo>(async url => await GetBook(client, url, cookies), GetParserOptions());
            getBookBlock.CompleteMessage(_logger, "Загрузка всех книг завершено. Ждем сохранения.");

            var batchBlock = new BatchBlock<BookInfo>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<BookInfo[]>(async books => await SaveBooks(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранение завершено.");

            getPageBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(getBookBlock);
            getBookBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);

            for (var i = 1; i <= await pagesCount; i++) {
                await getPageBlock.SendAsync(GetUrl(i));
            }

            return new IDataflowBlock[] {
                getPageBlock,
                filterBlock,
                getBookBlock,
                batchBlock,
                saveBookBlock
            };
        }
    }
}

