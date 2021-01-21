using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Parser.Core;
using Core.Extensions;
using Core.Providers.Interfaces;
using HtmlAgilityPack;
using NLog;
using TurnerSoftware.SitemapTools.Parser;
using Urait.Parser.Configs;

namespace Urait.Parser.Logic {
    public class Parser {
        private static readonly Logger _logger = LogManager.GetLogger(nameof(Parser));
        
        private readonly IParserConfig _config;
        private readonly IRepository<Book> _provider;

        public Parser(IParserConfig config, IRepository<Book> provider) {
            _config = config;
            _provider = provider;
        }
        
        public async Task Parse() {
            var client = HttpClientExtensions.GetClient(_config);

            var processed = _provider.ReadProjection(t => t.Id).ContinueWith(t => new HashSet<long>(t.Result));
            
            var filterBlock = new TransformManyBlock<IEnumerable<Uri>, Uri>(async uris => Filter(uris, await processed));
            filterBlock.CompleteMessage(_logger, "Обход всех страниц успешно завершен. Ждем получения всех книг.");
            
            var getBookBlock = new TransformBlock<Uri, Book>(async book => await GetBook(client, book), new ExecutionDataflowBlockOptions {MaxDegreeOfParallelism = _config.MaxThread, EnsureOrdered = false});
            getBookBlock.CompleteMessage(_logger, "Получение всех книг завершено. Ждем сохранения.");
            
            var batchBlock = new BatchBlock<Book>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<Book[]>(async books => await _provider.CreateMany(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранения завершено. Работа программы завершена.");
            
            filterBlock.LinkTo(getBookBlock);
            getBookBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);
            
            await filterBlock.SendAsync(await GetLinksSitemaps(client, new Uri("https://urait.ru/sitemap.xml")));
            
            await DataflowExtension.WaitBlocks(filterBlock, getBookBlock, batchBlock, saveBookBlock);
        }

        /// <summary>
        /// Даже не пытался сделать этот метод понятным
        /// </summary>
        /// <param name="client"></param>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static async Task<Book> GetBook(HttpClient client, Uri uri) {
            var content = await client.GetStringWithTriesAsync(uri);
            if (string.IsNullOrEmpty(content)) {
                return default;
            }
            
            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var book = new Book {
                Id = int.Parse(uri.Segments.Last().Split("-").Last()), 
                Name = doc.DocumentNode.GetByFilterFirst("h1", "book_title")?.InnerText.Trim(), 
                Authors = doc.DocumentNode.GetByFilterFirst("ul", "creation-info__authors")?.FirstChild?.InnerText?.Trim(), 
                Year = doc.DocumentNode.GetByFilterFirst("div", "creation-info__year")?.InnerText?.Trim(),
            };


            foreach (var div in doc.DocumentNode.GetByFilter("div", "book-about-produce__item")) {
                var name = div.GetByFilterFirst("span", "book-about-produce__title")?.InnerText.ToLower().Trim();
                if (string.IsNullOrEmpty(name)) {
                    continue;
                }
                
                var value = string.Join(", ", div.GetByFilter("span", "book-about-produce__info").Select(t => t.InnerText.Trim()));

                if (name == "isbn") {
                    book.ISBN = value;
                } else if (name.Contains("страниц")) {
                    int.TryParse(value, out book.Pages);
                }
            }
            
            foreach (var div in doc.DocumentNode.GetByFilter("div", "book-about-info__item")) {
                var name = div.GetByFilterFirst("div", "book-about-info__title")?.InnerText.ToLower().Trim();
                var value = string.Join(", ", div.GetByFilter("div", "book-about-info__info").Select(t => t.InnerText.Trim()));
                
                if (!string.IsNullOrEmpty(name) && name.Contains("библиографическое описание")) {
                    book.Bib = value;
                }
            }

            if (!string.IsNullOrEmpty(book.Bib)) {
                var publisher = Regex.Match(book.Bib, "Издательство (.*?),");
                if (publisher.Success) {
                    book.Bib = publisher.Groups[1].Value;
                }
            }

            return book;
        }

        private static IEnumerable<Uri> Filter(IEnumerable<Uri> uris, ICollection<long> processed) {
            foreach (var uri in uris.Where(uri => uri.LocalPath.StartsWith("/book/"))) {
                var idStr = uri.Segments.Last().Split("-").Last();
                if (long.TryParse(idStr, out var id) && !processed.Contains(id)) {
                    yield return uri;
                }
            }
        }

        private static async Task<IEnumerable<Uri>> GetLinksSitemaps(HttpClient client, Uri sitemap) {
            var rootSitemap = await client.GetStringWithTriesAsync(sitemap);

            using var reader = new StringReader(rootSitemap);
            var sm = await new XmlSitemapParser().ParseSitemapAsync(reader);
            return sm.Urls.Select(t => t.Location);
        }
    }
}