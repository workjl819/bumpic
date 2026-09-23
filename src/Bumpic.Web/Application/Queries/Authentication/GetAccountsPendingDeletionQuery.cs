using Microsoft.EntityFrameworkCore;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Infrastructure;

namespace Bumpic.Web.Application.Queries.Authentication;

/// <summary>
/// 查询注销申请已过期、尚未软删除的账户标识，供定时任务分批执行注销。
/// </summary>
/// <param name="RequestedBefore">注销申请时间上限（含）。</param>
/// <param name="MaxCount">单批最大数量。</param>
public record GetAccountsPendingDeletionQuery(DateTimeOffset RequestedBefore, int MaxCount)
    : IQuery<List<UserAccountId>>;

/// <summary>
/// 待注销账户查询验证器。
/// </summary>
public class GetAccountsPendingDeletionQueryValidator : AbstractValidator<GetAccountsPendingDeletionQuery>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public GetAccountsPendingDeletionQueryValidator()
    {
        RuleFor(x => x.MaxCount)
            .GreaterThan(0).WithMessage("单批数量必须大于 0")
            .LessThanOrEqualTo(500).WithMessage("单批数量不能超过 500");
    }
}

/// <summary>
/// 待注销账户查询处理器。
/// </summary>
public class GetAccountsPendingDeletionQueryHandler(ApplicationDbContext dbContext)
    : IQueryHandler<GetAccountsPendingDeletionQuery, List<UserAccountId>>
{
    /// <inheritdoc />
    public async Task<List<UserAccountId>> Handle(
        GetAccountsPendingDeletionQuery request,
        CancellationToken cancellationToken)
    {
        return await dbContext.UserAccounts
            .Where(account => account.DeletionRequestedAt != null
                && account.DeletionRequestedAt <= request.RequestedBefore
                && !account.Deleted)
            .OrderBy(account => account.DeletionRequestedAt)
            .Take(request.MaxCount)
            .Select(account => account.Id)
            .ToListAsync(cancellationToken);
    }
}
