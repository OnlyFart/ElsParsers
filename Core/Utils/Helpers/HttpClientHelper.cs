using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Core.Configs;
using NLog;

namespace Core.Utils.Helpers {
    public class HttpClientHelper {
        private const int MAX_TRY_COUNT = 3;
        
        private static readonly Logger _logger = LogManager.GetLogger(nameof(HttpClientHelper));
        
        /// <summary>
        /// Создание HttpClient'a для обхода сайта
        /// </summary>
        /// <returns></returns>
        public static HttpClient GetClient(IParserConfigBase config) {
            var handler = new HttpClientHandler{ AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate};
            if (!string.IsNullOrEmpty(config.Proxy)) {
                var split = config.Proxy.Split(":");
                handler.Proxy = new WebProxy(split[0], int.Parse(split[1])); 
            }

            var httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            httpClient.DefaultRequestHeaders.Add("User-Agent" ,"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.88 Safari/537.36");
            
            return httpClient;
        }
        
        public static async Task<string> GetStringAsync(HttpClient client, Uri url) {
            for (var i = 0; i < MAX_TRY_COUNT; i++) {
                try {
                    _logger.Debug($"Get {url}");
                    var response = await client.GetAsync(url);
                    if (response.StatusCode == HttpStatusCode.NotFound) {
                        return null;
                    }
                    _logger.Debug($"End {url}. Response {response}");
                    
                    return await response.Content.ReadAsStringAsync();
                } catch (Exception e) {
                    _logger.Error(e.ToString());
                }
            }

            return null;
        }

        public static async Task<string> PostAsync(HttpClient client, Uri url, ByteArrayContent data) {
            for (var i = 0; i < MAX_TRY_COUNT; i++) {
                try {
                    _logger.Debug($"Post {url}.");
                    using var response = await client.PostAsync(url, data);
                    var responseString = await response.Content.ReadAsStringAsync();
                    _logger.Debug($"Post {url}. Response {responseString}");
                    
                    return responseString;
                } catch (Exception e) {
                    _logger.Error(e.ToString());
                }
            }

            return null;
        }
    }
}
