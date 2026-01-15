using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace NOCAPI.Modules.Users.Helpers
{
    public class TokenService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
        private const string TokenCacheKey = "AlertsiteAccessToken";
        private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(55);

        public TokenService(IHttpClientFactory httpClientFactory, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            if (_cache.TryGetValue(TokenCacheKey, out string token))
            {
                return token;
            }

            var client = _httpClientFactory.CreateClient();
            var url = "https://api.alertsite.com/api/v3/access-tokens";

            var bodyObj = new
            {
                username = "EMCTSAIOpsTeam@computershare.com",
                password = "W/UMYu9~6CtMpDm8"
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(bodyObj, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await client.PostAsync(url, content);
            var payload = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Failed to get token: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {payload}");
            }

            var tokenData = JsonSerializer.Deserialize<TokenResponse>(payload, _jsonOptions);
            if (tokenData == null || string.IsNullOrWhiteSpace(tokenData.AccessToken))
                throw new InvalidOperationException("Invalid token response: missing access_token.");

            // Cache the token with expiration slightly before it expires
            _cache.Set(TokenCacheKey, tokenData.AccessToken, TokenLifetime);

            Console.WriteLine(tokenData.AccessToken);
            return tokenData.AccessToken;
        }

        private class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;
        }
    }
}
