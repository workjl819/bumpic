using Microsoft.Extensions.Options;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Infrastructure.Repositories;
using Bumpic.Web.Options;

namespace Bumpic.Web.Application.Commands.Authentication;

/// <summary>
/// 提交账户注销命令结果。
/// </summary>
/// <param name="ScheduledDeletionAt">计划清除数据的时间（宽限期到期时间）。</param>
public record RequestAccountDeletionResult(DateTimeOffset ScheduledDeletionAt);

/// <summary>
/// 提交账户注销命令：打上注销标记并进入配置的宽限期（AccountDeletion:GracePeriod，默认 30 天）；宽限期内账户可正常使用，用户登录即自动取消。
/// 用于撤销 Apple / Google 授权的凭据由 Endpoint 在事务外换取后传入。
/// </summary>
/// <param name="UserAccountId">当前登录用户。</param>
/// <param name="AppleRevocationTokenCiphertext">
/// Apple 撤销令牌密文（可选）：由 Endpoint 在事务外用授权码换取并加密，随领域事件写入外部身份绑定表，
/// 宽限期届满后由集成事件处理器解密并调用 Apple 撤销接口。
/// </param>
public record RequestAccountDeletionCommand(
    UserAccountId UserAccountId,
    string? AppleRevocationTokenCiphertext = null) : ICommand<RequestAccountDeletionResult>;

/// <summary>
/// 提交账户注销命令验证器。
/// </summary>
public class RequestAccountDeletionCommandValidator : AbstractValidator<RequestAccountDeletionCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public RequestAccountDeletionCommandValidator()
    {
        RuleFor(x => x.UserAccountId)
            .NotNull().WithMessage("用户标识不能为空")
            .Must(userId => userId.Id != Guid.Empty).WithMessage("用户标识不能为空");
    }
}

/// <summary>
/// 提交账户注销命令处理器。
/// </summary>
public class RequestAccountDeletionCommandHandler(
    IUserAccountRepository userRepository,
    IOptions<AccountDeletionOptions> accountDeletionOptions)
    : ICommandHandler<RequestAccountDeletionCommand, RequestAccountDeletionResult>
{
    /// <inheritdoc />
    public async Task<RequestAccountDeletionResult> Handle(
        RequestAccountDeletionCommand request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetAsync(request.UserAccountId, cancellationToken);
        if (user is null || user.Deleted)
        {
            throw new KnownException("USER_NOT_FOUND");
        }

        var now = DateTimeOffset.UtcNow;
        var gracePeriod = accountDeletionOptions.Value.GracePeriod;
        user.RequestDeletion(now, gracePeriod, request.AppleRevocationTokenCiphertext);
        return new RequestAccountDeletionResult(now.Add(gracePeriod));
    }
}
