using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Microsoft.EntityFrameworkCore;

namespace Bumpic.Web.Application.Queries.PointAccount;

/// <summary>
/// 查询当前用户点数余额。
/// </summary>
public record GetPointBalanceQuery(UserAccountId UserAccountId) : IQuery<GetPointBalanceResult>;

/// <summary>
/// 当前用户点数余额查询处理器。
/// </summary>
public class GetPointBalanceQueryHandler(ApplicationDbContext dbContext)
    : IQueryHandler<GetPointBalanceQuery, GetPointBalanceResult>
{
    /// <summary>
    /// 返回当前余额；尚未建立点数账户时返回零余额。
    /// </summary>
    public async Task<GetPointBalanceResult> Handle(
        GetPointBalanceQuery request,
        CancellationToken cancellationToken)
    {
        return await dbContext.PointAccounts
                   .AsNoTracking()
                   .Where(x => x.UserAccountId == request.UserAccountId && !x.Deleted)
                   .Select(x => new GetPointBalanceResult(
                       x.AvailablePoints,
                       x.FrozenPoints))
                   .SingleOrDefaultAsync(cancellationToken)
               ?? new GetPointBalanceResult(AvailablePoints: 0, FrozenPoints: 0);
    }
}

/// <summary>
/// 当前用户点数余额查询验证器。
/// </summary>
public class GetPointBalanceQueryValidator : AbstractValidator<GetPointBalanceQuery>
{
    /// <summary>
    /// 初始化查询验证规则。
    /// </summary>
    public GetPointBalanceQueryValidator()
    {
        RuleFor(x => x.UserAccountId).NotNull();
    }
}
