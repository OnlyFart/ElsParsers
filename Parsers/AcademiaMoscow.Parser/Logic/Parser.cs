using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Types;
using HtmlAgilityPack;
using Parser.Core.Configs;
using Parser.Core.Extensions;
using Parser.Core.Logic;

namespace AcademiaMoscow.Parser.Logic {
    public class Parser : ParserBase {
        private const string CATALOG_URL = "https://academia-moscow.ru/catalogue/4831/";
        
        public Parser(IParserConfigBase config,
            IRepository<BookInfo> provider) : base(config, provider) { }
        
        protected override string ElsName => "AcademiaMoscow";

        private async Task<BookInfo> GetBook(Uri url, HttpClient client) {
            _logger.Info($"Получаем книгу {url}");
            var content = await client.GetStringWithTriesAsync(url);
            if (string.IsNullOrWhiteSpace(content)) {
                return null;
            }
            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            var id = url.Segments.Last().Split("/").First();;
            var detailedDescriptionBlock = doc.DocumentNode.GetByFilterFirst("div", "detailed-description");
            var authorInfoBlock = doc.DocumentNode.GetByFilterFirst("div", "author-book");
            var book = new BookInfo(id, ElsName) {
                Name = doc.DocumentNode.Descendants("h1").FirstOrDefault()?.InnerText,
                Authors = authorInfoBlock.ChildNodes.Where(node => node.Name == "a").Select(node => node.InnerText.Trim()).Where(text => !string.IsNullOrWhiteSpace(text)).StrJoin(", ")
            };
            foreach (var node in detailedDescriptionBlock.ChildNodes) {
                var nameBlock = node.GetByFilterFirst("span", "bold-text");
                if (nameBlock == null) {
                    continue;
                }
                var name = nameBlock.InnerText;
                var value = nameBlock.NextSibling.InnerText.Trim();
                if (name.Contains("ISBN")) {
                    book.ISBN = value;
                } else if (name.Contains("Год")) {
                    book.Year = value;
                } else if (name.Contains("Объем")) {
                    int.TryParse(value, out book.Pages);
                }
            }
            return book;
        }

        private async Task<IEnumerable<Uri>> GetBooksFromPage(HttpClient client, string url) {
            _logger.Info($"Получаем данные для {url}");
            Uri uri = new Uri(url);
            var content = await client.GetStringWithTriesAsync(uri);
            var urls = new List<Uri>();
            
            if (string.IsNullOrEmpty(content)) {
                return urls;
            }
            
            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            foreach (var div in doc.DocumentNode.GetByFilter("div", "title-book")) {
                var link = div.GetByFilterFirst("a");
                var href = link?.Attributes["href"]?.Value;
                var page = uri.ToString()
                    .Replace(uri.PathAndQuery, "") + href;
                urls.Add(new Uri(page));
            }
            return urls;
        }

        private async Task<int> GetMaxPageCount(string url,  HttpClient client) {
            var uri = new Uri(url);
            var content = await client.GetStringWithTriesAsync(uri);
           
            if (string.IsNullOrEmpty(content)) {
                return 1;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            var pageList = doc.DocumentNode.GetByFilterFirst("ul", "pagination");
            if (pageList == null) {
                return 1;
            }

            return pageList.ChildNodes.Select(node => int.TryParse(node.InnerText, out var page) ? page : 1).Max();
        }
    
        private static IEnumerable<Uri> Filter(IEnumerable<Uri> uris, ISet<string> processed) {
            foreach (var uri in uris) {
                var idStr = uri.Segments.Last().Split("/").First();
                if (long.TryParse(idStr, out var id) && processed.Add(id.ToString())) {
                    yield return uri;
                }
            }
        }

        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            var pagesCount = GetMaxPageCount($"{CATALOG_URL}?PAGEN_1=1", client);
            var getPageBlock = new TransformBlock<string, IEnumerable<Uri>>(async url => await GetBooksFromPage(client, url));
            var filterBlock = new TransformManyBlock<IEnumerable<Uri>, Uri>(uris => Filter(uris, processed));
            var getBookBlock = new TransformBlock<Uri, BookInfo>(async url => await GetBook(url, client), GetParserOptions());
            var batchBlock = new BatchBlock<BookInfo>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<BookInfo[]>(async books => {
                    await _provider.CreateMany(books); }
            );
            getBookBlock.CompleteMessage(_logger, "Загрузка всех книг завершено. Ждем сохранения.");
            
            getPageBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(getBookBlock);
            getBookBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);
            
            for (var i = 1; i <= await pagesCount; i++) {
                await getPageBlock.SendAsync($"{CATALOG_URL}?PAGEN_1={i}");
            }

            return new IDataflowBlock[] {getPageBlock, filterBlock, getBookBlock, batchBlock, saveBookBlock};
        }
    }
}