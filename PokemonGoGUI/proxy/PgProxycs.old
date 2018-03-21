using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NLog;
using PokemonGoGUI.Helper;
using PokemonGoGUI.Models;


namespace PokemonGoGUI.proxy
{
    public class PgProxy
    {
        /*private string _baseurl="http://localhost:4242/account";*/
        private readonly string _baseurl;
        private const string GetAccounturl = "request";
        private const string ReleaseAccounturl = "release";
        private const string AddAccounturl = "add";
        private readonly IHttpHandler _client;
        private Logger _logger;

        public PgProxy(string baseurl)
        {
            _baseurl = baseurl;
            _logger = LogManager.GetCurrentClassLogger();
            _client = new Helper.HttpClientHandler();
        }

        public async Task<List<PgAccount>> GetPgAccounts(string identifier, bool below30, int quantity)
        {
            try
            {
                var builder = new UriBuilder($"{_baseurl}/{GetAccounturl}")
                {
                    Query = below30
                        ? $"system_id='{identifier}'&count={quantity}&min_level=0&max_level=29&banned_or_new=true"
                        : $"system_id='{identifier}'&count={quantity}&min_level=30&max_level=40&banned_or_new=true"
                };
                var response = await _client.GetAsync(builder.Uri.ToString());
                if (response.IsSuccessStatusCode)
                {
                    var ret = new List<PgAccount>();
                    if (quantity > 0)
                    {
                        if (quantity == 1)
                        {
                            var retAccount = await response.Content.ReadAsAsync<PgAccount>();
                            retAccount.SystemId = identifier;
                            ret.Add(retAccount);
                        }
                        else
                        {
                            var retAccouts = await response.Content.ReadAsAsync<List<PgAccount>>();
                            if (retAccouts != null)
                            {
                                foreach (var t in retAccouts)
                                {
                                    t.SystemId = identifier;
                                    ret.Add(t);
                                }
                            }
                        }
                    }

                    return ret;
                }

                _logger.Error(response);
                return null;
            }
            catch (Exception e)
            {
                _logger.Error(e);
                return null;
            }
        }

        public async Task AddPgAccount(int level, PgAccount account)
        {
            var builder = new UriBuilder($"{_baseurl}/{AddAccounturl}");
            if (level > 29)
            {
                account.InputLevel = level;
            }

            var response = await _client.PostAsJsonAsync(builder.Uri.ToString(), account);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Error(response);
            }
            else
            {
                if (level > 29)
                {
                    await ReleaseAccount(account);
                }
            }
        }

        public async Task ReleaseAccount(PgAccount account)
        {
            var builder = new UriBuilder($"{_baseurl}/{ReleaseAccounturl}");
            var response = await _client.PostAsJsonAsync(builder.Uri.ToString(), account);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Error(response);
            }
        }
    }
}
