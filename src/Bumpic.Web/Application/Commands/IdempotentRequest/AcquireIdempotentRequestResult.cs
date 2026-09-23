using Bumpic.Domain.AggregateModel.IdempotentRequestAggregate;
using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Web.Application.Commands.IdempotentRequest;

/// <summary>
/// 幂等请求领取结果。
/// </summary>
public record AcquireIdempotentRequestResult(
    IdempotentRequestId IdempotentRequestId,
    IdempotentRequestAcquireResult Result,
    Guid? LeaseToken,
    DateTimeOffset? LeaseExpiresAt,
    DateTimeOffset? NextRetryAt,
    StoreTransactionId? StoreTransactionId,
    string? ResultCode);
