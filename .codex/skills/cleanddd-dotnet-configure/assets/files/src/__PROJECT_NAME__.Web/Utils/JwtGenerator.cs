using System.Security.Claims;
using __PROJECT_NAME__.Domain.Enums;
using Microsoft.Extensions.Options;
using NetCorePal.Extensions.Jwt;
using Newtonsoft.Json;

namespace __PROJECT_NAME__.Web.Utils;

/// <summary>
/// 登录 Token 
/// </summary>
public class TokenVo
{
    /// <summary>
    /// Jwt token
    /// </summary>
    [JsonProperty("access_token")]
    public required string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 刷新 Jwt token 的 token
    /// </summary>
    [JsonProperty("refresh_token")]
    public required string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// 过期时间
    /// </summary>
    [JsonProperty("expires_in")]
    public string ExpiresIn { get; set; } = string.Empty;

    /// <summary>
    /// 权限范围
    /// </summary>
    [JsonProperty("scope")]
    public string Scope { get; set; } = string.Empty;
    
    /// <summary>
    /// 操作类型
    /// </summary>
    [JsonProperty("operation")]
    public OperationType Operation { get; set; } = OperationType.Login; // 默认登录场景
}

/// <summary>
/// 用户信息
/// </summary>
public record UserData
{
    /// <summary>
    /// 用户 Id
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// 手机号
    /// </summary>
    public required string Phone { get; set; }
    
    /// <summary>
    /// 区号
    /// </summary>
    public required string PhoneRegion { get; set; }

    /// <summary>
    /// 邮箱
    /// </summary>
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// 管理员信息
/// </summary>
public record AdminUserData
{
    /// <summary>
    /// 管理员 Id
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 邮箱
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 手机号
    /// </summary>
    public required string Phone { get; set; }

    /// <summary>
    /// 手机区号
    /// </summary>
    public required string PhoneRegion { get; set; }

    /// <summary>
    /// 角色
    /// </summary>
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// Jwt 配置
/// </summary>
public record JwtConfig
{
    /// <summary>
    /// 密钥
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// 验签公钥 Id
    /// </summary>
    public string Kid { get; set; } = string.Empty;

    /// <summary>
    /// 颁发者
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// 受众
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// 过期时间, 单位分钟
    /// </summary>
    public int ExpirationInMinutes { get; set; } = 60; // 默认60min

    /// <summary>TokenVo
    /// RefreshToken 过期时间, 单位分钟
    /// </summary>
    public int RefreshTokenExpirationInMinutes { get; set; } = 60;  // 默认60min
}

/// <summary>
/// Jwt 生成工具
/// </summary>
public class JwtGenerator(IOptions<JwtConfig> options, IJwtProvider jwtProvider)
{
    /// <summary>
    /// 生成前台用户 Token
    /// </summary>
    public async Task<TokenVo> Generate(UserData userData)
    {
        var jwtConfig = options.Value;
        var claims = new[]
        {
            new Claim("uid", userData.Id.ToString()),
            new Claim("type", PolicyNames.Client),
            new Claim("phone", userData.Phone),
            new Claim("phone-region", userData.PhoneRegion),
            new Claim("email", userData.Email),
            new Claim("refresh-token", "false")
        };

        var expires = DateTimeOffset.UtcNow.AddMinutes(jwtConfig.ExpirationInMinutes);
        var jwtString = await jwtProvider.GenerateJwtToken(new JwtData(
            jwtConfig.Issuer,
            jwtConfig.Audience,
            claims,
            DateTime.UtcNow,
            expires.UtcDateTime)
        );

        var refreshClaims = new[]
        {
            new Claim("uid", userData.Id.ToString()),
            new Claim("type", PolicyNames.Client),
            new Claim("phone", userData.Phone),
            new Claim("phone-region", userData.PhoneRegion),
            new Claim("email", userData.Email),
            new Claim("refresh-token", "true")
        };
        var refreshExpires = DateTimeOffset.UtcNow.AddMinutes(jwtConfig.RefreshTokenExpirationInMinutes);
        var refreshTokenString = await jwtProvider.GenerateJwtToken(new JwtData(
            jwtConfig.Issuer,
            jwtConfig.Audience,
            refreshClaims,
            DateTime.UtcNow,
            refreshExpires.UtcDateTime)
        );


        return new TokenVo
        {
            AccessToken = $"Bearer {jwtString}",
            RefreshToken = $"Bearer {refreshTokenString}",
            ExpiresIn = expires.ToUnixTimeMilliseconds().ToString(),
            Scope = "Login"
        };
    }

    /// <summary>
    /// 生成管理员 Token
    /// </summary>
    public async Task<TokenVo> Generate(AdminUserData userData)
    {
        var jwtConfig = options.Value;
        var claims = new[]
        {
            new Claim("uid", userData.Id.ToString()),
            new Claim("type", PolicyNames.Admin),
            new Claim("emile", userData.Email),
            new Claim(ClaimTypes.Role, userData.Role),
            new Claim("refresh-token", "false")
        };

        var expires = DateTimeOffset.UtcNow.AddMinutes(jwtConfig.ExpirationInMinutes);
        var jwtString = await jwtProvider.GenerateJwtToken(new JwtData(
            jwtConfig.Issuer,
            jwtConfig.Audience,
            claims,
            DateTime.UtcNow,
            expires.UtcDateTime));

        return new TokenVo
        {
            AccessToken = $"Bearer {jwtString}",
            RefreshToken = string.Empty,
            ExpiresIn = expires.ToUnixTimeMilliseconds().ToString(),
            Scope = "Login"
        };
    }
}

/// <summary>
/// Token 类型
/// </summary>
public abstract class PolicyNames
{
    /// <summary>
    /// 后台 Token
    /// </summary>
    public const string Admin = "admin";

    /// <summary>
    /// 管理员
    /// </summary>
    public const string AdminOnly = "adminOnly";

    /// <summary>
    /// C 端
    /// </summary>
    public const string Client = "client";

    /// <summary>
    ///刷新 Token
    /// </summary>
    public const string RefreshToken = "refreshToken";
    
    /// <summary>
    /// Agent
    /// </summary>
    public const string Agent = "agent";
}

/// <summary>
/// Token 类型
/// </summary>
public abstract class TokenType
{
    /// <summary>
    /// 后台 Token
    /// </summary>
    public const string Admin = "admin";

    /// <summary>
    /// C 端
    /// </summary>
    public const string Client = "client";
}