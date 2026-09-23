using Bumpic.Domain.AggregateModel.IdempotentRequestAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.IdempotentRequest;

/// <summary>
/// 标记幂等请求可重试失败命令。
/// </summary>
public record MarkIdempotentRequestRetryableFailedCommand(
    IdempotentRequestId IdempotentRequestId,
    Guid LeaseToken,
    string ResultCode,
    DateTimeOffset NextRetryAt) : ICommand;

/// <summary>
/// 标记幂等请求可重试失败命令处理器。
/// </summary>
public class MarkIdempotentRequestRetryableFailedCommandHandler(
    IIdempotentRequestRepository repository,
    IClock clock) : ICommandHandler<MarkIdempotentRequestRetryableFailedCommand>
{
    /// <summary>
    /// 释放当前租约并设置下一次允许重试时间。
    /// </summary>
    public async Task Handle(
        MarkIdempotentRequestRetryableFailedCommand request,
        CancellationToken cancellationToken)
    {
        var idempotentRequest = await repository.GetAsync(request.IdempotentRequestId, cancellationToken)
                                ?? throw new KnownException("IDEMPOTENT_REQUEST_NOT_FOUND");
        idempotentRequest.MarkRetryableFailed(
            leaseToken: request.LeaseToken,
            resultCode: request.ResultCode,
            nextRetryAt: request.NextRetryAt,
            now: clock.UtcNow);
    }
}

/// <summary>
/// 标记幂等请求可重试失败命令锁。
/// </summary>
public class MarkIdempotentRequestRetryableFailedCommandLock
    : ICommandLock<MarkIdempotentRequestRetryableFailedCommand>
{
    /// <summary>
    /// 同一幂等请求的状态提交串行执行。
    /// </summary>
    public Task<CommandLockSettings> GetLockKeysAsync(
        MarkIdempotentRequestRetryableFailedCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:idempotent-request-id:{command.IdempotentRequestId.Id:N}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// 标记幂等请求可重试失败命令验证器。
/// </summary>
public class MarkIdempotentRequestRetryableFailedCommandValidator
    : AbstractValidator<MarkIdempotentRequestRetryableFailedCommand>
{
    /// <summary>
    /// 初始化验证规则。
    /// </summary>
    public MarkIdempotentRequestRetryableFailedCommandValidator()
    {
        RuleFor(x => x.IdempotentRequestId).NotNull();
        RuleFor(x => x.LeaseToken).NotEmpty();
        RuleFor(x => x.ResultCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NextRetryAt).NotEmpty();
    }
}
