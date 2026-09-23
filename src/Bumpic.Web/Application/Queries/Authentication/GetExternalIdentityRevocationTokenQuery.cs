using Microsoft.EntityFrameworkCore;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure;

namespace Bumpic.Web.Application.Queries.Authentication;

/// <summary>
/// 外部身份撤销令牌查询响应。
/// </summary>
/// <param name="BindingExists">该用户是否存在对应平台的绑定记录（含已软删除）；false 表示无需撤销。</param>
/// <param name="Ciphertext">撤销令牌密文；为空表示绑定存在但未捕获到令牌。</param>
public record GetExternalIdentityRevocationTokenResponse(bool BindingExists, string Ciphertext);

/// <summary>
/// 查询外部身份绑定表上的平台撤销令牌密文；账户注销后绑定已软删除，因此忽略查询过滤器。
/// </summary>
/// <param name="UserAccountId">用户账户标识。</param>
/// <param name="Provider">外部身份提供方。</param>
public record GetExternalIdentityRevocationTokenQuery(
    UserAccountId UserAccountId,
    UserExternalIdentityProvider Provider) : IQuery<GetExternalIdentityRevocationTokenResponse>;

/// <summary>
/// 平台撤销令牌查询验证器。
/// </summary>
public class GetExternalIdentityRevocationTokenQueryValidator : AbstractValidator<GetExternalIdentityRevocationTokenQuery>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public GetExternalIdentityRevocationTokenQueryValidator()
    {
        RuleFor(x => x.UserAccountId).NotNull().Must(id => id.Id != Guid.Empty);
        RuleFor(x => x.Provider).IsInEnum();
    }
}

/// <summary>
/// 平台撤销令牌查询处理器。
/// </summary>
public class GetExternalIdentityRevocationTokenQueryHandler(ApplicationDbContext dbContext)
    : IQueryHandler<GetExternalIdentityRevocationTokenQuery, GetExternalIdentityRevocationTokenResponse>
{
    /// <inheritdoc />
    public async Task<GetExternalIdentityRevocationTokenResponse> Handle(
        GetExternalIdentityRevocationTokenQuery request,
        CancellationToken cancellationToken)
    {
        var ciphertext = await dbContext.UserExternalIdentities
            .IgnoreQueryFilters()
            .Where(identity => identity.UserAccountId == request.UserAccountId
                && identity.Provider == request.Provider)
            .OrderByDescending(identity => identity.CreatedAt)
            .Select(identity => identity.RevocationTokenCiphertext)
            .FirstOrDefaultAsync(cancellationToken);

        // 区分「没有该平台绑定」与「有绑定但没有令牌」：前者无需撤销，后者属于流程缺陷，必须暴露。
        return ciphertext is null
            ? new GetExternalIdentityRevocationTokenResponse(false, string.Empty)
            : new GetExternalIdentityRevocationTokenResponse(true, ciphertext);
    }
}
