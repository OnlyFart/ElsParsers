using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;
using NLog;

namespace Parser.Core.Extensions {
    public static class HttpClientExtensions {
        private const int MAX_TRY_COUNT = 3;
        
        private static readonly Logger _logger = LogManager.GetLogger(nameof(HttpClientExtensions));

        public static async Task<(string Response, HttpStatusCode StatusCode)> GetStringWithTriesAsync(this HttpClient client, Uri url) {
            for (var i = 0; i < MAX_TRY_COUNT; i++) {
                try {
                    _logger.Debug($"Get {url}");
                    using var response = await client.GetAsync(url);
                    if (response.StatusCode == HttpStatusCode.NotFound) {
                        return (string.Empty, response.StatusCode);
                    }
                    
                    if (response.StatusCode != HttpStatusCode.OK) {
                        continue;
                    }
                    
                    _logger.Debug($"End {url}. Response {response}");
                    
                    return (await response.Content.ReadAsStringAsync(), response.StatusCode);
                } catch (Exception e) {
                    _logger.Error(e.ToString());
                }
            }

            return default;
        }

        public static async Task<string> PostWithTriesAsync(this HttpClient client, Uri uri, ByteArrayContent data) {
            for (var i = 0; i < MAX_TRY_COUNT; i++) {
                try {
                    _logger.Debug($"Post {uri}.");
                    using var response = await client.PostAsync(uri, data);

                    if (response.StatusCode != HttpStatusCode.OK) {
                        continue;
                    }
                    
                    var responseString = await response.Content.ReadAsStringAsync();
                    _logger.Debug($"Post {uri}. Response {responseString}");
                    
                    return responseString;
                } catch (Exception e) {
                    _logger.Error(e.ToString());
                }
            }

            return default;
        }
        
        public static async Task<T> GetJson<T>(this HttpClient client, Uri uri) {
            var (response, statusCode) = await client.GetStringWithTriesAsync(uri);

            return statusCode != HttpStatusCode.OK ? default : JsonConvert.DeserializeObject<T>(response);
        }
        
        public static async Task<T> PostJson<T>(this HttpClient client, Uri uri, ByteArrayContent data) {
            var content = await client.PostWithTriesAsync(uri, data);

            return string.IsNullOrWhiteSpace(content) ? default : JsonConvert.DeserializeObject<T>(content);
        }

        public static async Task<HtmlDocument> GetHtmlDoc(this HttpClient client, Uri uri) {
            var (response, statusCode) = await client.GetStringWithTriesAsync(uri);

            if (statusCode != HttpStatusCode.OK) {
                return default;
            }
            
            var doc = new HtmlDocument();
            doc.LoadHtml(response);

            return doc;
        }
    }
}
