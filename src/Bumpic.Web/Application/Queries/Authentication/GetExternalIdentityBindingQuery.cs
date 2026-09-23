using Microsoft.EntityFrameworkCore;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure;

namespace Bumpic.Web.Application.Queries.Authentication;

/// <summary>
/// 平台身份绑定查询响应。
/// </summary>
/// <param name="IsBound">该平台身份是否已绑定到未删除账户。</param>
/// <param name="UserAccountId">已绑定账户标识；未绑定时为 null。</param>
/// <param name="EmailAddress">已绑定账户的登录邮箱；未绑定时为空字符串。</param>
public record GetExternalIdentityBindingResponse(
    bool IsBound,
    UserAccountId? UserAccountId,
    string EmailAddress);

/// <summary>
/// 按平台与平台用户标识查询绑定关系及对应账户，供快捷登录/绑定流程判断当前身份是否已绑定。
/// </summary>
/// <param name="Provider">外部身份提供方。</param>
/// <param name="SubjectId">平台用户标识（Apple/Google 的 sub）。</param>
public record GetExternalIdentityBindingQuery(UserExternalIdentityProvider Provider, string SubjectId)
    : IQuery<GetExternalIdentityBindingResponse>;

/// <summary>
/// 平台身份绑定查询验证器。
/// </summary>
public class GetExternalIdentityBindingQueryValidator : AbstractValidator<GetExternalIdentityBindingQuery>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public GetExternalIdentityBindingQueryValidator()
    {
        RuleFor(x => x.SubjectId)
            .NotEmpty().WithMessage("平台用户标识不能为空")
            .MaximumLength(256).WithMessage("平台用户标识长度不能超过 256 个字符");
    }
}

/// <summary>
/// 平台身份绑定查询处理器：身份不存在或其账户已删除时都视为未绑定。
/// </summary>
public class GetExternalIdentityBindingQueryHandler(ApplicationDbContext dbContext)
    : IQueryHandler<GetExternalIdentityBindingQuery, GetExternalIdentityBindingResponse>
{
    /// <inheritdoc />
    public async Task<GetExternalIdentityBindingResponse> Handle(
        GetExternalIdentityBindingQuery request,
        CancellationToken cancellationToken)
    {
        var identity = await dbContext.UserExternalIdentities.FirstOrDefaultAsync(
            candidate => candidate.Provider == request.Provider
                && candidate.SubjectId == request.SubjectId
                && !candidate.Deleted,
            cancellationToken);
        if (identity is null)
        {
            return new GetExternalIdentityBindingResponse(false, null, string.Empty);
        }

        var account = await dbContext.UserAccounts.FirstOrDefaultAsync(
            candidate => candidate.Id == identity.UserAccountId && !candidate.Deleted,
            cancellationToken);
        if (account is null)
        {
            return new GetExternalIdentityBindingResponse(false, null, string.Empty);
        }

        return new GetExternalIdentityBindingResponse(true, account.Id, account.EmailAddress);
    }
}
