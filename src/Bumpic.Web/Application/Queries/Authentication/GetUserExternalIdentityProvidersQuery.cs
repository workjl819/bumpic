using Microsoft.EntityFrameworkCore;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure;

namespace Bumpic.Web.Application.Queries.Authentication;

/// <summary>
/// 查询用户已绑定的外部身份提供方，供注销流程校验需要携带哪些撤销凭据。
/// </summary>
/// <param name="UserAccountId">用户账户标识。</param>
public record GetUserExternalIdentityProvidersQuery(UserAccountId UserAccountId)
    : IQuery<IReadOnlyList<UserExternalIdentityProvider>>;

/// <summary>
/// 已绑定身份提供方查询验证器。
/// </summary>
public class GetUserExternalIdentityProvidersQueryValidator : AbstractValidator<GetUserExternalIdentityProvidersQuery>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public GetUserExternalIdentityProvidersQueryValidator()
    {
        RuleFor(x => x.UserAccountId).NotNull().Must(id => id.Id != Guid.Empty);
    }
}

/// <summary>
/// 已绑定外部身份提供方查询处理器。
/// </summary>
public class GetUserExternalIdentityProvidersQueryHandler(ApplicationDbContext dbContext)
    : IQueryHandler<GetUserExternalIdentityProvidersQuery, IReadOnlyList<UserExternalIdentityProvider>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<UserExternalIdentityProvider>> Handle(
        GetUserExternalIdentityProvidersQuery request,
        CancellationToken cancellationToken)
    {
        return await dbContext.UserExternalIdentities
            .Where(identity => identity.UserAccountId == request.UserAccountId && !identity.Deleted)
            .Select(identity => identity.Provider)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
