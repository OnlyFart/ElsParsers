using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Web;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Utils.Helpers;
using HtmlAgilityPack;
using NLog;
using TurnerSoftware.SitemapTools;
using TurnerSoftware.SitemapTools.Parser;
using ZnaniumParser.Configs;
using ZnaniumParser.Types;

namespace ZnaniumParser.Logic {
    public class Parser {
        private static readonly Logger _logger = LogManager.GetLogger(nameof(Parser));
        
        private readonly IParserConfig _config;
        private readonly IBooksProvider<Book> _provider;

        public Parser(IParserConfig config, IBooksProvider<Book> provider) {
            _config = config;
            _provider = provider;
        }
        
        public async Task Parse() {
            var client = HttpClientHelper.GetClient(_config);
            
            var processed = _provider.GetProcessed().ContinueWith(t => new HashSet<long>(t.Result));

            var getPageBlock = new TransformBlock<Uri, SitemapFile>(async url => await GetLinksSitemaps(client, url));
            CompleteMessage(getPageBlock, "Обход всех страниц успешно завершен. Ждем получения всех книг.");
            
            var filterBlock = new TransformManyBlock<SitemapFile, long>(async sitemap => Filter(sitemap, await processed));
            var getBookBlock = new TransformBlock<long, Book>(async book => await GetBook(client, book), new ExecutionDataflowBlockOptions {MaxDegreeOfParallelism = _config.MaxThread, EnsureOrdered = false});
            CompleteMessage(getBookBlock, "Получение всех книг завершено. Ждем сохранения.");
            
            var batchBlock = new BatchBlock<Book>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<Book[]>(async books => await _provider.Save(books));
            CompleteMessage(saveBookBlock, "Сохранения завершено. Работа программы завершена.");

            getPageBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(getBookBlock);
            getBookBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);

            foreach (var sitemap in (await GetLinksSitemaps(client, new Uri("https://znanium.com/sitemap.xml"))).Sitemaps) {
                await getPageBlock.SendAsync(sitemap.Location);
            }

            await DataflowExtension.WaitBlocks(getPageBlock, filterBlock, getBookBlock, batchBlock, saveBookBlock);
        }
        
        private static void CompleteMessage(IDataflowBlock block, string message) {
            block.Completion.ContinueWith(task => _logger.Info(message)).GetAwaiter();
        }

        /// <summary>
        /// Даже не пытался сделать этот метод понятным
        /// </summary>
        /// <param name="client"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private static async Task<Book> GetBook(HttpClient client, long id) {
            var content = await HttpClientHelper.GetStringAsync(client, new Uri($"https://znanium.com/catalog/document?id={id}"));
            if (string.IsNullOrEmpty(content)) {
                return null;
            }
            
            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            
            var bookContent = doc.DocumentNode.GetByFilter("div", "book-content").FirstOrDefault();
            var bookInfoBlock = bookContent.GetByFilter("div", "desktop-book-header").FirstOrDefault();

            var book = new Book {
                Id = id,
                Name = bookInfoBlock.GetByFilter("h1").FirstOrDefault().InnerText.Trim(),
                Bib = doc.GetElementbyId("doc-biblio-card").InnerText.Trim()
            };

            foreach (var div in bookContent.GetByFilter("div", "book-links2")) {
                var name = div.InnerText.Trim();
                var value = div.GetByFilter("a")?.FirstOrDefault()?.InnerText;
                
                if (name.Contains("Издательство")) {
                    book.Publisher = value;
                } else if (name.Contains("Авторы")) {
                    book.Authors = value;
                }
            }
            
            foreach (var div in bookContent.GetByFilter("div", "book-chars__inner")) {
                var name = div.GetByFilter("div", "book-chars__name").FirstOrDefault().InnerText.ToLower().Trim();
                if (string.IsNullOrEmpty(name)) {
                    continue;
                }
                
                var value = div.GetByFilter("div", "book-chars__view").FirstOrDefault().InnerText.Trim();

                if (name == "isbn") {
                    book.ISBN = value;
                } else if (name.Contains("страниц")) {
                    int.TryParse(value, out book.Pages);
                } else if (name.Contains("год")) {
                    int.TryParse(value, out book.Year);
                } else if (name.Contains("артикул")) {
                    book.Article = value;
                } else if (name.Contains("isbn-онлайн")) {
                    book.IsbnOnline = value;
                } else if (name.Contains("doi")) {
                    book.DOI = value;
                }else {
                    _logger.Warn($"Появилось новое поле {name}, которое не сохраняется в базу");
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
            var rootSitemap = await HttpClientHelper.GetStringAsync(client, sitemap);

            using var reader = new StringReader(rootSitemap);
            return await new XmlSitemapParser().ParseSitemapAsync(reader);
        }
    }
}