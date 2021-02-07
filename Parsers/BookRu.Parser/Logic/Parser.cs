using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using BookRu.Parser.Types.API;
using BookRu.Parser.Types.API.Book;
using BookRu.Parser.Types.API.Categories;
using BookRu.Parser.Types.API.Sidebar;
using Core.Extensions;
using Core.Providers.Interfaces;
using Core.Types;
using Newtonsoft.Json;
using Parser.Core.Configs;
using Parser.Core.Extensions;
using Parser.Core.Logic;

namespace BookRu.Parser.Logic {
    public class Parser : ParserBase {
        public Parser(IParserConfigBase config, IRepository<BookInfo> provider) : base(config, provider) { }

        protected override string ElsName => "BookRu";

        protected override async Task<IDataflowBlock[]> RunInternal(HttpClient client, ISet<string> processed) {
            var getBookIdsBlock = new TransformBlock<MenuItem, IEnumerable<string>>(async categoryId => await GetBookIds(client, categoryId));
            getBookIdsBlock.CompleteMessage(_logger, "Получение каталогов книг закончено. Ждем получения книг.");

            var filterBlock = new TransformManyBlock<IEnumerable<string>, string>(bookIds => Filter(bookIds, processed));
            var getBooksBlock = new TransformBlock<string, BookInfo>(async bookId => await GetBook(client, bookId), GetParserOptions());
            getBooksBlock.CompleteMessage(_logger, "Получение книг закончено. Ждем сохранения.");

            var batchBlock = new BatchBlock<BookInfo>(_config.BatchSize);
            var saveBookBlock = new ActionBlock<BookInfo[]>(async books => await SaveBooks(books));
            saveBookBlock.CompleteMessage(_logger, "Сохранение завершено.");

            getBookIdsBlock.LinkTo(filterBlock);
            filterBlock.LinkTo(getBooksBlock);
            getBooksBlock.LinkTo(batchBlock);
            batchBlock.LinkTo(saveBookBlock);

            foreach (var categoryId in await GetCategoryIds(client)) {
                await getBookIdsBlock.SendAsync(categoryId);
            }
            
            return new IDataflowBlock[] {getBookIdsBlock, filterBlock, getBooksBlock, batchBlock, saveBookBlock};
        }

        private static IEnumerable<string> Filter(IEnumerable<string> bookIds, ISet<string> processed) {
            return bookIds.Where(processed.Add);
        }

        private static async Task<IEnumerable<MenuItem>> GetCategoryIds(HttpClient client) {
            var sidebar = await client.GetJson<ApiResponse<Sidebar>>(new Uri("https://www.book.ru/cat/get_sidebar"));
            return sidebar?.Data.Content.SelectMany(t => t.Value);
        }

        private static async Task<IEnumerable<string>> GetBookIds(HttpClient client, MenuItem menuItem) {
            var data = new {
                cat_id = menuItem.Id,
                as_view = 3,
                years = new string[] { }
            };

            _logger.Info($"Получаем каталог с ID = {menuItem.Id}, Name = {menuItem.Text}");

            var content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
            var response = await client.PostJson<ApiResponse<CategoryContent>>(new Uri("https://www.book.ru/cat/get_categories"), content);
            return response?.Data?.Content?.Select(t => t.Key) ?? Enumerable.Empty<string>();
        }

        private async Task<BookInfo> GetBook(HttpClient client, string id) {
            var response = await client.GetJson<ApiResponse<Dictionary<string, BookItem>>>(new Uri($"https://www.book.ru/book/get_book/{id}"));
            return response?.Data == default || !response.Data.TryGetValue(id, out var book)
                ? default
                : new BookInfo(id, ElsName) {
                    Authors = book.Author,
                    Bib = book.Bib,
                    ISBN = book.ISBN,
                    Name = book.Name,
                    Pages = book.Pages ?? 0,
                    Year = book.Year,
                    Publisher = book.Publisher
                };
        }
    }
}
