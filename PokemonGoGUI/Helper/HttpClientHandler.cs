using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text;
using System.Threading.Tasks;

namespace PokemonGoGUI.Helper
{
    public class HttpClientHandler : IHttpHandler
    {
        private HttpClient _client = new HttpClient();

        public HttpResponseMessage Get(string url)
        {
            return GetAsync(url).Result;
        }

        public HttpResponseMessage Post(string url, HttpContent content)
        {
            return PostAsync(url, content).Result;
        }

        public async Task<HttpResponseMessage> GetAsync(string url)
        {
            return await _client.GetAsync(url);
        }

        public async Task<HttpResponseMessage> PostAsync(string url, HttpContent content)
        {
            return await _client.PostAsync(url, content);
        }

        public async Task<HttpResponseMessage> PostAsJsonAsync<T>(string url, T value)
        {
            return await _client.PostAsJsonAsync(url, value);
        }

        public async Task<HttpResponseMessage> PatchAsJsonAsync<T>(string url, T value)
        {
            
            var content = new ObjectContent<T>(value, new JsonMediaTypeFormatter());
            var requestUri = new UriBuilder(url).Uri;

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), requestUri) { Content = content };

            return await _client.SendAsync(request);
            
        }
    }
}
