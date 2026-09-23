using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Microsoft.EntityFrameworkCore;
using Bumpic.Domain.Enums;

namespace Bumpic.Web.Application.Queries.PointAccount;

/// <summary>
/// 分页查询当前用户点数流水。
/// </summary>
/// <param name="UserAccountId">当前用户账户标识。</param>
/// <param name="PageIndex">页码，从 1 开始。</param>
/// <param name="PageSize">每页数量。</param>
public record GetAccountPointRecordsQuery(
    UserAccountId UserAccountId,
    int PageIndex,
    int PageSize) : IQuery<PagedData<AccountPointRecordItem>>;

/// <summary>
/// 当前用户点数流水分页查询处理器。
/// </summary>
public class GetAccountPointRecordsQueryHandler(ApplicationDbContext dbContext)
    : IQueryHandler<GetAccountPointRecordsQuery, PagedData<AccountPointRecordItem>>
{
    /// <summary>
    /// 按创建时间和强类型标识倒序返回不可变点数流水。
    /// </summary>
    /// <param name="request">查询请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>当前用户点数流水分页结果。</returns>
    public async Task<PagedData<AccountPointRecordItem>> Handle(
        GetAccountPointRecordsQuery request,
        CancellationToken cancellationToken)
    {
        return await dbContext.AccountPointRecords
            .AsNoTracking()
            .Where(item => item.UserAccountId == request.UserAccountId && !item.Deleted)
            .Where(item => item.Type == AccountPointRecordType.RegistrationGranted
                           || item.Type == AccountPointRecordType.InvitationGranted
                           || item.Type == AccountPointRecordType.PurchaseGranted
                           || item.Type == AccountPointRecordType.RestorationSettled
                           || item.Type == AccountPointRecordType.PurchaseReversed
                           || item.Type == AccountPointRecordType.PurchaseReinstated)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new AccountPointRecordItem(
                AccountPointRecordId: x.Id,
                Type: x.Type,
                Amount: x.Amount,
                IsPositive: x.Type == AccountPointRecordType.RegistrationGranted
                            || x.Type == AccountPointRecordType.InvitationGranted
                            || x.Type == AccountPointRecordType.PurchaseGranted
                            || x.Type == AccountPointRecordType.PurchaseReinstated,
                CreatedAt: x.CreatedAt))
            .ToPagedDataAsync(
                pageIndex: request.PageIndex,
                pageSize: request.PageSize,
                countTotal: true,
                cancellationToken: cancellationToken);
    }
}

/// <summary>
/// 当前用户点数流水分页查询验证器。
/// </summary>
public class GetAccountPointRecordsQueryValidator : AbstractValidator<GetAccountPointRecordsQuery>
{
    /// <summary>
    /// 初始化查询验证规则。
    /// </summary>
    public GetAccountPointRecordsQueryValidator()
    {
        RuleFor(x => x.UserAccountId)
            .NotNull()
            .Must(userAccountId => userAccountId is not null && userAccountId.Id != Guid.Empty);
        RuleFor(x => x.PageIndex).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
