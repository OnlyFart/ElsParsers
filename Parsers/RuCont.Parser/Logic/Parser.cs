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
using Parser.Core.Configs;
using Parser.Core.Extensions;
using Parser.Core.Logic;
using RuCont.Parser.Types.API;
using TurnerSoftware.SitemapTools;
using TurnerSoftware.SitemapTools.Parser;

namespace RuCont.Parser.Logic {
    public class Parser : ParserBase {
        public Parser(IParserConfigBase config, IRepository<BookInfo> provider) : base(config, provider) { }
        
        protected override string ElsName => "RuCont";
        
        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            var getPageBlock = new TransformBlock<Uri, SitemapFile>(async url => await GetLinksSitemaps(client, url));
            getPageBlock.CompleteMessage(_logger, "Обход карт сайта успешно завершен. Ждем получения всех книг.");
            
            var filterBlock = new TransformManyBlock<SitemapFile, string>(sitemap => Filter(sitemap, processed));
            var getBookBlock = new TransformBlock<string, BookInfo>(async book => await GetBook(client, book), GetParserOptions());
            getBookBlock.CompleteMessage(_logger, "Получение всех книг завершено. Ждем сохранения.");
            
            var batchBlock = new BatchBlock<BookInfo>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<BookInfo[]>(async books => await SaveBooks(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранения завершено. Работа программы завершена.");

            getPageBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(getBookBlock);
            getBookBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);

            foreach (var sitemap in (await GetLinksSitemaps(client, new Uri("https://rucont.ru/sitemaps/sitemap.xml"))).Sitemaps) {
                await getPageBlock.SendAsync(sitemap.Location);
            }

            return new IDataflowBlock[] {getPageBlock, filterBlock, getBookBlock, batchBlock, saveBookBlock};
        }
        
        private static IEnumerable<string> Filter(SitemapFile sitemap, ISet<string> processed) {
            foreach (var uri in sitemap.Urls.Where(u => u.Location.LocalPath.StartsWith("/efd/"))) {
                var idStr = uri.Location.Segments.Last();
                if (long.TryParse(idStr, out var id) && processed.Add(id.ToString())) {
                    yield return id.ToString();
                }
            }
        }

        private static async Task<SitemapFile> GetLinksSitemaps(HttpClient client, Uri sitemap) {
            var rootSitemap = await client.GetStringWithTriesAsync(sitemap);

            using var reader = new StringReader(rootSitemap);
            return await new XmlSitemapParser().ParseSitemapAsync(reader);
        }


        private async Task<BookInfo> GetBook(HttpClient client, string id) {
            var book = await client.GetJson<FullInfo>(new Uri($"https://lib.rucont.ru/api/efd/{id}/full_info"));
            // Не смогли получить книгу, либо это вообще не книга
            if (book == default || book.Type != 1) {
                return default;
            }

            var authors = new List<string>();
            foreach (var author in book.Authors ?? new string[]{ }) {
                var cleanAuthor = Regex.Replace(author, @"\((.*?)\)", string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(book.Bib)) {
                    try {
                        var rgxPattern = $@"(\w\.\w\.)?\W{cleanAuthor}\W(\w\.\w\.)?";
                        var initials = Regex.Match(book.Bib, rgxPattern);
                        if (initials.Success) {
                            var first = initials.Groups[1].Value;
                            var second = initials.Groups[2].Value;

                            cleanAuthor += $" {(string.IsNullOrWhiteSpace(first) ? second : first).Trim()}";
                        }
                    } catch {
                        // ignored
                    }
                }

                authors.Add(cleanAuthor);
            }

            return new BookInfo(id, ElsName) {
                Bib = book.Bib,
                ISBN = book.ISBN,
                Year = book.Year,
                Name = book.Title,
                Publisher = book.Publisher,
                Pages = book.Pages ?? 0,
                Authors = authors.StrJoin(", ")
            };
        }
    }
}
