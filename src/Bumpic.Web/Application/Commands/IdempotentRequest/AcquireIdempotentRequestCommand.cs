using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.AggregateModel.IdempotentRequestAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;
using System.Security.Cryptography;
using System.Text;

namespace Bumpic.Web.Application.Commands.IdempotentRequest;

/// <summary>
/// 创建、领取或接管幂等请求命令。
/// </summary>
public record AcquireIdempotentRequestCommand(
    UserAccountId UserAccountId,
    string Operation,
    string IdempotencyKey,
    byte[] RequestHash,
    int RequestHashVersion,
    TimeSpan LeaseDuration) : ICommand<AcquireIdempotentRequestResult>;

/// <summary>
/// 创建、领取或接管幂等请求命令处理器。
/// </summary>
public class AcquireIdempotentRequestCommandHandler(
    IIdempotentRequestRepository repository,
    IClock clock) : ICommandHandler<AcquireIdempotentRequestCommand, AcquireIdempotentRequestResult>
{
    /// <summary>
    /// 获取当前请求的执行权或返回已有处理状态。
    /// </summary>
    public async Task<AcquireIdempotentRequestResult> Handle(
        AcquireIdempotentRequestCommand request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var leaseToken = Guid.NewGuid();
        var idempotentRequest = await repository.FindAsync(
            userAccountId: request.UserAccountId,
            operation: request.Operation,
            idempotencyKey: request.IdempotencyKey,
            cancellationToken: cancellationToken);

        if (idempotentRequest is null)
        {
            idempotentRequest = new Domain.AggregateModel.IdempotentRequestAggregate.IdempotentRequest(
                userAccountId: request.UserAccountId,
                operation: request.Operation,
                idempotencyKey: request.IdempotencyKey,
                requestHash: request.RequestHash,
                requestHashVersion: request.RequestHashVersion,
                leaseToken: leaseToken,
                leaseExpiresAt: now.Add(request.LeaseDuration),
                now: now);
            await repository.AddAsync(idempotentRequest, cancellationToken);
            return CreateResult(
                idempotentRequest: idempotentRequest,
                result: IdempotentRequestAcquireResult.Acquired,
                leaseToken: leaseToken);
        }

        var acquireResult = idempotentRequest.TryAcquire(
            requestHash: request.RequestHash,
            requestHashVersion: request.RequestHashVersion,
            newLeaseToken: leaseToken,
            leaseDuration: request.LeaseDuration,
            now: now);
        return CreateResult(
            idempotentRequest: idempotentRequest,
            result: acquireResult,
            leaseToken: acquireResult == IdempotentRequestAcquireResult.Acquired ? leaseToken : null);
    }

    private static AcquireIdempotentRequestResult CreateResult(
        Domain.AggregateModel.IdempotentRequestAggregate.IdempotentRequest idempotentRequest,
        IdempotentRequestAcquireResult result,
        Guid? leaseToken)
    {
        return new AcquireIdempotentRequestResult(
            IdempotentRequestId: idempotentRequest.Id,
            Result: result,
            LeaseToken: leaseToken,
            LeaseExpiresAt: idempotentRequest.LeaseExpiresAt,
            NextRetryAt: idempotentRequest.NextRetryAt,
            StoreTransactionId: idempotentRequest.StoreTransactionId,
            ResultCode: idempotentRequest.ResultCode);
    }
}

/// <summary>
/// 幂等请求领取命令锁。
/// </summary>
public class AcquireIdempotentRequestCommandLock : ICommandLock<AcquireIdempotentRequestCommand>
{
    /// <summary>
    /// 同一用户、操作和幂等键的领取过程串行执行。
    /// </summary>
    public Task<CommandLockSettings> GetLockKeysAsync(
        AcquireIdempotentRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        var identity = $"{command.UserAccountId.Id:N}:{command.Operation}:{command.IdempotencyKey}";
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:idempotent-request:{digest}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// 创建、领取或接管幂等请求命令验证器。
/// </summary>
public class AcquireIdempotentRequestCommandValidator
    : AbstractValidator<AcquireIdempotentRequestCommand>
{
    /// <summary>
    /// 初始化验证规则。
    /// </summary>
    public AcquireIdempotentRequestCommandValidator()
    {
        RuleFor(x => x.Operation).NotEmpty().MaximumLength(100);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(255);
        RuleFor(x => x.RequestHash).Must(x => x.Length == 32);
        RuleFor(x => x.RequestHashVersion).GreaterThan(0);
        RuleFor(x => x.LeaseDuration)
            .GreaterThan(TimeSpan.Zero)
            .LessThanOrEqualTo(TimeSpan.FromMinutes(10));
    }
}
