using Bumpic.Domain.Enums;

namespace Bumpic.Domain.AggregateModel.UserAccountAggregate;

/// <summary>
/// 外部身份标识。
/// </summary>
public partial record UserExternalIdentityId : IGuidStronglyTypedId;

/// <summary>
/// 用户快捷登录身份聚合根；独立表保存，通过用户账户标识逻辑关联。
/// </summary>
public class UserExternalIdentity : Entity<UserExternalIdentityId>, IAggregateRoot
{
    /// <summary>
    /// 供 EF Core 使用的构造函数。
    /// </summary>
    protected UserExternalIdentity()
    {
    }

    /// <summary>
    /// 为用户绑定一个外部身份。
    /// </summary>
    /// <param name="userAccountId">逻辑关联的用户账户标识。</param>
    /// <param name="provider">外部身份提供方。</param>
    /// <param name="subjectId">提供方返回的稳定用户标识。</param>
    /// <returns>新建的外部身份。</returns>
    public static UserExternalIdentity Create(
        UserAccountId userAccountId,
        UserExternalIdentityProvider provider,
        string subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            throw new KnownException("EXTERNAL_SUBJECT_REQUIRED");
        }

        var now = DateTimeOffset.UtcNow;
        return new UserExternalIdentity
        {
            UserAccountId = userAccountId,
            Provider = provider,
            SubjectId = subjectId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 逻辑关联的用户账户标识。
    /// </summary>
    public UserAccountId UserAccountId { get; private set; } = new UserAccountId(Guid.Empty);

    /// <summary>
    /// 外部身份提供方。
    /// </summary>
    public UserExternalIdentityProvider Provider { get; private set; } = UserExternalIdentityProvider.Apple;

    /// <summary>
    /// 提供方返回的稳定用户标识。
    /// </summary>
    public string SubjectId { get; private set; } = string.Empty;

    /// <summary>
    /// 身份绑定时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// 身份最近更新时间。
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// 软删除标记。
    /// </summary>
    public bool Deleted { get; private set; } = false;

    /// <summary>
    /// 乐观并发控制版本。
    /// </summary>
    public RowVersion RowVersion { get; private set; } = new(0);

    /// <summary>
    /// 保存平台撤销令牌密文；账户注销时由集成事件处理器在撤销成功后清除。
    /// </summary>
    /// <param name="ciphertext">加密后的平台撤销令牌。</param>
    /// <param name="now">当前时间。</param>
    public void SetRevocationToken(string ciphertext, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertext);

        RevocationTokenCiphertext = ciphertext;
        UpdatedAt = now;
    }

    /// <summary>
    /// 清除平台撤销令牌密文（撤销成功后调用）。
    /// </summary>
    /// <param name="now">当前时间。</param>
    public void ClearRevocationToken(DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(RevocationTokenCiphertext))
        {
            return;
        }

        RevocationTokenCiphertext = string.Empty;
        UpdatedAt = now;
    }

    /// <summary>
    /// 平台撤销令牌密文；为空表示无需撤销或已完成撤销。仅密文落库，禁止写入日志。
    /// </summary>
    public string RevocationTokenCiphertext { get; private set; } = string.Empty;

    /// <summary>
    /// 逻辑删除外部身份；账户注销流程调用，实体保留用于审计。
    /// </summary>
    /// <param name="now">当前时间。</param>
    public void Delete(DateTimeOffset now)
    {
        if (Deleted)
        {
            return;
        }

        Deleted = true;
        UpdatedAt = now;
    }

}
