using System.Net.Http;
using System.Threading.Tasks;

namespace PokemonGoGUI.Helper
{
    public interface IHttpHandler
    {
        HttpResponseMessage Get(string url);
        HttpResponseMessage Post(string url, HttpContent content);
        Task<HttpResponseMessage> GetAsync(string url);
        Task<HttpResponseMessage> PostAsync(string url, HttpContent content);
        Task<HttpResponseMessage> PostAsJsonAsync<T>(string url, T value);
    }
}
