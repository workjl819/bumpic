using Bumpic.Domain.AggregateModel.IdempotentRequestAggregate;
using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.IdempotentRequest;

/// <summary>
/// 完成幂等请求命令。
/// </summary>
public record CompleteIdempotentRequestCommand(
    IdempotentRequestId IdempotentRequestId,
    Guid LeaseToken,
    string ResultCode,
    StoreTransactionId? StoreTransactionId) : ICommand;

/// <summary>
/// 完成幂等请求命令处理器。
/// </summary>
public class CompleteIdempotentRequestCommandHandler(
    IIdempotentRequestRepository repository,
    IClock clock) : ICommandHandler<CompleteIdempotentRequestCommand>
{
    /// <summary>
    /// 使用 fencing 令牌提交确定性处理结果。
    /// </summary>
    public async Task Handle(
        CompleteIdempotentRequestCommand request,
        CancellationToken cancellationToken)
    {
        var idempotentRequest = await repository.GetAsync(request.IdempotentRequestId, cancellationToken)
                                ?? throw new KnownException("IDEMPOTENT_REQUEST_NOT_FOUND");
        idempotentRequest.Complete(
            leaseToken: request.LeaseToken,
            resultCode: request.ResultCode,
            storeTransactionId: request.StoreTransactionId,
            now: clock.UtcNow);
    }
}

/// <summary>
/// 完成幂等请求命令锁。
/// </summary>
public class CompleteIdempotentRequestCommandLock : ICommandLock<CompleteIdempotentRequestCommand>
{
    /// <summary>
    /// 同一幂等请求的状态提交串行执行。
    /// </summary>
    public Task<CommandLockSettings> GetLockKeysAsync(
        CompleteIdempotentRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:idempotent-request-id:{command.IdempotentRequestId.Id:N}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// 完成幂等请求命令验证器。
/// </summary>
public class CompleteIdempotentRequestCommandValidator
    : AbstractValidator<CompleteIdempotentRequestCommand>
{
    /// <summary>
    /// 初始化验证规则。
    /// </summary>
    public CompleteIdempotentRequestCommandValidator()
    {
        RuleFor(x => x.IdempotentRequestId).NotNull();
        RuleFor(x => x.LeaseToken).NotEmpty();
        RuleFor(x => x.ResultCode).NotEmpty().MaximumLength(100);
    }
}
