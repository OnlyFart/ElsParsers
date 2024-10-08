using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Web;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Types;
using HtmlAgilityPack;
using Parser.Core.Configs;
using Parser.Core.Extensions;
using Parser.Core.Logic;
using TurnerSoftware.SitemapTools;
using TurnerSoftware.SitemapTools.Parser;

namespace Znanium.Parser.Logic {
    public class Parser : ParserBase {
        protected override string ElsName => "Znanium";

        public Parser(IParserConfigBase config, IRepository<BookInfo> provider) : base(config, provider) {
        }
        
        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            var getPageBlock = new TransformBlock<Uri, SitemapFile>(async url => await GetLinksSitemaps(client, url));
            getPageBlock.CompleteMessage(_logger, "Обход карт сайта успешно завершен. Ждем получения всех книг.");
            
            var filterBlock = new TransformManyBlock<SitemapFile, long>(sitemap => Filter(sitemap, processed));
            var getBookBlock = new TransformBlock<long, BookInfo>(async book => await GetBook(client, book), GetParserOptions());
            getBookBlock.CompleteMessage(_logger, "Получение всех книг завершено. Ждем сохранения.");
            
            var batchBlock = new BatchBlock<BookInfo>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<BookInfo[]>(async books => await SaveBooks(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранения завершено. Работа программы завершена.");

            getPageBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(getBookBlock);
            getBookBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);

            foreach (var sitemap in (await GetLinksSitemaps(client, new Uri("https://znanium.com/sitemap.xml"))).Sitemaps) {
                await getPageBlock.SendAsync(sitemap.Location);
            }

            return [getPageBlock, filterBlock, getBookBlock, batchBlock, saveBookBlock];
        }

        private async Task<BookInfo> GetBook(HttpClient client, long id) {
            var url = new Uri($"https://znanium.com/catalog/document?id={id}");
            var (response, statusCode) = await client.GetStringWithTriesAsync(url);
            
            if (statusCode == HttpStatusCode.NotFound || response == default) {
                _logger.Warn($"Не удалось загрузить книгу {url}. {statusCode}");
                return default;
            }
            
            _logger.Debug($"Получена книга {url}");
            
            var book = new BookInfo(id.ToString(), ElsName);
            var doc = new HtmlDocument();
            doc.LoadHtml(response);

            var bookContent = doc.DocumentNode.GetByFilterFirst("div", "book-single__top-content-right");
            book.Name = doc.DocumentNode.GetByFilterFirst("h1", "book-single__title")?.InnerText.Trim();
            book.Bib = doc.DocumentNode.GetByFilter("div", "tab-panel").FirstOrDefault(t => t.GetAttributeValue("nav", string.Empty) == "bib")?.InnerText?.Trim();

            foreach (var div in bookContent.GetByFilter("div", "book-link")) {
                var name = div.InnerText.Trim();
                var value = div.GetByFilter("a");

                if (name.Contains("Издательство")) {
                    book.Publisher = value.FirstOrDefault()?.InnerText;
                } else if (name.Contains("Авторы")) {
                    book.Authors = value.Where(t => !string.IsNullOrEmpty(t.InnerText)).Select(t => t?.InnerText?.Trim()).StrJoin(", ");
                }
            }

            foreach (var div in bookContent.GetByFilter("div", "book-link")) {
                var split = div.InnerText.Split(":");
                if (split.Length != 2) {
                    continue;
                }

                var name = split[0].ToLower().Trim();
                var value = split[1].Trim();

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

        private static IEnumerable<long> Filter(SitemapFile sitemap, ISet<string> processed) {
            foreach (var uri in sitemap.Urls) {
                var idStr = HttpUtility.ParseQueryString(uri.Location.Query).Get("id");
                if (long.TryParse(idStr, out var id) && processed.Add(id.ToString())) {
                    yield return id;
                }
            }
        }

        private static async Task<SitemapFile> GetLinksSitemaps(HttpClient client, Uri sitemap) {
            var rootSitemap = await client.GetStringWithTriesAsync(sitemap);

            using var reader = new StringReader(rootSitemap.Response);
            return await new XmlSitemapParser().ParseSitemapAsync(reader);
        }
    }
}