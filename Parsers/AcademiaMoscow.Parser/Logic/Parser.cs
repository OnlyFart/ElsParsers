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

namespace AcademiaMoscow.Parser.Logic {
    public class Parser : ParserBase {
        public Parser(IParserConfigBase config, IRepository<BookInfo> provider) : base(config, provider) { }
        
        private static Uri GetUrl(int page) => new Uri($"https://academia-moscow.ru/catalogue/4831/?PAGEN_1={page}");
        
        protected override string ElsName => "AcademiaMoscow";

        private async Task<BookInfo> GetBook(HttpClient client, Uri uri) {
            _logger.Info($"Получаем книгу {uri}");
            var doc = await client.GetHtmlDoc(uri);
            if (doc == default) {
                return default;
            }

            var detailedDescriptionBlock = doc.DocumentNode.GetByFilterFirst("div", "detailed-description");
            var authorInfoBlock = doc.DocumentNode.GetByFilterFirst("div", "author-book");
            
            var book = new BookInfo(uri.Segments.Last().Split("/").First(), ElsName) {
                Name = doc.DocumentNode.GetByFilterFirst("h1")?.InnerText,
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

        private static async Task<IEnumerable<Uri>> GetBookLinks(HttpClient client, Uri uri) {
            _logger.Info($"Получаем данные для {uri}");

            var doc = await client.GetHtmlDoc(uri);
            return doc == default ? 
                Enumerable.Empty<Uri>() : 
                doc.DocumentNode.GetByFilter("div", "title-book").Select(div => div.GetByFilterFirst("a")?.Attributes["href"]?.Value).Select(href => new Uri(uri, href));
        }

        private static async Task<int> GetMaxPageCount(HttpClient client, Uri uri) {
            var doc = await client.GetHtmlDoc(uri);
            return doc == default ? 
                1 : 
                doc.DocumentNode.GetByFilterFirst("ul", "pagination")?.ChildNodes.Select(node => int.TryParse(node.InnerText, out var page) ? page : 1).Max() ?? 1;
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
            var pagesCount = GetMaxPageCount(client, GetUrl(1));
            
            var getPageBlock = new TransformBlock<Uri, IEnumerable<Uri>>(async url => await GetBookLinks(client, url));
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
            
            for (var i = 1; i <= await pagesCount; i++) {
                await getPageBlock.SendAsync(GetUrl(i));
            }

            return new IDataflowBlock[] {getPageBlock, filterBlock, getBookBlock, batchBlock, saveBookBlock};
        }
    }
}