using Microsoft.EntityFrameworkCore;
using Bumpic.Domain;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Infrastructure;

namespace Bumpic.Web.Application.Queries.Registration;

/// <summary>
/// 按邮箱查询账户结果。
/// </summary>
/// <param name="Found">是否找到未删除账户。</param>
/// <param name="UserId">用户账户标识；未找到时为 null。</param>
/// <param name="EmailAddress">规范化后的邮箱。</param>
/// <param name="InvitationCode">本人邀请码；未找到时为空字符串。</param>
public record GetUserAccountByEmailResponse(
    bool Found,
    UserAccountId? UserId,
    string EmailAddress,
    string InvitationCode);

/// <summary>
/// 按规范化邮箱查询未删除账户。
/// </summary>
public record GetUserAccountByEmailQuery(string EmailAddress) : IQuery<GetUserAccountByEmailResponse>;

/// <summary>
/// 按邮箱查询账户验证器。
/// </summary>
public class GetUserAccountByEmailQueryValidator : AbstractValidator<GetUserAccountByEmailQuery>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public GetUserAccountByEmailQueryValidator()
    {
        RuleFor(x => x.EmailAddress)
            .NotEmpty().WithMessage("邮箱不能为空");
    }
}

/// <summary>
/// 按邮箱查询账户处理器。
/// </summary>
public class GetUserAccountByEmailQueryHandler(ApplicationDbContext dbContext)
    : IQueryHandler<GetUserAccountByEmailQuery, GetUserAccountByEmailResponse>
{
    /// <inheritdoc />
    public async Task<GetUserAccountByEmailResponse> Handle(
        GetUserAccountByEmailQuery request,
        CancellationToken cancellationToken)
    {
        var email = EmailAddressNormalizer.Normalize(request.EmailAddress);
        var account = await dbContext.UserAccounts.FirstOrDefaultAsync(
            candidate => candidate.EmailAddress == email && !candidate.Deleted,
            cancellationToken);
        if (account is null)
        {
            return new GetUserAccountByEmailResponse(
                Found: false,
                UserId: null,
                EmailAddress: email,
                InvitationCode: string.Empty);
        }

        return new GetUserAccountByEmailResponse(
            Found: true,
            UserId: account.Id,
            EmailAddress: account.EmailAddress,
            InvitationCode: account.InvitationCode);
    }
}