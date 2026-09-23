using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NetCorePal.Extensions.Jwt;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Web.Utils;
using StackExchange.Redis;

namespace Bumpic.Web.Services.SessionTokens;

/// <summary>
/// 会话令牌签发结果。
/// </summary>
public record SessionTokens(string AccessToken, string RefreshToken, int ExpiresInSeconds);

/// <summary>
/// 刷新令牌处理结果。
/// </summary>
public record RefreshResult(UserAccountId UserId, SessionTokens SessionTokens);

/// <summary>
/// 用户会话令牌服务：签发客户端访问令牌与 30 天刷新令牌，刷新令牌以摘要存于 Redis，支持轮换与撤销。
/// 令牌签发参数统一取自上游 Jwt 配置（<see cref="JwtConfig"/>）。
/// </summary>
public class SessionTokenIssuer(
    IConnectionMultiplexer connectionMultiplexer,
    IJwtProvider jwtProvider,
    IOptions<JwtConfig> jwtConfig)
{
    /// <summary>
    /// 客户端令牌类型。
    /// </summary>
    public const string ClientType = "client";

    /// <summary>
    /// Redis 会话键前缀。
    /// </summary>
    private const string SessionKeyPrefix = "Bumpic_session";

    /// <summary>
    /// 访问令牌默认有效期（分钟），仅当配置未提供时使用。
    /// </summary>
    private const int DefaultAccessTokenMinutes = 15;

    /// <summary>
    /// 刷新令牌默认有效期（分钟），仅当配置未提供时使用。
    /// </summary>
    private const int DefaultRefreshTokenMinutes = 43200;

    /// <summary>
    /// 生成访问令牌与刷新令牌，并保存刷新会话。
    /// </summary>
    /// <param name="userId">用户账户标识。</param>
    /// <param name="email">用户邮箱（当前仅保留参数，用于后续扩展声明）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>会话令牌签发结果。</returns>
    public async Task<SessionTokens> GenerateAsync(
        UserAccountId userId,
        string email,
        CancellationToken cancellationToken)
    {
        var config = jwtConfig.Value;
        var accessMinutes = config.ExpirationInMinutes > 0 ? config.ExpirationInMinutes : DefaultAccessTokenMinutes;
        var refreshMinutes = config.RefreshTokenExpirationInMinutes > 0
            ? config.RefreshTokenExpirationInMinutes
            : DefaultRefreshTokenMinutes;

        var now = DateTime.UtcNow;
        var accessExpires = now.AddMinutes(accessMinutes);
        var accessToken = await jwtProvider.GenerateJwtToken(new JwtData(
            config.Issuer,
            config.Audience,
            BuildClaims(userId),
            now,
            accessExpires));

        var refreshToken = CreateRefreshToken();
        await SaveRefreshTokenAsync(refreshToken, userId, TimeSpan.FromMinutes(refreshMinutes));

        return new SessionTokens(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresInSeconds: (int)(accessExpires - now).TotalSeconds);
    }

    /// <summary>
    /// 轮换刷新令牌：校验旧会话后签发新令牌并撤销旧会话。
    /// </summary>
    /// <param name="refreshToken">当前刷新令牌。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>刷新结果。</returns>
    /// <exception cref="KnownException">刷新令牌无效或已撤销。</exception>
    public async Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var userId = await ReadRefreshTokenAsync(refreshToken);
        if (userId is null)
        {
            throw new KnownException("INVALID_REFRESH_TOKEN");
        }

        var database = connectionMultiplexer.GetDatabase();
        await database.KeyDeleteAsync(SessionKey(refreshToken));
        var sessionTokens = await GenerateAsync(new UserAccountId(userId.Value), email: string.Empty, cancellationToken);
        return new RefreshResult(new UserAccountId(userId.Value), sessionTokens);
    }

    /// <summary>
    /// 撤销刷新会话。
    /// </summary>
    /// <param name="refreshToken">待撤销的刷新令牌。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>撤销任务。</returns>
    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        return database.KeyDeleteAsync(SessionKey(refreshToken));
    }

    /// <summary>
    /// 保存刷新会话摘要。
    /// </summary>
    private async Task SaveRefreshTokenAsync(string refreshToken, UserAccountId userId, TimeSpan ttl)
    {
        var database = connectionMultiplexer.GetDatabase();
        await database.StringSetAsync(SessionKey(refreshToken), userId.Id.ToString(), ttl);
    }

    /// <summary>
    /// 读取刷新会话对应的用户标识。
    /// </summary>
    private async Task<Guid?> ReadRefreshTokenAsync(string refreshToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        var value = await database.StringGetAsync(SessionKey(refreshToken));
        if (value.IsNullOrEmpty || !Guid.TryParse(value.ToString(), out var userId))
        {
            return null;
        }

        return userId;
    }

    /// <summary>
    /// 生成不透明刷新令牌。
    /// </summary>
    private static string CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// 生成会话键（仅保存令牌摘要，不保存明文刷新令牌）。
    /// </summary>
    private static string SessionKey(string refreshToken)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return $"{SessionKeyPrefix}:{Convert.ToHexString(digest)}";
    }

    /// <summary>
    /// 构建访问令牌声明。
    /// </summary>
    private static Claim[] BuildClaims(UserAccountId userId)
    {
        return
        [
            new Claim("uid", userId.Id.ToString()),
            new Claim("type", ClientType),
            new Claim("refresh-token", "false")
        ];
    }
}
