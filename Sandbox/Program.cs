using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Core.Configs;
using Core.Extensions;
using Core.Providers.Implementations;
using HtmlAgilityPack;
using TurnerSoftware.SitemapTools;
using TurnerSoftware.SitemapTools.Parser;

namespace Sandbox {
    class Program {
        public class  ParserConfig : IParserConfigBase {
            public string Proxy { get; set; }
            public int MaxThread { get; set; }
            public int BatchSize { get; set; }
        }
        
        public class MongoConfig : IMongoConfig {
            public string ConnectionString => "";
            public string DatabaseName => "Schools";
            public string CollectionName => "Schoolme";
        }

        public class School {
            public string Link;
            public string Name;
            public string FullName;
            public string EMail;
            public string Address;
            public string Phone;
            public string Director;
            public string Type;
            public string Vid;
        }
        
        private static async Task<SitemapFile> GetLinksSitemaps(HttpClient client, Uri sitemap) {
            var rootSitemap = await client.GetStringWithTriesAsync(sitemap);

            using var reader = new StringReader(rootSitemap);
            return await new XmlSitemapParser().ParseSitemapAsync(reader);
        }
        
        private static IEnumerable<Uri> Filter(SitemapFile sitemap, ICollection<string> processed) {
            return sitemap.Urls.Where(uri => uri.Location.Segments.Length == 3 && !processed.Contains(uri.Location.ToString())).Select(uri => uri.Location);
        }

        public static async Task Main(string[] args) {
            var client = HttpClientExtensions.GetClient(new ParserConfig{Proxy = "127.0.0.1:8888"});
            
            var mongoProvider = new MongoRepository<School>(new MongoConfig());

            var processed = await mongoProvider.ReadProjection(t => t.Link).ContinueWith(t => new HashSet<string>(t.Result));

            var toProcess = new HashSet<Uri>();
            foreach (var url in new[]{"https://schoolme.ru/sitemap1.xml", "https://schoolme.ru/sitemap2.xml", "https://schoolme.ru/sitemap3.xml"}) {
                var sitemaps = await GetLinksSitemaps(client, new Uri(url));

                foreach (var t in Filter(sitemaps, processed)) {
                    toProcess.Add(t);
                }
                
            }

            Console.WriteLine($"К обработке {toProcess.Count} урлов");

            ServicePointManager.DefaultConnectionLimit = 1000;
            
            var batchBlock = new BatchBlock<School>(100);
            var saveBookBlock = new ActionBlock<School[]>(async books => await mongoProvider.CreateMany(books));
            
            batchBlock.LinkTo(saveBookBlock);
            
            Parallel.ForEach(toProcess, new ParallelOptions {MaxDegreeOfParallelism = 10}, url => {
                var content = client.GetStringWithTriesAsync(url).Result;
                if (string.IsNullOrEmpty(content)) {
                    Console.WriteLine($"NULL {url}");
                    return;
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(content);

                var scool = new School {
                    Link = url.ToString(),
                    Name = doc.DocumentNode.GetByFilter("h1", "school_wrap_title").FirstOrDefault().InnerText.Trim()
                };
                foreach (var block in doc.DocumentNode.GetByFilterEq("div", "not_contact")) {
                    var nameBlock = block.GetByFilter("div", "not_contact_name").FirstOrDefault();
                    var name = nameBlock.InnerText.Trim();
                    var value = nameBlock.NextSibling.InnerText.Trim();
                    if (string.IsNullOrWhiteSpace(value)) {
                        value = nameBlock.NextSibling.NextSibling.InnerText.ToString();
                    }

                    if (name.Contains("Полное наименование")) {
                        scool.FullName = value;
                    } else if (name.Contains("Адрес")) {
                        scool.Address = value;
                    } else if (name.Contains("Руководитель")) {
                        scool.Director = value;
                    } else if (name.Contains("E-mail")) {
                        scool.EMail = value;
                    } else if (name.Contains("Тип учебного учреждения")) {
                        scool.Type = value;
                    } else if (name.Contains("Телефон")) {
                        scool.Phone = value;
                    } else if (name.Contains("Вид деятельности")) {
                        scool.Vid = value;
                    } else {
                        Console.WriteLine(name);
                    }
                }

                batchBlock.SendAsync(scool).Wait();
            });
            
            await DataflowExtension.WaitBlocks(batchBlock, saveBookBlock);

            Console.WriteLine("Hello World!");
        }
    }
}
