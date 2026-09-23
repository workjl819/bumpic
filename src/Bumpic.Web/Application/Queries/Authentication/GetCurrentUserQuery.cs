using Microsoft.EntityFrameworkCore;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure;

namespace Bumpic.Web.Application.Queries.Authentication;

/// <summary>
/// 已绑定外部身份信息。
/// </summary>
/// <param name="Provider">外部身份提供方（Apple / Google）；注销账户时客户端据此决定需要提交哪一份平台凭据。</param>
/// <param name="BoundAt">绑定时间。</param>
public record BoundExternalIdentityInfo(UserExternalIdentityProvider Provider, DateTimeOffset BoundAt);

/// <summary>
/// 当前用户信息。
/// </summary>
/// <param name="UserId">用户账户标识。</param>
/// <param name="EmailAddress">规范化后的登录邮箱。</param>
/// <param name="Status">账户状态（Active / Disabled / Deleted）。</param>
/// <param name="InvitationCode">本人邀请码，可直接分享。</param>
/// <param name="ExternalIdentities">
/// 已绑定的外部身份列表，未绑定时为空数组。客户端据此判断注销账户时需要提交哪些平台凭据
/// （已绑定 Apple 需提交 appleAuthorizationCode，已绑定 Google 需提交 googleAccessToken）。
/// </param>
public record CurrentUserInfo(
    UserAccountId UserId,
    string EmailAddress,
    UserAccountStatus Status,
    string InvitationCode,
    IReadOnlyList<BoundExternalIdentityInfo> ExternalIdentities);

/// <summary>
/// 查询当前用户与绑定身份。
/// </summary>
public record GetCurrentUserQuery(UserAccountId UserId) : IQuery<CurrentUserInfo>;

/// <summary>
/// 查询当前用户验证器。
/// </summary>
public class GetCurrentUserQueryValidator : AbstractValidator<GetCurrentUserQuery>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public GetCurrentUserQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotNull().WithMessage("用户标识不能为空")
            .Must(userId => userId.Id != Guid.Empty).WithMessage("用户标识不能为空");
    }
}

/// <summary>
/// 查询当前用户处理器。
/// </summary>
public class GetCurrentUserQueryHandler(ApplicationDbContext dbContext)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserInfo>
{
    /// <inheritdoc />
    public async Task<CurrentUserInfo> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.UserAccounts.FirstOrDefaultAsync(
            account => account.Id == request.UserId && !account.Deleted,
            cancellationToken);
        if (user is null)
        {
            throw new KnownException("USER_NOT_FOUND");
        }

        var identities = await dbContext.UserExternalIdentities
            .Where(identity => identity.UserAccountId == request.UserId && !identity.Deleted)
            .Select(identity => new BoundExternalIdentityInfo(identity.Provider, identity.CreatedAt))
            .ToListAsync(cancellationToken);

        return new CurrentUserInfo(
            UserId: user.Id,
            EmailAddress: user.EmailAddress,
            Status: user.Status,
            InvitationCode: user.InvitationCode,
            ExternalIdentities: identities);
    }
}