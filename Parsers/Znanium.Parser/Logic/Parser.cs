using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Web;
using Parser.Core;
using Core.Extensions;
using Core.Providers.Interfaces;
using HtmlAgilityPack;
using NLog;
using TurnerSoftware.SitemapTools;
using TurnerSoftware.SitemapTools.Parser;
using Znanium.Parser.Configs;

namespace Znanium.Parser.Logic {
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

            var getPageBlock = new TransformBlock<Uri, SitemapFile>(async url => await GetLinksSitemaps(client, url));
            getPageBlock.CompleteMessage(_logger, "Обход всех страниц успешно завершен. Ждем получения всех книг.");
            
            var filterBlock = new TransformManyBlock<SitemapFile, long>(async sitemap => Filter(sitemap, await processed));
            var getBookBlock = new TransformBlock<long, Book>(async book => await GetBook(client, book), new ExecutionDataflowBlockOptions {MaxDegreeOfParallelism = _config.MaxThread, EnsureOrdered = false});
            getBookBlock.CompleteMessage(_logger, "Получение всех книг завершено. Ждем сохранения.");
            
            var batchBlock = new BatchBlock<Book>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<Book[]>(async books => await _provider.CreateMany(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранения завершено. Работа программы завершена.");

            getPageBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(getBookBlock);
            getBookBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);

            foreach (var sitemap in (await GetLinksSitemaps(client, new Uri("https://znanium.com/sitemap.xml"))).Sitemaps) {
                await getPageBlock.SendAsync(sitemap.Location);
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
            var content = await client.GetStringWithTriesAsync(new Uri($"https://znanium.com/catalog/document?id={id}"));
            if (string.IsNullOrEmpty(content)) {
                return default;
            }
            
            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            
            var bookContent = doc.DocumentNode.GetByFilterFirst("div", "book-content");
            var bookInfoBlock = bookContent.GetByFilterFirst("div", "desktop-book-header");

            var book = new Book {
                Id = id,
                Name = bookInfoBlock.GetByFilterFirst("h1")?.InnerText.Trim(),
                Bib = doc.GetElementbyId("doc-biblio-card").InnerText.Trim()
            };

            foreach (var div in bookContent.GetByFilter("div", "book-links2")) {
                var name = div.InnerText.Trim();
                var value = div.GetByFilter("a");
                
                if (name.Contains("Издательство")) {
                    book.Publisher = value.FirstOrDefault()?.InnerText;
                } else if (name.Contains("Авторы")) {
                    book.Authors = string.Join(", ", value.Where(t => !string.IsNullOrEmpty(t.InnerText)).Select(t => t?.InnerText?.Trim()));
                }
            }
            
            foreach (var div in bookContent.GetByFilter("div", "book-chars__inner")) {
                var name = div.GetByFilterFirst("div", "book-chars__name")?.InnerText.ToLower().Trim();
                if (string.IsNullOrEmpty(name)) {
                    continue;
                }
                
                var value = div.GetByFilterFirst("div", "book-chars__view")?.InnerText.Trim();

                if (name == "isbn") {
                    book.ISBN = value;
                } else if (name.Contains("страниц")) {
                    int.TryParse(value, out book.Pages);
                } else if (name.Contains("год")) {
                    book.Year = value;
                }
            }

            return book;
        }

        private static IEnumerable<long> Filter(SitemapFile sitemap, ICollection<long> processed) {
            foreach (var uri in sitemap.Urls) {
                var pars = HttpUtility.ParseQueryString(uri.Location.Query);

                var idStr = pars.Get("id");
                if (long.TryParse(idStr, out var id) && !processed.Contains(id)) {
                    yield return id;
                }
            }
        }

        private static async Task<SitemapFile> GetLinksSitemaps(HttpClient client, Uri sitemap) {
            var rootSitemap = await client.GetStringWithTriesAsync(sitemap);

            using var reader = new StringReader(rootSitemap);
            return await new XmlSitemapParser().ParseSitemapAsync(reader);
        }
    }
}