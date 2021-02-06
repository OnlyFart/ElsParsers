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

namespace IBooks.Parser.Logic {
    public class Parser : ParserBase {
        public Parser(IParserConfigBase config, IRepository<BookInfo> provider) : base(config, provider) { }
        
        protected override string ElsName => "IBooks";
        
        private static Uri GetUrl(int page) => new Uri($"https://ibooks.ru/products?page={page}&paging=100");

        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            var pagesCount = await GetMaxPageCount(client, GetUrl(1));
            
            _logger.Info($"Всего страниц {pagesCount}");
            
            var getBooksBlock = new TransformBlock<int, IEnumerable<BookInfo>>(async page => await GetBooks(client, page), GetParserOptions());
            getBooksBlock.CompleteMessage(_logger, "Получение всех книги успешно завершено. Сохранения.");
            
            var filterBlock = new TransformManyBlock<IEnumerable<BookInfo>, BookInfo>(books => Filter(books, processed));

            var batchBlock = new BatchBlock<BookInfo>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<BookInfo[]>(async books => await _provider.CreateMany(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранение завершено.");
            
            getBooksBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);
            
            for (var i = 1; i <= pagesCount; i++) {
                await getBooksBlock.SendAsync(i);
            }

            return new IDataflowBlock[] {getBooksBlock, filterBlock, batchBlock, saveBookBlock};
        }
        
        private static IEnumerable<BookInfo> Filter(IEnumerable<BookInfo> books, ISet<string> processed) {
            return books.Where(book => processed.Add(book.ExternalId));
        }
        
        private static async Task<int> GetMaxPageCount(HttpClient client, Uri uri) {
            var content = await client.GetStringWithTriesAsync(uri);
           
            if (string.IsNullOrEmpty(content)) {
                return 1;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            
            return doc.DocumentNode.GetByFilterFirst("div", "pagination")?.ChildNodes.Select(node => int.TryParse(node.InnerText, out var page) ? page : 1).Max() ?? 1;
        }

        private async Task<IEnumerable<BookInfo>> GetBooks(HttpClient client, int page) {
            var url = GetUrl(page);
            
            _logger.Info($"Загружаем страницу {url}");
            
            var content = await client.GetStringWithTriesAsync(url);
           
            if (string.IsNullOrEmpty(content)) {
                return Enumerable.Empty<BookInfo>();
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var descriptionBlocks = doc.DocumentNode.GetByFilter("div", "product__descr");

            var result = new List<BookInfo>();
            foreach (var block in descriptionBlocks) {
                var a = block.GetByFilterFirst("a", "product__name");

                if (a == null) {
                    continue;
                }

                var externalId = a.Attributes["href"].Value.Split("/", StringSplitOptions.RemoveEmptyEntries).Last();
                var book = new BookInfo(externalId, ElsName) {Name = a.InnerText.Trim()};

                foreach (var article in block.GetByFilter("div", "product__article")) {
                    if (article.InnerText.Contains("ISBN")) {
                        book.ISBN = article.InnerText.Replace("ISBN", string.Empty).Trim();
                        break;
                    }
                }
                
                foreach (var authors in block.GetByFilter("div", "product__authors")) {
                    if (authors.InnerText.Contains("Авторы:")) {
                        book.Authors = authors.InnerText.Replace("Авторы:", string.Empty).Trim();
                        break;
                    }
                }

                foreach (var fullText in block.GetByFilter("a", "product__fulltext")) {
                    book.Bib = fullText.InnerText.Trim();
                    break;
                }

                foreach (var productText in block.GetByFilter("div", "product__text")) {
                    var split = productText.InnerText.Replace("&nbsp;", string.Empty).Split(",", StringSplitOptions.RemoveEmptyEntries);

                    foreach (var elem in split) {
                        if (elem.EndsWith("г.")) {
                            book.Year = elem.Replace("г.", string.Empty).Trim();
                        } else if (elem.EndsWith("с.")) {
                            int.TryParse(elem.Replace("с.", string.Empty), out book.Pages);
                        } else {
                            book.Publisher = elem.Trim();
                        }
                    }
                }
                
                result.Add(book);
            }

            return result;
        }
    }
}