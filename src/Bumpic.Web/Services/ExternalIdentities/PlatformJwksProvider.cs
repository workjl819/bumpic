using Microsoft.Extensions.Caching.Memory;
using Bumpic.Web.Clients;

namespace Bumpic.Web.Services.ExternalIdentities;

/// <summary>
/// 平台 JWKS 提供实现：经 Apple/Google Refit Client 获取并缓存 1 小时，支持强制刷新。
/// </summary>
public class PlatformJwksProvider(
    IMemoryCache memoryCache,
    IAppleAuthClient appleAuthClient,
    IGoogleAuthClient googleAuthClient,
    ILogger<PlatformJwksProvider> logger)
{
    /// <summary>
    /// JWKS 缓存时长。
    /// </summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    /// <summary>
    /// Apple JWKS 缓存键。
    /// </summary>
    private const string AppleCacheKey = "apple-jwks";

    /// <summary>
    /// Google JWKS 缓存键。
    /// </summary>
    private const string GoogleCacheKey = "google-jwks";

    /// <summary>
    /// 获取 Apple JWKS。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="forceRefresh">为 true 时忽略缓存重新拉取。</param>
    public Task<string> GetAppleJwksAsync(CancellationToken cancellationToken, bool forceRefresh = false)
    {
        return FetchAsync(AppleCacheKey, () => appleAuthClient.GetJwksAsync(cancellationToken), forceRefresh);
    }

    /// <summary>
    /// 获取 Google JWKS。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="forceRefresh">为 true 时忽略缓存重新拉取。</param>
    public Task<string> GetGoogleJwksAsync(CancellationToken cancellationToken, bool forceRefresh = false)
    {
        return FetchAsync(GoogleCacheKey, () => googleAuthClient.GetCertsAsync(cancellationToken), forceRefresh);
    }

    /// <summary>
    /// 缓存优先获取 JWKS。
    /// </summary>
    private async Task<string> FetchAsync(string cacheKey, Func<Task<string>> fetch, bool forceRefresh)
    {
        if (!forceRefresh && memoryCache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        if (forceRefresh)
        {
            memoryCache.Remove(cacheKey);
        }

        var json = await fetch();
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new KnownException(ExternalIdTokenErrorCodes.Invalid);
        }

        memoryCache.Set(cacheKey, json, CacheTtl);
        logger.LogInformation("已刷新外部身份 JWKS：{CacheKey}", cacheKey);
        return json;
    }
}
