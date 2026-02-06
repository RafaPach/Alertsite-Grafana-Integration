using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using NOCAPI.Plugins.Config;

namespace NOCAPI.Modules.Users.Helpers
{
    public class TokenService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;

        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
        private const string TokenCacheKey = "AlertsiteAccessTokenItem";
        private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(55);

        // Prevents token stampedes
        private static readonly SemaphoreSlim _refreshLock = new(1, 1);

        public TokenService(IHttpClientFactory httpClientFactory, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
        }

        public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
        {
            // 1) Try to get the current cached token
            if (_cache.TryGetValue<TokenCacheItem>(TokenCacheKey, out var cached))
            {
                // If token is younger than 55 minutes, reuse it
                var age = DateTimeOffset.UtcNow - cached.AcquiredAtUtc;
                if (age < RefreshThreshold && !string.IsNullOrWhiteSpace(cached.AccessToken))
                {
                    return cached.AccessToken!;
                }
            }

            // 2) Acquire lock to refresh token only once if multiple callers arrive
            await _refreshLock.WaitAsync(ct);
            try
            {
                // Double-check after entering the lock (another caller may have refreshed)
                if (_cache.TryGetValue<TokenCacheItem>(TokenCacheKey, out cached))
                {
                    var age = DateTimeOffset.UtcNow - cached.AcquiredAtUtc;
                    if (age < RefreshThreshold && !string.IsNullOrWhiteSpace(cached.AccessToken))
                    {
                        return cached.AccessToken!;
                    }
                }

                // 3) Actually fetch a new token
                var newToken = await RequestNewTokenAsync(ct);

                var item = new TokenCacheItem
                {
                    AccessToken = newToken,
                    AcquiredAtUtc = DateTimeOffset.UtcNow
                };

                // 4) Cache the token. Set absolute expiration at 60 minutes (server expiry),
                // but our refresh logic will proactively refresh at 55 minutes.
                _cache.Set(
                    TokenCacheKey,
                    item,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60)
                    });

                return newToken;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        public async Task<string> ForceRefreshAsync(CancellationToken ct = default)
        {
            await _refreshLock.WaitAsync(ct);
            try
            {
                var newToken = await RequestNewTokenAsync(ct);
                var item = new TokenCacheItem
                {
                    AccessToken = newToken,
                    AcquiredAtUtc = DateTimeOffset.UtcNow
                };

                _cache.Set(
                    TokenCacheKey,
                    item,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60)
                    });

                return newToken;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private async Task<string> RequestNewTokenAsync(CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient();
            var url = "https://api.alertsite.com/api/v3/access-tokens";


            var username = (PluginConfigWrapper.Get("Username") ?? "EMCTSAIOpsTeam@computershare.com").Trim();
            var password = (PluginConfigWrapper.Get("Password") ?? "W/UMYu9~6CtMpDm8").Trim();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "AlertSite credentials are missing. Check Plugins/{pluginName}/config.json and keys: Username, Password.");
            }

            var bodyObj = new { username, password };

            using var content = new StringContent(
                JsonSerializer.Serialize(bodyObj, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await client.PostAsync(url, content, ct);
            var payload = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Failed to get token: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {payload}");
            }

            var tokenData = JsonSerializer.Deserialize<TokenResponse>(payload, _jsonOptions);
            if (tokenData == null || string.IsNullOrWhiteSpace(tokenData.AccessToken))
                throw new InvalidOperationException("Invalid token response: missing access_token.");

            return tokenData.AccessToken!;
        }

        private sealed class TokenCacheItem
        {
            public string? AccessToken { get; set; }
            public DateTimeOffset AcquiredAtUtc { get; set; }
        }

        private sealed class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;
        }
    }
}
