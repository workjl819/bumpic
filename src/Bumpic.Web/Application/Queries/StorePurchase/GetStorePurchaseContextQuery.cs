using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Bumpic.Web.Application.Queries.StorePurchase;

/// <summary>
/// 根据用户账户标识查询商店购买上下文。
/// </summary>
public record GetStorePurchaseContextQuery(UserAccountId UserAccountId, AppStore Store)
    : IQuery<GetStorePurchaseContextResult>;

/// <summary>
/// 商店购买上下文查询处理器。
/// </summary>
public class GetStorePurchaseContextQueryHandler(ApplicationDbContext dbContext)
    : IQueryHandler<GetStorePurchaseContextQuery, GetStorePurchaseContextResult>
{
    /// <summary>
    /// 一次查询返回用户账户标识和稳定购买账户标识。
    /// </summary>
    public async Task<GetStorePurchaseContextResult> Handle(
        GetStorePurchaseContextQuery request,
        CancellationToken cancellationToken)
    {
        return await dbContext.UserAccounts
                   .AsNoTracking()
                   .Where(x => x.Id == request.UserAccountId
                               && x.PurchaseAccountToken != Guid.Empty
                               && !x.Deleted)
                   .Select(x => new GetStorePurchaseContextResult(
                       UserAccountId: x.Id,
                       Store: request.Store,
                       PurchaseAccountToken: x.PurchaseAccountToken))
                   .SingleOrDefaultAsync(cancellationToken)
               ?? throw new KnownException("STORE_PURCHASE_CONTEXT_NOT_FOUND");
    }
}

/// <summary>
/// 商店购买上下文查询验证器。
/// </summary>
public class GetStorePurchaseContextQueryValidator : AbstractValidator<GetStorePurchaseContextQuery>
{
    /// <summary>
    /// 初始化验证规则。
    /// </summary>
    public GetStorePurchaseContextQueryValidator()
    {
        RuleFor(x => x.UserAccountId)
            .NotNull();
        RuleFor(x => x.Store)
            .IsInEnum();
    }
}
