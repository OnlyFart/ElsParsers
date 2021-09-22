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
using ProfSpo.Parser.Types.API;

namespace ProfSpo.Parser.Logic {
    public class Parser : ParserBase {
        public Parser(IParserConfigBase config, IRepository<BookInfo> provider) : base(config, provider) { }
        protected override string ElsName => "ProfSpo";
        
        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            FillHttpClient(client);

            var token = await GetToken(client);
            if (string.IsNullOrWhiteSpace(token)) {
                _logger.Error("Не удалось получить токен");
                return Array.Empty<IDataflowBlock>();
            }

            var response = await GetResponse(client, token, 1);
            
            var getBooksBlock = new TransformBlock<int, IEnumerable<BookInfo>>(async page => await GetBooks(client, token, page), GetParserOptions());
            getBooksBlock.CompleteMessage(_logger, "Загрузка всех книг завершено. Ждем сохранения.");
            
            var filterBlock = new TransformManyBlock<IEnumerable<BookInfo>, BookInfo>(books => Filter(books, processed));
            
            var batchBlock = new BatchBlock<BookInfo>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<BookInfo[]>(async books => await SaveBooks(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранения завершено. Работа программы завершена.");
            
            getBooksBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);
            
            _logger.Info($"Всего страниц для обходы {response.LastPage}");
            for (var i = 1; i <= response.LastPage; i++) {
                await getBooksBlock.SendAsync(i);
            }

            return new IDataflowBlock[] {getBooksBlock, filterBlock, batchBlock, saveBookBlock};
        }

        private static IEnumerable<BookInfo> Filter(IEnumerable<BookInfo> books, ISet<string> processed) {
            return books.Where(book => processed.Add(book.ExternalId));
        }

        /// <summary>
        /// Небольшой костыль. Что бы валидно работали запросы для этого сайта
        /// </summary>
        /// <param name="client"></param>
        private static void FillHttpClient(HttpClient client) {
            client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        }

        private static async Task<Info> GetResponse(HttpClient client, string token, int page) {
            var apiResponse = await client.PostJson<ApiResponse>(new Uri("https://profspo.ru/catalog/search"), GetParams(token, page));
            return apiResponse?.Info ?? new Info();
        }

        private static FormUrlEncodedContent GetParams(string token, int page) {
            var values = new KeyValuePair<string, string>[] {
                new("title", ""),
                new("authors", ""),
                new("publishers", ""),
                new("year1", ""),
                new("year2", ""),
                new("type", ""),
                new("profileCode", ""),
                new("profileId", ""),
                new("discipline_id", ""),
                new("collection", ""),
                new("common", ""),
                new("page", "1"),
                new("mark", ""),
                new("discipline", ""),
                new("_token", token),
                new("page", page.ToString())
            };

            return new FormUrlEncodedContent(values);
        }
        
        private async Task<IEnumerable<BookInfo>> GetBooks(HttpClient client, string token, int page) {
            _logger.Info($"Запрашиваем страницу {page}");

            var result = new List<BookInfo>();
            foreach (var apiBook in (await GetResponse(client, token, page)).Data) {
                var book = new BookInfo(apiBook.Id.ToString(), ElsName) {
                    Authors = apiBook.Authors,
                    Bib = apiBook.Bibliography ?? string.Empty,
                    ISBN = apiBook.ISBN,
                    Name = apiBook.Title,
                    Year = apiBook.Year,
                    Publisher = apiBook.Publishers
                };

                int.TryParse(book.Bib.Split('—')
                    .FirstOrDefault(x => x.Contains(" c."))
                    ?.Trim()
                    .Split(' ')
                    .First(), out book.Pages);

                if (string.IsNullOrWhiteSpace(book.ISBN)
                    && book.Bib.Contains("ISBN", StringComparison.InvariantCultureIgnoreCase)) {
                    book.ISBN = book.Bib.Split("ISBN", StringSplitOptions.RemoveEmptyEntries)[1].Trim()
                        .Split(". ")
                        .First();
                }
                
                result.Add(book);
            }

            return result;
        }

        private static async Task<string> GetToken(HttpClient client) {
            var doc = await client.GetHtmlDoc(new Uri("https://profspo.ru/catalog"));
            var metaBlock = doc?.DocumentNode.GetByFilter("meta").FirstOrDefault(t => t.Attributes["name"]?.Value == "csrf-token");
            return metaBlock?.Attributes["content"].Value;
        }
    }
}
