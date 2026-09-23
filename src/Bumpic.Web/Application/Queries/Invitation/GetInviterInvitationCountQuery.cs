using Microsoft.EntityFrameworkCore;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure;

namespace Bumpic.Web.Application.Queries.Invitation;

/// <summary>
/// 邀请人有效邀请人数查询响应。
/// </summary>
/// <param name="InvitationCount">
/// 邀请人已发放邀请奖励的邀请人数。每个受邀用户只能建立一次邀请关系、每个邀请关系只发放一次奖励，
/// 因此邀请奖励流水条数等于有效邀请人数。
/// </param>
public record GetInviterInvitationCountResponse(int InvitationCount);

/// <summary>
/// 查询邀请人的有效邀请人数，用于邀请人数上限判断。
/// </summary>
/// <param name="InviterUserAccountId">邀请人用户账户标识。</param>
public record GetInviterInvitationCountQuery(UserAccountId InviterUserAccountId)
    : IQuery<GetInviterInvitationCountResponse>;

/// <summary>
/// 邀请人数查询验证器。
/// </summary>
public class GetInviterInvitationCountQueryValidator : AbstractValidator<GetInviterInvitationCountQuery>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public GetInviterInvitationCountQueryValidator()
    {
        RuleFor(x => x.InviterUserAccountId)
            .Must(inviter => inviter.Id != Guid.Empty).WithMessage("邀请人标识不能为空");
    }
}

/// <summary>
/// 邀请人数查询处理器。
/// </summary>
public class GetInviterInvitationCountQueryHandler(ApplicationDbContext dbContext)
    : IQueryHandler<GetInviterInvitationCountQuery, GetInviterInvitationCountResponse>
{
    /// <inheritdoc />
    public async Task<GetInviterInvitationCountResponse> Handle(
        GetInviterInvitationCountQuery request,
        CancellationToken cancellationToken)
    {
        var invitationCount = await dbContext.AccountPointRecords.CountAsync(
            record => record.UserAccountId == request.InviterUserAccountId
                && record.Type == AccountPointRecordType.InvitationGranted
                && !record.Deleted,
            cancellationToken);
        return new GetInviterInvitationCountResponse(invitationCount);
    }
}
