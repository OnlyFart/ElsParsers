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
            Console.WriteLine($"Получаем книгу {url}");
            var content = await client.GetStringWithTriesAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var id = doc.DocumentNode.SelectSingleNode("//input[@name='id']")?.InnerHtml;
            var detailedDescriptionBlock = doc.DocumentNode.GetByFilterFirst("div", "detailed-description");
            var authorInfoBlock = doc.DocumentNode.GetByFilterFirst("div", "author-book");

            var book = new BookInfo(id, ElsName) {
                ISBN = detailedDescriptionBlock.ChildNodes.FirstOrDefault(x => x.InnerHtml.Contains("ISBN издания:"))
                    ?.InnerText.Split(':')[1]
                    .Trim(),
                Name = doc.DocumentNode.Descendants("h1")
                    .FirstOrDefault()
                    ?.InnerText,
                Year = detailedDescriptionBlock.ChildNodes.FirstOrDefault(x => x.InnerHtml.Contains("Год выпуска:"))
                    ?.InnerText.Split(':')[1]
                    .Trim(),
                Pages = Int32.Parse(detailedDescriptionBlock.ChildNodes.FirstOrDefault(x => x.InnerHtml.Contains("Объем:"))
                    ?.InnerText.Split(':')[1]
                    .Trim() ?? "0")
            };
            var authors = string.Empty;
            foreach (var author in authorInfoBlock.ChildNodes) {
                authors += author.InnerText;
            }

            book.Authors = authors;

            return book;
        }

        private async Task<IEnumerable<Uri>> GetBooksFromPage(HttpClient client, string url) {
            Console.WriteLine($"Получаем данные для {url}");
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

            return int.Parse(pageList.ChildNodes.Last(x => !string.IsNullOrEmpty(x.InnerText) && x.InnerText != "\n" && x.InnerHtml != "\n\t\t")?.InnerText ?? "1");
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