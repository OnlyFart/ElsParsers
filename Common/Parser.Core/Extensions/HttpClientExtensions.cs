using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;
using NLog;
using Parser.Core.Configs;

namespace Parser.Core.Extensions {
    public static class HttpClientExtensions {
        private const int MAX_TRY_COUNT = 3;
        
        private static readonly Logger _logger = LogManager.GetLogger(nameof(HttpClientExtensions));
        
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

        public static async Task<string> GetStringWithTriesAsync(this HttpClient client, Uri url) {
            for (var i = 0; i < MAX_TRY_COUNT; i++) {
                try {
                    _logger.Debug($"Get {url}");
                    using var response = await client.GetAsync(url);
                    if (response.StatusCode == HttpStatusCode.NotFound) {
                        return default;
                    }
                    
                    if (response.StatusCode != HttpStatusCode.OK) {
                        continue;
                    }
                    
                    _logger.Debug($"End {url}. Response {response}");
                    
                    return await response.Content.ReadAsStringAsync();
                } catch (Exception e) {
                    _logger.Error(e.ToString());
                }
            }

            return default;
        }
        
        private static HttpRequestMessage CloneHttpRequestMessage(this HttpRequestMessage req)
        {
            var clone = new HttpRequestMessage(req.Method, req.RequestUri);

            clone.Content = req.Content;
            clone.Version = req.Version;

            foreach (KeyValuePair<string, object> prop in req.Properties) {
                clone.Properties.Add(prop);
            }

            foreach (KeyValuePair<string, IEnumerable<string>> header in req.Headers) {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
        
        public static async Task<string> GetStringWithTriesAsync(this HttpClient client, HttpRequestMessage message) {
            for (var i = 0; i < MAX_TRY_COUNT; i++) {
                try {
                    _logger.Debug($"Get {message.RequestUri}");
                    using var tmpMsg = CloneHttpRequestMessage(message);
                    using var response = await client.SendAsync(tmpMsg);
                    if (response.StatusCode == HttpStatusCode.NotFound) {
                        return default;
                    }
                    
                    if (response.StatusCode != HttpStatusCode.OK) {
                        continue;
                    }
                    
                    _logger.Debug($"End {message.RequestUri}. Response {response}");
                    return await response.Content.ReadAsStringAsync();
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
            var content = await client.GetStringWithTriesAsync(uri);

            return string.IsNullOrWhiteSpace(content) ? default : JsonConvert.DeserializeObject<T>(content);
        }
        
        public static async Task<T> PostJson<T>(this HttpClient client, Uri uri, ByteArrayContent data) {
            var content = await client.PostWithTriesAsync(uri, data);

            return string.IsNullOrWhiteSpace(content) ? default : JsonConvert.DeserializeObject<T>(content);
        }

        public static async Task<HtmlDocument> GetHtmlDoc(this HttpClient client, Uri uri) {
            var content = await client.GetStringWithTriesAsync(uri);

            if (string.IsNullOrWhiteSpace(content)) {
                return default;
            }
            
            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            return doc;
        }
        
        public static async Task<HtmlDocument> GetHtmlDoc(this HttpClient client, HttpRequestMessage message) {
            var content = await client.GetStringWithTriesAsync(message);

            if (string.IsNullOrWhiteSpace(content)) {
                return default;
            }
            
            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            return doc;
        }
    }
}
