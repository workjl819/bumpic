namespace Bumpic.Web.Services.ExternalIdentities;

/// <summary>
/// 验证后的外部身份主体：一次认证的临时结果，不持久化。
/// </summary>
/// <param name="SubjectId">平台返回的稳定用户标识（Apple 团队范围 sub / Google OIDC sub）。</param>
/// <param name="VerifiedEmail">平台声明的可选邮箱。</param>
/// <param name="VerifiedAt">验证时间（UTC）。</param>
public record UserExternalIdentityPrincipal(string SubjectId, string? VerifiedEmail, DateTimeOffset VerifiedAt);
