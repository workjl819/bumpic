using Microsoft.EntityFrameworkCore;
using Bumpic.Domain;
using Bumpic.Infrastructure;

namespace Bumpic.Web.Application.Queries.Registration;

/// <summary>
/// 邮箱是否已注册查询响应。
/// </summary>
/// <param name="IsRegistered">该邮箱是否已存在未删除账户；为 true 时客户端应走登录流程。</param>
public record CheckEmailRegisteredResponse(bool IsRegistered);

/// <summary>
/// 邮箱是否已注册查询。
/// </summary>
public record CheckEmailRegisteredQuery(string EmailAddress) : IQuery<CheckEmailRegisteredResponse>;

/// <summary>
/// 邮箱是否已注册查询验证器。
/// </summary>
public class CheckEmailRegisteredQueryValidator : AbstractValidator<CheckEmailRegisteredQuery>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public CheckEmailRegisteredQueryValidator()
    {
        RuleFor(x => x.EmailAddress)
            .NotEmpty().WithMessage("邮箱不能为空")
            .EmailAddress().WithMessage("邮箱格式不正确")
            .MaximumLength(320).WithMessage("邮箱长度不能超过 320 个字符");
    }
}

/// <summary>
/// 邮箱是否已注册查询处理器。
/// </summary>
public class CheckEmailRegisteredQueryHandler(ApplicationDbContext dbContext)
    : IQueryHandler<CheckEmailRegisteredQuery, CheckEmailRegisteredResponse>
{
    /// <inheritdoc />
    public async Task<CheckEmailRegisteredResponse> Handle(
        CheckEmailRegisteredQuery request,
        CancellationToken cancellationToken)
    {
        var email = EmailAddressNormalizer.Normalize(request.EmailAddress);
        var exists = await dbContext.UserAccounts.AnyAsync(
            account => account.EmailAddress == email && !account.Deleted,
            cancellationToken);
        return new CheckEmailRegisteredResponse(exists);
    }
}