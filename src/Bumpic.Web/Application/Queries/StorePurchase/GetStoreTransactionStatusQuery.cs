using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Microsoft.EntityFrameworkCore;

namespace Bumpic.Web.Application.Queries.StorePurchase;

/// <summary>
/// 查询当前用户商店交易状态查询。
/// </summary>
public record GetStoreTransactionStatusQuery(
    UserAccountId UserAccountId,
    StoreTransactionId StoreTransactionId)
    : IQuery<GetStoreTransactionStatusResult>;

/// <summary>
/// 商店交易状态查询处理器。
/// </summary>
public class GetStoreTransactionStatusQueryHandler(ApplicationDbContext dbContext)
    : IQueryHandler<GetStoreTransactionStatusQuery, GetStoreTransactionStatusResult>
{
    /// <summary>
    /// 只返回当前登录用户自己的交易和当前余额。
    /// </summary>
    public async Task<GetStoreTransactionStatusResult> Handle(
        GetStoreTransactionStatusQuery request,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.StoreTransactions
            .AsNoTracking()
            .Where(x => x.Id == request.StoreTransactionId
                        && x.UserAccountId == request.UserAccountId
                        && !x.Deleted)
            .Select(x => new
            {
                x.Store,
                x.ProductId,
                x.Status,
                x.GrantedPoints,
                x.ReversedPoints,
                x.FailureCode,
                x.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KnownException("PURCHASE_NOT_FOUND");
        var account = await dbContext.PointAccounts
            .AsNoTracking()
            .Where(x => x.UserAccountId == request.UserAccountId && !x.Deleted)
            .Select(x => new { x.AvailablePoints, x.FrozenPoints })
            .SingleOrDefaultAsync(cancellationToken);

        return new GetStoreTransactionStatusResult(
            StoreTransactionId: request.StoreTransactionId,
            Store: transaction.Store,
            ProductId: transaction.ProductId,
            Status: transaction.Status,
            GrantedPoints: transaction.GrantedPoints,
            ReversedPoints: transaction.ReversedPoints,
            FailureCode: transaction.FailureCode,
            AvailablePoints: account?.AvailablePoints ?? 0,
            FrozenPoints: account?.FrozenPoints ?? 0,
            UpdatedAt: transaction.UpdatedAt);
    }
}

/// <summary>
/// 商店交易状态查询验证器。
/// </summary>
public class GetStoreTransactionStatusQueryValidator
    : AbstractValidator<GetStoreTransactionStatusQuery>
{
    /// <summary>
    /// 初始化查询条件规则。
    /// </summary>
    public GetStoreTransactionStatusQueryValidator()
    {
        RuleFor(x => x.UserAccountId).NotNull();
        RuleFor(x => x.StoreTransactionId).NotNull();
    }
}
