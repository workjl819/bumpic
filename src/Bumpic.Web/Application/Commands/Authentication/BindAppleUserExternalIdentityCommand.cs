using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.Authentication;

/// <summary>
/// Apple 绑定命令结果。
/// </summary>
/// <param name="SubjectId">Apple 团队范围 sub。</param>
public record BindAppleExternalIdentityResult(string SubjectId);

/// <summary>
/// 绑定 Apple 身份命令：只操作外部身份聚合（Apple 令牌校验由 Endpoint 在事务外完成）。
/// </summary>
public record BindAppleUserExternalIdentityCommand(
    UserAccountId UserId,
    string SubjectId) : ICommand<BindAppleExternalIdentityResult>;

/// <summary>
/// 绑定 Apple 身份命令验证器。
/// </summary>
public class BindAppleUserExternalIdentityCommandValidator : AbstractValidator<BindAppleUserExternalIdentityCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public BindAppleUserExternalIdentityCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotNull().WithMessage("用户标识不能为空")
            .Must(userId => userId.Id != Guid.Empty).WithMessage("用户标识不能为空");
        RuleFor(x => x.SubjectId)
            .NotEmpty().WithMessage("平台用户标识不能为空");
    }
}

/// <summary>
/// 绑定 Apple 身份命令处理器。
/// </summary>
public class BindAppleUserExternalIdentityCommandHandler(
    IUserAccountRepository userRepository,
    IUserExternalIdentityRepository identityRepository)
    : ICommandHandler<BindAppleUserExternalIdentityCommand, BindAppleExternalIdentityResult>
{
    /// <inheritdoc />
    public async Task<BindAppleExternalIdentityResult> Handle(
        BindAppleUserExternalIdentityCommand request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetAsync(request.UserId, cancellationToken);
        if (user is null || user.Deleted)
        {
            throw new KnownException("USER_NOT_FOUND");
        }

        var byUser = await identityRepository.FindByUserAndProviderAsync(
            request.UserId,
            UserExternalIdentityProvider.Apple,
            cancellationToken);
        if (byUser is not null)
        {
            throw new KnownException("EXTERNAL_IDENTITY_CONFLICT");
        }

        var bySubject = await identityRepository.FindByProviderAndSubjectAsync(
            UserExternalIdentityProvider.Apple,
            request.SubjectId,
            cancellationToken);
        if (bySubject is not null)
        {
            throw new KnownException("EXTERNAL_IDENTITY_CONFLICT");
        }

        var identity = UserExternalIdentity.Create(
            userAccountId: request.UserId,
            provider: UserExternalIdentityProvider.Apple,
            subjectId: request.SubjectId);
        await identityRepository.AddAsync(identity, cancellationToken);
        return new BindAppleExternalIdentityResult(request.SubjectId);
    }
}
