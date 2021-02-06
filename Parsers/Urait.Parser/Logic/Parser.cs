using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Types;
using HtmlAgilityPack;
using Parser.Core.Configs;
using Parser.Core.Extensions;
using Parser.Core.Logic;
using TurnerSoftware.SitemapTools.Parser;

namespace Urait.Parser.Logic {
    public class Parser : ParserBase {
        protected override string ElsName => "Urait";
        
        public Parser(IParserConfigBase config, IRepository<BookInfo> provider) : base(config, provider) {

        }
        
        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            var filterBlock = new TransformManyBlock<IEnumerable<Uri>, Uri>(uris => Filter(uris, processed));
            filterBlock.CompleteMessage(_logger, "Получение всех ссылок на книги успешно завершено. Ждем загрузки всех книг.");
            
            var getBookBlock = new TransformBlock<Uri, BookInfo>(async book => await GetBook(client, book), GetParserOptions());
            getBookBlock.CompleteMessage(_logger, "Загрузка всех книг завершено. Ждем сохранения.");
            
            var batchBlock = new BatchBlock<BookInfo>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<BookInfo[]>(async books => await SaveBooks(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранения завершено. Работа программы завершена.");
            
            filterBlock.LinkTo(getBookBlock);
            getBookBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);
            
            await filterBlock.SendAsync(await GetLinksSitemaps(client, new Uri("https://urait.ru/sitemap.xml")));
            
            return new IDataflowBlock[] {filterBlock, getBookBlock, batchBlock, saveBookBlock};
        }

        private async Task<BookInfo> GetBook(HttpClient client, Uri uri) {
            var content = await client.GetStringWithTriesAsync(uri);
            if (string.IsNullOrEmpty(content)) {
                return default;
            }
            
            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var book = new BookInfo(uri.Segments.Last().Split("-").Last(), ElsName) {
                Name = doc.DocumentNode.GetByFilterFirst("h1", "book_title")?.InnerText.Trim(), 
                Authors = doc.DocumentNode.GetByFilterFirst("ul", "creation-info__authors")?.FirstChild?.InnerText?.Trim(), 
                Year = doc.DocumentNode.GetByFilterFirst("div", "creation-info__year")?.InnerText?.Trim(),
            };
            
            foreach (var div in doc.DocumentNode.GetByFilter("div", "book-about-produce__item")) {
                var name = div.GetByFilterFirst("span", "book-about-produce__title")?.InnerText.ToLower().Trim();
                if (string.IsNullOrEmpty(name)) {
                    continue;
                }
                
                var value = div.GetByFilter("span", "book-about-produce__info").Select(t => t.InnerText.Trim()).StrJoin(", ");

                if (name == "isbn") {
                    book.ISBN = value;
                } else if (name.Contains("страниц")) {
                    int.TryParse(value, out book.Pages);
                }
            }
            
            foreach (var div in doc.DocumentNode.GetByFilter("div", "book-about-info__item")) {
                var name = div.GetByFilterFirst("div", "book-about-info__title")?.InnerText.ToLower().Trim();
                var value = div.GetByFilter("div", "book-about-info__info").Select(t => t.InnerText.Trim()).StrJoin(", ");
                
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

        private static IEnumerable<Uri> Filter(IEnumerable<Uri> uris, ISet<string> processed) {
            foreach (var uri in uris.Where(uri => uri.LocalPath.StartsWith("/book/"))) {
                var idStr = uri.Segments.Last().Split("-").Last();
                if (long.TryParse(idStr, out var id) && processed.Add(id.ToString())) {
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