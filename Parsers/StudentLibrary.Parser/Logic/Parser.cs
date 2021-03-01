using System;
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
        public Parser(IParserConfigBase config, IRepository<BookInfo> provider) : base(config, provider) { }
        
        protected override string ElsName => "StudentLibrary";
        
        private static Uri GetUrl(int page) => new($"https://www.studentlibrary.ru/ru/catalogue/switch_kit/x-total/-esf2k2z11-year-dec-page-{page}.html");

        private async Task<BookInfo> GetBook(HttpClient client, Uri uri) {
            _logger.Info($"Получаем книгу {uri}");
           
            var doc = await client.GetHtmlDoc(uri);

            if (doc == default) {
                return default;
            }

            var id = uri.Segments.Last().Split(".")[0];
            var detailedDescriptionBlock = doc.DocumentNode.GetByFilterFirst("div", "reader-info");
            var book = new BookInfo(id, ElsName) {
                Name = doc.DocumentNode.GetByFilterFirst("h2")?.InnerText
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
                } else if (name.Contains("Для каталога")) {
                    book.Bib = value;
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
                } else if (name.Contains("Год издания")) {
                    book.Year = value;
                } 
            }

            return book;
        }

        private static async Task<IEnumerable<Uri>> GetBookLinks(HttpClient client, Uri uri) {
            _logger.Info($"Получаем данные для {uri}");
            
            var doc = await client.GetHtmlDoc(uri);
            
            return doc == default
                ? Enumerable.Empty<Uri>()
                : doc.DocumentNode.GetByFilter("div", "wrap-title-book-sengine")
                    .Select(div => div.GetByFilterFirst("a")
                        ?.Attributes["href"]
                        ?.Value)
                    .Select(href => new Uri(uri, href));
        }

        private static async Task<int> GetMaxPageCount(HttpClient client, Uri uri) {
            var doc = await client.GetHtmlDoc(uri);

            return doc == default
                ? 1
                : doc.DocumentNode.GetByFilterFirst("ul", "pagination-ros-num va-m")
                    ?.ChildNodes.Select(node => int.TryParse(node.InnerText, out var page) ? page : 1)
                    .Max() ?? 1;
        }

        private static IEnumerable<Uri> Filter(IEnumerable<Uri> uris, ISet<string> processed) {
            foreach (var uri in uris) {
                var idStr = uri.Segments.Last().Split(".")[0];

                if (processed.Add(idStr)) {
                    yield return uri;
                }
            }
        }

        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            var pagesCount = await GetMaxPageCount(client, GetUrl(1));

            var getPageBlock = new TransformBlock<Uri, IEnumerable<Uri>>(async url => await GetBookLinks(client, url), GetParserOptions());
            getPageBlock.CompleteMessage(_logger, "Получение всех ссылок на книги успешно завершено. Ждем загрузки всех книг.");

            var filterBlock = new TransformManyBlock<IEnumerable<Uri>, Uri>(uris => Filter(uris, processed));

            var getBookBlock = new TransformBlock<Uri, BookInfo>(async url => await GetBook(client, url), GetParserOptions());
            getBookBlock.CompleteMessage(_logger, "Загрузка всех книг завершено. Ждем сохранения.");

            var batchBlock = new BatchBlock<BookInfo>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<BookInfo[]>(async books => await SaveBooks(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранение завершено.");

            getPageBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(getBookBlock);
            getBookBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);

            _logger.Info($"Всего страниц для обходы {pagesCount}");
            for (var i = 1; i <= pagesCount; i++) {
                await getPageBlock.SendAsync(GetUrl(i));
            }

            return new IDataflowBlock[] {getPageBlock, filterBlock, getBookBlock, batchBlock, saveBookBlock };
        }
    }
}

