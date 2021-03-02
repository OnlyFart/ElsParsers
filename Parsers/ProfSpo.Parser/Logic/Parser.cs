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

            var response = await GetSearchResponse(client, token, 1);
            
            var getBooksBlock = new TransformBlock<int, Info>(async page => await GetSearchResponse(client, token, page), GetParserOptions());
            getBooksBlock.CompleteMessage(_logger, "Загрузка всех книг завершено. Ждем сохранения.");
            
            var filterBlock = new TransformManyBlock<Info, BookInfo>(info => Filter(info, processed));
            
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

        public IEnumerable<BookInfo> Filter(Info info, ISet<string> processed) {
            foreach (var book in info.Data) {
                if (processed.Add(book.Id.ToString())) {
                    var result = new BookInfo(book.Id.ToString(), ElsName) {
                        Authors = book.Authors,
                        Bib = book.Bibliography,
                        ISBN = book.ISBN,
                        Name = book.Title,
                        Year = book.Year,
                        Publisher = book.Publishers
                    };

                    int.TryParse(result.Bib.Split('—')
                        .FirstOrDefault(x => x.Contains(" c."))
                        ?.Trim()
                        .Split(' ')
                        .First(), out result.Pages);

                    if (string.IsNullOrWhiteSpace(result.ISBN) && result.Bib.Contains("ISBN", StringComparison.InvariantCultureIgnoreCase)) {
                        result.ISBN = result.Bib.Split("ISBN", StringSplitOptions.RemoveEmptyEntries)[1].Trim()
                            .Split(". ")
                            .First();
                    }

                    yield return result;
                }
            }
        }

        private static void FillHttpClient(HttpClient client) {
            client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        }
        
        private static async Task<Info> GetSearchResponse(HttpClient client, string token, int page) {
            _logger.Info($"Запрашиваем страницу {page}");

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

            var apiResponse = await client.PostJson<ApiResponse>(new Uri("https://profspo.ru/catalog/search"), new FormUrlEncodedContent(values));
            return apiResponse?.Info ?? new Info();
        }

        private static async Task<string> GetToken(HttpClient client) {
            var doc = await client.GetHtmlDoc(new Uri("https://profspo.ru/catalog"));
            var metaBlock = doc?.DocumentNode.GetByFilter("meta").FirstOrDefault(t => t.Attributes["name"]?.Value == "csrf-token");
            return metaBlock?.Attributes["content"].Value;
        }
    }
}
