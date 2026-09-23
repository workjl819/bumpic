using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.Authentication;

/// <summary>
/// Google 绑定命令结果。
/// </summary>
/// <param name="SubjectId">Google 团队范围 sub。</param>
public record BindGoogleExternalIdentityResult(string SubjectId);

/// <summary>
/// 绑定 Google 身份命令：只操作外部身份聚合（Google 令牌校验由 Endpoint 在事务外完成）。
/// </summary>
public record BindGoogleUserExternalIdentityCommand(
    UserAccountId UserId,
    string SubjectId) : ICommand<BindGoogleExternalIdentityResult>;

/// <summary>
/// 绑定 Google 身份命令验证器。
/// </summary>
public class BindGoogleUserExternalIdentityCommandValidator : AbstractValidator<BindGoogleUserExternalIdentityCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public BindGoogleUserExternalIdentityCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotNull().WithMessage("用户标识不能为空")
            .Must(userId => userId.Id != Guid.Empty).WithMessage("用户标识不能为空");
        RuleFor(x => x.SubjectId)
            .NotEmpty().WithMessage("平台用户标识不能为空");
    }
}

/// <summary>
/// 绑定 Google 身份命令处理器。
/// </summary>
public class BindGoogleUserExternalIdentityCommandHandler(
    IUserAccountRepository userRepository,
    IUserExternalIdentityRepository identityRepository)
    : ICommandHandler<BindGoogleUserExternalIdentityCommand, BindGoogleExternalIdentityResult>
{
    /// <inheritdoc />
    public async Task<BindGoogleExternalIdentityResult> Handle(
        BindGoogleUserExternalIdentityCommand request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetAsync(request.UserId, cancellationToken);
        if (user is null || user.Deleted)
        {
            throw new KnownException("USER_NOT_FOUND");
        }

        var byUser = await identityRepository.FindByUserAndProviderAsync(
            request.UserId,
            UserExternalIdentityProvider.Google,
            cancellationToken);
        if (byUser is not null)
        {
            throw new KnownException("EXTERNAL_IDENTITY_CONFLICT");
        }

        var bySubject = await identityRepository.FindByProviderAndSubjectAsync(
            UserExternalIdentityProvider.Google,
            request.SubjectId,
            cancellationToken);
        if (bySubject is not null)
        {
            throw new KnownException("EXTERNAL_IDENTITY_CONFLICT");
        }

        var identity = UserExternalIdentity.Create(
            userAccountId: request.UserId,
            provider: UserExternalIdentityProvider.Google,
            subjectId: request.SubjectId);
        await identityRepository.AddAsync(identity, cancellationToken);
        return new BindGoogleExternalIdentityResult(request.SubjectId);
    }
}
